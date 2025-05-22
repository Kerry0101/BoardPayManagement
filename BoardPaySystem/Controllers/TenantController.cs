using System.Security.Claims;
using BoardPaySystem.Models;
using BoardPaySystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoardPaySystem.Controllers
{
    [Authorize(Roles = "Tenant")]
    public class TenantController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly INotificationService _notificationService;

        public TenantController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            INotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
        }

        // GET: Tenant Dashboard (redirect to Bills)
        public IActionResult Index()
        {
            return RedirectToAction("Bills");
        }

        // GET: Tenant/Bills
        public async Task<IActionResult> Bills()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var bills = await _context.Bills
                .Where(b => b.TenantId == userId && b.IsApproved)
                .OrderByDescending(b => b.BillingDate)
                .ToListAsync();

            // Get all meter readings linked to these bills
            var billIds = bills.Select(b => b.BillId).ToList();
            var readings = await _context.MeterReadings
                .Where(m => m.BillId != null && billIds.Contains(m.BillId.Value))
                .ToListAsync();

            // Dictionary for quick lookup in the view
            ViewBag.BillReadings = readings.ToDictionary(r => r.BillId.Value, r => r);

            return View(bills);
        }

        // GET: Tenant/BillDetails/{id}
        public async Task<IActionResult> BillDetails(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var bill = await _context.Bills
                .Include(b => b.Room)
                    .ThenInclude(r => r.Floor)
                        .ThenInclude(f => f.Building)
                .FirstOrDefaultAsync(b => b.BillId == id && b.TenantId == userId && b.IsApproved);
            if (bill == null)
            {
                return NotFound();
            }

            // Overdue/late fee logic (copied from landlord controller)
            bool isPastDueDate = bill.DueDate < DateTime.Now.Date;
            bool isUnpaid = bill.Status == BillStatus.NotPaid || bill.Status == BillStatus.Pending;
            ViewBag.IsPastDueDate = isPastDueDate && isUnpaid;

            if ((bill.Status == BillStatus.Overdue || (isPastDueDate && isUnpaid)) && bill.Room?.Floor?.Building != null)
            {
                decimal lateFeePercentage = bill.Room.Floor.Building.LateFee;
                decimal adjustedRent = bill.MonthlyRent * (1 + lateFeePercentage / 100);
                decimal adjustedWaterFee = bill.WaterFee * (1 + lateFeePercentage / 100);
                decimal adjustedElectricityFee = bill.ElectricityFee * (1 + lateFeePercentage / 100);
                decimal adjustedWifiFee = bill.WifiFee * (1 + lateFeePercentage / 100);
                decimal totalLateFee = (adjustedRent - bill.MonthlyRent) +
                                      (adjustedWaterFee - bill.WaterFee) +
                                      (adjustedElectricityFee - bill.ElectricityFee) +
                                      (adjustedWifiFee - bill.WifiFee);
                TimeSpan daysLate = DateTime.Now.Date - bill.DueDate.Date;
                ViewBag.IsOverdue = true;
                ViewBag.DaysOverdue = daysLate.Days;
                ViewBag.LateFeePercentage = lateFeePercentage;
                ViewBag.AdjustedRent = adjustedRent;
                ViewBag.AdjustedWaterFee = adjustedWaterFee;
                ViewBag.AdjustedElectricityFee = adjustedElectricityFee;
                ViewBag.AdjustedWifiFee = adjustedWifiFee;
                ViewBag.TotalLateFee = totalLateFee;
                ViewBag.AdjustedTotal = bill.TotalAmount + totalLateFee - (bill.LateFee ?? 0);
            }
            else
            {
                ViewBag.IsOverdue = false;
            }

            return View(bill);
        }

        // GET: Tenant/Payments
        public async Task<IActionResult> Payments()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var payments = await _context.Bills
                .Where(b => b.TenantId == userId && b.Status == BillStatus.Paid)
                .Include(b => b.Room)
                .OrderByDescending(b => b.PaymentDate)
                .ToListAsync();

            return View(payments);
        }

        // GET: Tenant/History
        public async Task<IActionResult> History()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var readings = await _context.MeterReadings
                .Where(m => m.TenantId == userId)
                .OrderByDescending(m => m.ReadingDate)
                .ToListAsync();
            return View(readings);
        }

        // GET: Tenant/Profile
        public async Task<IActionResult> Profile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound("User not found");
            }

            // Get tenant's current room
            var room = await _context.Rooms
                .Include(r => r.Floor)
                .ThenInclude(f => f.Building)
                .FirstOrDefaultAsync(r => r.TenantId == userId);

            ViewBag.Room = room;

            return View(user);
        }

        // GET: Tenant/Payment/{id}
        public async Task<IActionResult> Payment(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var bill = await _context.Bills.FirstOrDefaultAsync(b => b.BillId == id && b.TenantId == userId && b.IsApproved);
            if (bill == null)
            {
                TempData["Error"] = "Bill not found or not yet approved.";
                return RedirectToAction("Bills");
            }
            return View(bill);
        }

        // POST: Tenant/InitiatePayment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InitiatePayment(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var bill = await _context.Bills
                .FirstOrDefaultAsync(b => b.BillId == id && b.TenantId == userId);
            if (bill == null)
            {
                return NotFound();
            }
            // Mark the bill as pending payment
            bill.Status = BillStatus.Pending;
            bill.PaymentReference = "Cash-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            await _context.SaveChangesAsync();
            // Redirect to confirmation page or payment page
            return RedirectToAction(nameof(Payment), new { id = bill.BillId });
        }

        // POST: Tenant/ConfirmPayment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(int id, string referenceNumber, string paymentMethod)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var bill = await _context.Bills
                .FirstOrDefaultAsync(b => b.BillId == id && b.TenantId == userId && b.Status == BillStatus.Pending);

            if (bill == null)
            {
                return NotFound();
            }

            // Validate reference number
            if (string.IsNullOrEmpty(referenceNumber))
            {
                ModelState.AddModelError("ReferenceNumber", "Payment reference number is required.");
                return RedirectToAction(nameof(Payment), new { id = bill.BillId });
            }

            // Update payment information
            bill.Status = BillStatus.Paid;
            bill.PaymentDate = DateTime.Now;
            bill.PaymentReference = referenceNumber;
            bill.PaymentMethod = paymentMethod;

            // Create a payment record
            var payment = new Payment
            {
                BillId = bill.BillId,
                TenantId = userId,
                Amount = bill.Amount,
                PaymentDate = DateTime.Now,
                PaymentMethod = paymentMethod,
                ReferenceNumber = referenceNumber
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Payment confirmed successfully!";
            return RedirectToAction(nameof(Payments));
        }

        // POST: Tenant/CancelPayment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelPayment(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var bill = await _context.Bills
                .FirstOrDefaultAsync(b => b.BillId == id && b.TenantId == userId && b.Status == BillStatus.Pending);

            if (bill == null)
            {
                return NotFound();
            }

            // Reset to unpaid status
            bill.Status = BillStatus.NotPaid;
            bill.PaymentReference = null;

            await _context.SaveChangesAsync();

            TempData["InfoMessage"] = "Payment process has been cancelled.";
            return RedirectToAction(nameof(Bills));
        }

        // GET: Tenant/Overview
        public IActionResult Overview()
        {
            return RedirectToAction("Index");
        }

        // GET: Tenant/Notifications
        public async Task<IActionResult> Notifications()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var notifications = await _notificationService.GetUserNotificationsAsync(userId, includeRead: true);
            return View(notifications);
        }

        // POST: Tenant/MarkNotificationAsRead
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkNotificationAsRead(int id)
        {
            await _notificationService.MarkAsReadAsync(id);
            return RedirectToAction(nameof(Notifications));
        }

        // POST: Tenant/MarkAllNotificationsAsRead
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllNotificationsAsRead()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            await _notificationService.MarkAllAsReadAsync(userId);
            return RedirectToAction(nameof(Notifications));
        }

        // GET: Tenant/GetUnreadNotificationCount
        [HttpGet]
        public async Task<IActionResult> GetUnreadNotificationCount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var count = await _notificationService.GetUnreadCountAsync(userId);
            return Json(new { count });
        }

        // GET: Tenant/SmsPreferences
        public async Task<IActionResult> SmsPreferences()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // POST: Tenant/SaveSmsPreferences
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSmsPreferences(
            bool SmsNotificationsEnabled,
            bool SmsNewBillsEnabled,
            bool SmsDueReminderEnabled,
            bool SmsOverdueEnabled,
            bool SmsPaymentConfirmationEnabled,
            string PhoneNumber)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound();
            }

            // Update user's SMS preferences
            user.SmsNotificationsEnabled = SmsNotificationsEnabled;
            user.SmsNewBillsEnabled = SmsNewBillsEnabled;
            user.SmsDueReminderEnabled = SmsDueReminderEnabled;
            user.SmsOverdueEnabled = SmsOverdueEnabled;
            user.SmsPaymentConfirmationEnabled = SmsPaymentConfirmationEnabled;

            // Update phone number if provided
            if (!string.IsNullOrWhiteSpace(PhoneNumber))
            {
                user.PhoneNumber = PhoneNumber;
            }

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "SMS notification preferences updated successfully!";
            }
            else
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return RedirectToAction(nameof(SmsPreferences));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(currentPassword) || string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                return Json(new { success = false, message = "All fields are required." });
            }
            if (newPassword != confirmPassword)
            {
                return Json(new { success = false, message = "New password and confirmation do not match." });
            }
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found." });
            }
            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (result.Succeeded)
            {
                return Json(new { success = true, message = "Password updated successfully." });
            }
            else
            {
                var errorMsg = result.Errors.FirstOrDefault()?.Description ?? "Failed to update password.";
                return Json(new { success = false, message = errorMsg });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string firstName, string lastName, string phoneNumber, string userName)
        {
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(userName))
            {
                return Json(new { success = false, message = "All fields are required." });
            }
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found." });
            }
            user.FirstName = firstName;
            user.LastName = lastName;
            user.PhoneNumber = phoneNumber;
            user.UserName = userName;
            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                return Json(new { success = true, message = "Profile updated successfully." });
            }
            else
            {
                var errorMsg = result.Errors.FirstOrDefault()?.Description ?? "Failed to update profile.";
                return Json(new { success = false, message = errorMsg });
            }
        }
    }
}

