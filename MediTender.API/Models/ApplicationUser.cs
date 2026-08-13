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
        
        public string Role { get; set; } = "Vendor"; 

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime SubscriptionExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7); 
        public bool IsEmailVerified { get; set; } = false;
        public string VerificationToken { get; set; } = string.Empty;
        public string ResetPasswordToken { get; set; } = string.Empty;
        public DateTime? VerificationTokenExpiration { get; set; }
        public DateTime? ResetPasswordTokenExpiration { get; set; }
    }
}