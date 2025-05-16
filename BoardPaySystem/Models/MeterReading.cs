using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoardPaySystem.Models
{
    public class MeterReading
    {
        [Key]
        public int ReadingId { get; set; }

        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        public int RoomId { get; set; }

        [Required]
        public DateTime ReadingDate { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal CurrentReading { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? PreviousReading { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal UsageKwh => PreviousReading.HasValue ?
            Math.Max(0, CurrentReading - PreviousReading.Value) : 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal RatePerKwh { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCharge => UsageKwh * RatePerKwh;

        [StringLength(500)]
        public string? Notes { get; set; }

        public int? BillId { get; set; }

        // Navigation properties
        public virtual ApplicationUser Tenant { get; set; } = null!;
        public virtual Room Room { get; set; } = null!;
        public virtual Bill? Bill { get; set; }
    }
}