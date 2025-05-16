using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace BoardPaySystem.Models
{
    public class ApplicationUser : IdentityUser
    {
        public ApplicationUser()
        {
            FirstName = string.Empty;
            LastName = string.Empty;
        }

        [Required]
        [StringLength(50)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(50)]
        public string LastName { get; set; }

        // Optional fields for tenants
        public int? BuildingId { get; set; }
        public int? RoomId { get; set; }

        // Changed from Required to NotMapped to make it compatible with the existing database
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        // This property is maintained for backward compatibility
        // It's calculated from StartDate and used by billing service
        [Range(1, 31)]
        [NotMapped]
        public int BillingCycleDay
        {
            get => StartDate.Day;
            set { /* Setter provided for backward compatibility */ }
        }

        // Indicates if the tenant is archived (left the property)

        public bool IsApproved { get; set; }
        public bool IsArchived { get; set; } = false;

        // SMS notification preferences
        public bool SmsNotificationsEnabled { get; set; } = true;
        public bool SmsNewBillsEnabled { get; set; } = true;
        public bool SmsDueReminderEnabled { get; set; } = true;
        public bool SmsOverdueEnabled { get; set; } = true;
        public bool SmsPaymentConfirmationEnabled { get; set; } = true;

        // Navigation properties
        public virtual Building? Building { get; set; }
        public virtual Room? CurrentRoom { get; set; }
    }
}
