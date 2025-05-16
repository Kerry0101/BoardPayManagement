using System.ComponentModel.DataAnnotations;

namespace BoardPaySystem.Models
{
    public class SmsTestViewModel
    {
        [Required(ErrorMessage = "Phone number is required")]
        [Display(Name = "Phone Number")]
        [RegularExpression(@"^\+?[1-9]\d{1,14}$", ErrorMessage = "Please enter a valid phone number with country code (e.g., +1234567890)")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Message is required")]
        [Display(Name = "Message")]
        [StringLength(160, ErrorMessage = "Message cannot exceed 160 characters")]
        public string Message { get; set; } = string.Empty;
    }
}
