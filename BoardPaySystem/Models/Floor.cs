using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace BoardPaySystem.Models
{
    public class Floor
    {
        public Floor()
        {
            FloorName = string.Empty;
            Rooms = new List<Room>();
        }

        [Key]
        public int FloorId { get; set; }

        [Required]
        [StringLength(100)]
        public string FloorName { get; set; }

        [Required]
        public int FloorNumber { get; set; }

        [Required]
        public int BuildingId { get; set; }

        // Navigation properties
        [JsonIgnore]
        [BindNever]
        public Building? Building { get; set; }

        public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();

        public override string ToString()
        {
            return $"{FloorName} (Floor {FloorNumber})";
        }
    }
}