using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace BoardPaySystem.Models
{
    public class Building
    {
        public Building()
        {
            BuildingName = string.Empty;
            Address = string.Empty;
            Floors = new List<Floor>();
            Tenants = new List<ApplicationUser>();
        }

        [Key]
        public int BuildingId { get; set; }

        [Required]
        [StringLength(50)]
        public string BuildingName { get; set; }

        [Required]
        [StringLength(100)]
        public string Address { get; set; }

        // Default fees for the building
        [Required]
        [Column(TypeName = "decimal(10, 2)")]
        public decimal DefaultMonthlyRent { get; set; }

        [Required]
        [Column(TypeName = "decimal(10, 2)")]
        public decimal DefaultWaterFee { get; set; }

        [Required]
        [Column(TypeName = "decimal(10, 2)")]
        public decimal DefaultElectricityFee { get; set; }

        [Required]
        [Column(TypeName = "decimal(10, 2)")]
        public decimal DefaultWifiFee { get; set; }

        [Required]
        [Column(TypeName = "decimal(10, 2)")]
        [Range(0, 100, ErrorMessage = "Late fee percentage must be between 0 and 100")]
        [Display(Name = "Late Payment Fee (%)")]
        public decimal LateFee { get; set; }

        // Navigation properties
        [JsonIgnore]
        public virtual ICollection<Floor> Floors { get; set; }

        [JsonIgnore]
        public virtual ICollection<ApplicationUser> Tenants { get; set; }
    }
}