namespace MediTender.API.Models
{
    public class TenderInteraction
    {
        public int Id { get; set; }
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int TenderId { get; set; }
        public string VendorName { get; set; } = string.Empty;

        public Tender? Tender { get; set; }
    }
}