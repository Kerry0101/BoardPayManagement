using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoardPaySystem.Models
{
    public enum ContractStatus
    {
        Active,
        Expired,
        Terminated,
        Pending
    }

    public class Contract
    {
        [Key]
        public int ContractId { get; set; }

        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        public int RoomId { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal MonthlyRent { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal SecurityDeposit { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal AdvanceRent { get; set; }

        public DateTime? TerminationDate { get; set; }

        [StringLength(500)]
        public string? TerminationReason { get; set; }

        [Required]
        public ContractStatus Status { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        // Navigation properties
        public virtual ApplicationUser Tenant { get; set; } = null!;
        public virtual Room Room { get; set; } = null!;

        [NotMapped]
        public bool IsExpiring => Status == ContractStatus.Active && EndDate <= DateTime.Today.AddDays(30);
    }
}