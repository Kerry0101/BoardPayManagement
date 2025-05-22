using System.ComponentModel.DataAnnotations;

namespace BoardPaySystem.Models
{
    public class SmsTestViewModel
    {
        [Required(ErrorMessage = "Phone number is required")]
        [Display(Name = "Phone Number")]
        [RegularExpression(@"^(\+\d{10,15}|09\d{9})$", ErrorMessage = "Please enter a valid phone number (e.g., +639123456789 or 09123456789)")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Message is required")]
        [Display(Name = "Message")]
        [StringLength(160, ErrorMessage = "Message cannot exceed 160 characters")]
        public string Message { get; set; } = string.Empty;
    }
}
