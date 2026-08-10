namespace MediTender.API.Models
{
    public class PaymentTransaction
    {
        public int Id { get; set; }
        public string OrderId { get; set; } = string.Empty;
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    }
}