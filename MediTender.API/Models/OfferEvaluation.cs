using System.ComponentModel.DataAnnotations.Schema;

namespace MediTender.API.Models
{
    public class OfferEvaluation
    {
        public int Id { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public DateTime EvaluationDate { get; set; } = DateTime.UtcNow;
        public int TotalScore { get; set; }
        public string FinalDecision { get; set; } = string.Empty;
        public List<EvaluationDetail> Details { get; set; } = new();
        public int TenderId { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }

        public void UpdateFinalDecision()
        {
            TotalScore = Details.Sum(d => d.Score);

            bool hasFailedMandatory = Details.Any(d => d.IsMandatory && (d.Status == "Not Met" || d.Status == "Error"));
            bool hasPartialOrMissingMandatory = Details.Any(d => d.IsMandatory && (d.Status == "Partially Met" || d.Status == "Not Mentioned"));

            if (hasFailedMandatory)
                FinalDecision = "Recommended for Rejection";
            else if (hasPartialOrMissingMandatory)
                FinalDecision = "Pending Manual Review";
            else
                FinalDecision = "Recommended for Acceptance";
        }
    }

    public class EvaluationDetail
    {
        public int Id { get; set; }
        public int OfferEvaluationId { get; set; }
        public string Requirement { get; set; } = string.Empty;
        public bool IsMandatory { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Evidence { get; set; } = string.Empty;
        public int Score { get; set; }
    }
}