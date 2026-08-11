namespace MediTender.API.Models
{
    public class ApplicationUser
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Plan { get; set; } = "free"; 
        public int QuotaPoints { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime SubscriptionExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7); 
    }
}