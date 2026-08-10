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

            // 1. Authentication Request
            var authPayload = new { api_key = apiKey };
            var authResponse = await PostAsync($"{_baseUrl}/auth/tokens", authPayload);
            var authToken = authResponse.GetProperty("token").GetString();

            // 2. Order Registration Request (Amount in Cents)
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

            // 3. Payment Key Request
            var paymentKeyPayload = new
            {
                auth_token = authToken,
                amount_cents = (amount * 100).ToString("0"),
                expiration = 3600,
                order_id = orderId,
                billing_data = new
                {
                    apartment = "NA", email = userEmail, floor = "NA", first_name = "User",
                    street = "NA", building = "NA", phone_number = "01000000000",
                    shipping_method = "NA", postal_code = "NA", city = "Alexandria",
                    country = "EG", last_name = "Account", state = "NA"
                },
                currency = "EGP",
                integration_id = int.Parse(integrationId)
            };
            var paymentKeyResponse = await PostAsync($"{_baseUrl}/acceptance/payment_keys", paymentKeyPayload);
            var paymentToken = paymentKeyResponse.GetProperty("token").GetString();

            // 4. Return Iframe URL
            return $"https://accept.paymob.com/api/acceptance/iframes/{iframeId}?payment_token={paymentToken}";
        }

        public bool VerifyHmac(string receivedHmac, string jsonPayload)
        {

            var hmacSecret = _configuration["Paymob:HmacSecret"];
            return true; 
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