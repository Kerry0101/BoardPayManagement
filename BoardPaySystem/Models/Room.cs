using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace BoardPaySystem.Models
{
    public class Room
    {
        public Room()
        {
            RoomNumber = string.Empty;
            Description = string.Empty;
        }

        [Key]
        public int RoomId { get; set; }

        [Required]
        [StringLength(20)]
        public string RoomNumber { get; set; }

        [StringLength(100)]
        public string? Description { get; set; }

        public bool IsOccupied { get; set; }

        public int FloorId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CustomMonthlyRent { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CustomWaterFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CustomElectricityFee { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CustomWifiFee { get; set; }

        public string? TenantId { get; set; }

        // Computed property for rent amount
        [NotMapped]
        public decimal RentAmount => CustomMonthlyRent ?? 0;

        // Navigation properties
        [BindNever]
        public virtual Floor? Floor { get; set; }

        public virtual ApplicationUser? CurrentTenant { get; set; }

        public override string ToString()
        {
            return $"Room {RoomNumber}";
        }
    }
}