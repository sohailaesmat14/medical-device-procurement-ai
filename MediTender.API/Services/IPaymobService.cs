namespace MediTender.API.Services
{
    public interface IPaymobService
    {
        Task<string> GetPaymentIframeUrlAsync(decimal amount, string userEmail, string planType);
        bool VerifyHmac(string hmac, string jsonPayload);
    }
}