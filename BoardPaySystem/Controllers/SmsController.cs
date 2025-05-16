using BoardPaySystem.Models;
using BoardPaySystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BoardPaySystem.Controllers
{
    public class SmsController : Controller
    {
        private readonly SmsService _smsService;
        private readonly ILogger<SmsController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public SmsController(
            SmsService smsService,
            ILogger<SmsController> logger,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _smsService = smsService;
            _logger = logger;
            _userManager = userManager;
            _context = context;
        }

        // Utility method to check if tenant has enabled the specific SMS notification type
        public async Task<bool> CanSendSmsNotification(string tenantId, SmsNotificationType type)
        {
            if (string.IsNullOrEmpty(tenantId))
            {
                return false;
            }

            var user = await _userManager.FindByIdAsync(tenantId);
            if (user == null || string.IsNullOrEmpty(user.PhoneNumber) || !user.SmsNotificationsEnabled)
            {
                return false;
            }

            switch (type)
            {
                case SmsNotificationType.NewBill:
                    return user.SmsNewBillsEnabled;
                case SmsNotificationType.DueReminder:
                    return user.SmsDueReminderEnabled;
                case SmsNotificationType.Overdue:
                    return user.SmsOverdueEnabled;
                case SmsNotificationType.PaymentConfirmation:
                    return user.SmsPaymentConfirmationEnabled;
                default:
                    return true;
            }
        }

        // Enum to represent different types of SMS notifications
        public enum SmsNotificationType
        {
            NewBill,
            DueReminder,
            Overdue,
            PaymentConfirmation
        }

        [Authorize(Roles = "Admin,Landlord")]
        public IActionResult Test()
        {
            return View(new SmsTestViewModel());
        }
        [HttpPost]
        public async Task<IActionResult> SendSms(string toPhoneNumber, string messageBody)
        {
            if (string.IsNullOrEmpty(toPhoneNumber) || string.IsNullOrEmpty(messageBody))
            {
                return BadRequest(new { success = false, message = "Phone number and message body are required" });
            }

            // No need to format phone number for Semaphore
            bool success = await _smsService.SendSmsAsync(toPhoneNumber, messageBody);

            if (success)
            {
                return Ok(new { success = true, message = "SMS sent successfully" });
            }
            else
            {
                _logger.LogError("Failed to send SMS to {PhoneNumber}", toPhoneNumber);
                return StatusCode(500, new { success = false, message = "Failed to send SMS" });
            }
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Landlord")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Test(SmsTestViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            // No need to format phone number for Semaphore
            bool success = await _smsService.SendSmsAsync(model.PhoneNumber, model.Message);

            if (success)
            {
                TempData["SuccessMessage"] = $"SMS sent successfully to {model.PhoneNumber}";
            }
            else
            {
                TempData["ErrorMessage"] = $"Failed to send SMS to {model.PhoneNumber}";
            }

            return View(new SmsTestViewModel());
        }
    }
}
