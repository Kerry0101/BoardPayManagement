using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BoardPaySystem.Models
{
    public enum NotificationType
    {
        NewBill,
        UpcomingDue,
        Overdue,
        PaymentConfirmed,
        BillModified,
        General
    }

    public class Notification
    {
        [Key]
        public int NotificationId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Message { get; set; } = string.Empty;

        [Required]
        public NotificationType Type { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsRead { get; set; } = false;

        public string? ActionLink { get; set; }

        // Optional reference to related bill
        public int? BillId { get; set; }

        // Navigation properties
        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; } = null!;

        [ForeignKey("BillId")]
        public virtual Bill? Bill { get; set; }
    }
}