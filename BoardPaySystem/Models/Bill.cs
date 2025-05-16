using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoardPaySystem.Models
{
    public enum BillStatus
    {
        NotPaid,    // New status: Bill is created but tenant hasn't initiated payment
        Pending,    // Payment initiated (via GCash) but not yet approved
        Paid,       // Payment completed and confirmed
        Overdue,    // Payment past due date
        Cancelled,  // Bill cancelled
        WrittenOff  // Bill written off/forgiven by landlord
    }

    public class Bill
    {
        [Key]
        public int BillId { get; set; }

        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        public int RoomId { get; set; }

        [Required]
        public DateTime BillingDate { get; set; }

        // Explicitly store billing month and year for easier queries
        [Required]
        public int BillingMonth { get; set; }

        [Required]
        public int BillingYear { get; set; }

        [Required]
        public DateTime DueDate { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthlyRent { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal WaterFee { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ElectricityFee { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal WifiFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? LateFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? OtherFees { get; set; }

        [StringLength(200)]
        public string? OtherFeesDescription { get; set; }

        [Required]
        public BillStatus Status { get; set; }

        [Required]
        public bool IsApproved { get; set; } = false;

        public DateTime? PaymentDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? AmountPaid { get; set; }

        [StringLength(200)]
        public string? PaymentReference { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(50)]
        public string? PaymentMethod { get; set; }

        // Navigation properties
        public virtual ApplicationUser Tenant { get; set; } = null!;
        public virtual Room Room { get; set; } = null!;

        [NotMapped]
        public decimal TotalAmount => MonthlyRent + WaterFee + ElectricityFee + WifiFee + (LateFee ?? 0) + (OtherFees ?? 0);

        // Aliases for properties used in views and controllers
        [NotMapped]
        public decimal Amount => TotalAmount;

        [NotMapped]
        public decimal WaterCharge => WaterFee;

        [NotMapped]
        public decimal ElectricityCharge => ElectricityFee;

        [NotMapped]
        public decimal OtherCharges => OtherFees ?? 0;

        [NotMapped]
        public string? BillNotes => Notes;

        // Display property for the view
        [NotMapped]
        public string BillingPeriod => $"{new DateTime(BillingYear, BillingMonth, 1):MMMM yyyy}";
    }
}