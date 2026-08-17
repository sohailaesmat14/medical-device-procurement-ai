namespace MediTender.API.Models
{
    public static class BillingConstants
    {
        public const int FreeTrialQuota = 200;
        public const int MonthlyQuota = 2000;
        public const int AnnualQuota = 24000;

        public const decimal MonthlyPlanPrice = 2500m;
        public const decimal AnnualPlanPrice = 22000m;

        public const int ExtractionCost = 10;
        public const int PerVendorCost = 22;
        public const int AskCost = 2;
    }
}