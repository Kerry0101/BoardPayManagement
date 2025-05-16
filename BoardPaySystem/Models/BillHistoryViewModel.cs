namespace BoardPaySystem.Models
{
    public class BillHistoryViewModel
    {
        public string? TenantId { get; set; }
        public List<ApplicationUser> Tenants { get; set; } = new();
        public List<BillHistoryRow> Bills { get; set; } = new();
        public int? Year { get; set; }
    }

    public class BillHistoryRow
    {
        public string BillingPeriod { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public decimal Rent { get; set; }
        public decimal Water { get; set; }
        public decimal Electricity { get; set; }
        public decimal Wifi { get; set; }
        public decimal LateFee { get; set; }
        public decimal Total { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? PaymentDate { get; set; }
        public string? Reference { get; set; }
        public string? Notes { get; set; }
    }
}