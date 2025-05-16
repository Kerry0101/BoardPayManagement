namespace BoardPaySystem.Models
{
    public class PaymentHistoryViewModel
    {
        public List<PaymentHistoryRow> Payments { get; set; } = new();
        public int? Year { get; set; }
        public int? BuildingId { get; set; }
        public string? TenantId { get; set; }
        public List<Building> Buildings { get; set; } = new();
        public List<ApplicationUser> Tenants { get; set; } = new();
    }

    public class PaymentHistoryRow
    {
        public DateTime PaymentDate { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string RoomName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public string BillPeriod { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}