using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoardPaySystem.Models
{
    public class Payment
    {
        [Key]
        public int PaymentId { get; set; }

        [Required]
        public int BillId { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Required]
        public DateTime PaymentDate { get; set; }

        [Required]
        [StringLength(50)]
        public string PaymentMethod { get; set; } = string.Empty;

        [StringLength(100)]
        public string? ReferenceNumber { get; set; }

        [StringLength(255)]
        public string? ProofUrl { get; set; }

        [StringLength(255)]
        public string? Notes { get; set; }

        // Adding TenantId property
        public string? TenantId { get; set; }

        // Navigation property
        public virtual Bill Bill { get; set; } = null!;

        [ForeignKey("TenantId")]
        public virtual ApplicationUser? Tenant { get; set; }
    }
}