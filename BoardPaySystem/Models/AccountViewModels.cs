using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoardPaySystem.Models
{
    public class RegisterViewModel
    {
        [Required]
        [Display(Name = "Username")]
        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Phone Number")]
        [Phone]
        [StringLength(15)]
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public class CreateTenantViewModel
    {
        [Required(ErrorMessage = "Username is required")]
        [Display(Name = "Username")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at most {1} characters long.", MinimumLength = 3)]
        public required string Username { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at most {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public required string Password { get; set; }

        [Required(ErrorMessage = "First Name is required")]
        [Display(Name = "First Name")]
        public required string FirstName { get; set; }

        [Required(ErrorMessage = "Last Name is required")]
        [Display(Name = "Last Name")]
        public required string LastName { get; set; }

        [Required(ErrorMessage = "Phone Number is required")]
        [Phone]
        [Display(Name = "Phone Number")]
        public required string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Building is required")]
        [Display(Name = "Building")]
        public int BuildingId { get; set; }

        [Required(ErrorMessage = "Floor is required")]
        [Display(Name = "Floor")]
        public int FloorId { get; set; }

        [Required(ErrorMessage = "Room is required")]
        [Display(Name = "Room")]
        public int RoomId { get; set; }

        [Required(ErrorMessage = "Start date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Contract Start Date")]
        public DateTime StartDate { get; set; } = DateTime.Today;

        // This property is maintained for backward compatibility
        [NotMapped]
        public int BillingCycleDay
        {
            get => StartDate.Day;
            set { /* Setter provided for backward compatibility */ }
        }
    }

    public class LoginViewModel
    {
        [Required(ErrorMessage = "Username is required")]
        [Display(Name = "Username")]
        public required string Username { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public required string Password { get; set; }

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }
}