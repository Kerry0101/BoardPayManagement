namespace BoardPaySystem.Models
{
    public class MonthlyIncomeSummaryViewModel
    {
        public List<MonthlyIncomeRow> Rows { get; set; } = new();
        public decimal TotalBilled { get; set; }
        public decimal TotalCollected { get; set; }
        public decimal TotalOutstanding { get; set; }
        public decimal TotalOverdue { get; set; }
        public decimal TotalWrittenOff { get; set; }
        public decimal TotalIncome { get; set; }
        public int Year { get; set; }
        public int? BuildingId { get; set; }
    }

    public class MonthlyIncomeRow
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal Billed { get; set; }
        public decimal Collected { get; set; }
        public decimal Outstanding { get; set; }
        public decimal Overdue { get; set; }
        public decimal WrittenOff { get; set; }
    }
}