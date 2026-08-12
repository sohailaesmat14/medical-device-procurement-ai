using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace MediTender.API.Services
{
    public class PaymobService : IPaymobService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly string _baseUrl = "https://accept.paymob.com/api";

        public PaymobService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<string> GetPaymentIframeUrlAsync(decimal amount, string userEmail, string planType)
        {
            var apiKey = _configuration["Paymob:ApiKey"];
            var integrationId = _configuration["Paymob:IntegrationId"];
            var iframeId = _configuration["Paymob:IframeId"];

            var authPayload = new { api_key = apiKey };
            var authResponse = await PostAsync($"{_baseUrl}/auth/tokens", authPayload);
            var authToken = authResponse.GetProperty("token").GetString();

            var orderPayload = new
            {
                auth_token = authToken,
                delivery_needed = "false",
                amount_cents = (amount * 100).ToString("0"),
                currency = "EGP",
                items = new object[] { }
            };
            var orderResponse = await PostAsync($"{_baseUrl}/ecommerce/orders", orderPayload);
            var orderId = orderResponse.GetProperty("id").GetInt32();

            var paymentKeyPayload = new
            {
                auth_token = authToken,
                amount_cents = (amount * 100).ToString("0"),
                expiration = 3600,
                order_id = orderId,
                billing_data = new
                {
                    apartment = planType, 
                    email = userEmail, 
                    floor = "NA", 
                    first_name = userEmail.Split('@')[0],
                    street = "NA", 
                    building = "NA", 
                    phone_number = "+201000000000",
                    shipping_method = "NA", 
                    postal_code = "NA", 
                    city = "Cairo",
                    country = "EG", 
                    last_name = "User", 
                    state = "NA"
                },
                currency = "EGP",
                integration_id = int.Parse(integrationId!)
            };
            var paymentKeyResponse = await PostAsync($"{_baseUrl}/acceptance/payment_keys", paymentKeyPayload);
            var paymentToken = paymentKeyResponse.GetProperty("token").GetString();

            return $"https://accept.paymob.com/api/acceptance/iframes/{iframeId}?payment_token={paymentToken}";
        }

        public bool VerifyHmac(string receivedHmac, string jsonPayload)
        {
            var hmacSecret = _configuration["Paymob:HmacSecret"];
            if (string.IsNullOrEmpty(hmacSecret)) return false;

            using var doc = JsonDocument.Parse(jsonPayload);
            var obj = doc.RootElement.GetProperty("obj");

            var amountCents = obj.GetProperty("amount_cents").GetRawText();
            var createdAt = obj.GetProperty("created_at").GetString();
            var currency = obj.GetProperty("currency").GetString();
            var errorOccured = obj.GetProperty("error_occured").GetBoolean().ToString().ToLower();
            var hasParentTransaction = obj.GetProperty("has_parent_transaction").GetBoolean().ToString().ToLower();
            var id = obj.GetProperty("id").GetRawText();
            var integrationId = obj.GetProperty("integration_id").GetRawText();
            var is3dSecure = obj.GetProperty("is_3d_secure").GetBoolean().ToString().ToLower();
            var isAuth = obj.GetProperty("is_auth").GetBoolean().ToString().ToLower();
            var isCapture = obj.GetProperty("is_capture").GetBoolean().ToString().ToLower();
            var isRefunded = obj.GetProperty("is_refunded").GetBoolean().ToString().ToLower();
            var isStandalonePayment = obj.GetProperty("is_standalone_payment").GetBoolean().ToString().ToLower();
            var isVoided = obj.GetProperty("is_voided").GetBoolean().ToString().ToLower();
            var orderId = obj.GetProperty("order").GetProperty("id").GetRawText();
            var owner = obj.GetProperty("owner").GetRawText();
            var pending = obj.GetProperty("pending").GetBoolean().ToString().ToLower();
            
            var sourceData = obj.GetProperty("source_data");
            var sourceDataPan = sourceData.TryGetProperty("pan", out var pan) ? pan.GetString() : "";
            var sourceDataSubType = sourceData.TryGetProperty("sub_type", out var subType) ? subType.GetString() : "";
            var sourceDataType = sourceData.TryGetProperty("type", out var type) ? type.GetString() : "";
            
            var success = obj.GetProperty("success").GetBoolean().ToString().ToLower();

            var requestString = $"{amountCents}{createdAt}{currency}{errorOccured}{hasParentTransaction}{id}{integrationId}{is3dSecure}{isAuth}{isCapture}{isRefunded}{isStandalonePayment}{isVoided}{orderId}{owner}{pending}{sourceDataPan}{sourceDataSubType}{sourceDataType}{success}";

            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(hmacSecret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(requestString));
            var calculatedHmac = BitConverter.ToString(hash).Replace("-", "").ToLower();

            return calculatedHmac == receivedHmac.ToLower();
        }

        private async Task<JsonElement> PostAsync(string url, object payload)
        {
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseString);
            return doc.RootElement.Clone();
        }
    }
}