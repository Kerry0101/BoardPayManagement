using BoardPaySystem.Models;
using BoardPaySystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoardPaySystem.Controllers
{
    [Authorize(Roles = "Landlord")]
    public class BillingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IBillingService _billingService;
        private readonly INotificationService _notificationService;
        private readonly SmsService _smsService;
        private readonly ILogger<BillingController> _logger;

        public BillingController(
            ApplicationDbContext context,
            IBillingService billingService,
            INotificationService notificationService,
            SmsService smsService,
            ILogger<BillingController> logger)
        {
            _context = context;
            _billingService = billingService;
            _notificationService = notificationService;
            _smsService = smsService;
            _logger = logger;
        }

        // GET: /Billing/Index
        public IActionResult Index()
        {
            return RedirectToAction("Billing", "Landlord");
        }

        // GET: /Billing/Bills
        public IActionResult Bills()
        {
            // Redirect to the consolidated Landlord/Billing view
            return RedirectToAction("Billing", "Landlord");
        }

        // GET: /Billing/BillDetails/5
        public async Task<IActionResult> BillDetails(int id)
        {
            var bill = await _context.Bills
                .Include(b => b.Tenant)
                .Include(b => b.Room)
                    .ThenInclude(r => r.Floor)
                        .ThenInclude(f => f != null ? f.Building : null)
                .FirstOrDefaultAsync(b => b.BillId == id);

            if (bill == null)
            {
                return NotFound();
            }

            var payments = await _context.Payments
                .Where(p => p.BillId == id)
                .OrderBy(p => p.PaymentDate)
                .ToListAsync();
            ViewBag.Payments = payments;

            // Check if bill is actually overdue based on due date, regardless of its current status
            bool isPastDueDate = bill.DueDate < DateTime.Now.Date;
            bool isUnpaid = bill.Status == BillStatus.NotPaid || bill.Status == BillStatus.Pending;
            ViewBag.IsPastDueDate = isPastDueDate && isUnpaid;

            // Calculate overdue amounts if bill is overdue or should be overdue
            if ((bill.Status == BillStatus.Overdue || (isPastDueDate && isUnpaid)) && bill.Room?.Floor?.Building != null)
            {
                decimal lateFeePercentage = bill.Room.Floor.Building.LateFee;

                // Calculate late fee adjustments for each charge
                decimal adjustedRent = bill.MonthlyRent * (1 + lateFeePercentage / 100);
                decimal adjustedWaterFee = bill.WaterFee * (1 + lateFeePercentage / 100);
                decimal adjustedElectricityFee = bill.ElectricityFee * (1 + lateFeePercentage / 100);
                decimal adjustedWifiFee = bill.WifiFee * (1 + lateFeePercentage / 100);

                // Calculate the total late fee (difference between original and adjusted amounts)
                decimal totalLateFee = (adjustedRent - bill.MonthlyRent) +
                                      (adjustedWaterFee - bill.WaterFee) +
                                      (adjustedElectricityFee - bill.ElectricityFee) +
                                      (adjustedWifiFee - bill.WifiFee);

                // Calculate days overdue
                TimeSpan daysLate = DateTime.Now.Date - bill.DueDate.Date;

                // Put the adjusted values in ViewBag
                ViewBag.IsOverdue = true;
                ViewBag.DaysOverdue = daysLate.Days;
                ViewBag.LateFeePercentage = lateFeePercentage;
                ViewBag.AdjustedRent = adjustedRent;
                ViewBag.AdjustedWaterFee = adjustedWaterFee;
                ViewBag.AdjustedElectricityFee = adjustedElectricityFee;
                ViewBag.AdjustedWifiFee = adjustedWifiFee;
                ViewBag.TotalLateFee = totalLateFee;
                ViewBag.AdjustedTotal = bill.TotalAmount + totalLateFee - (bill.LateFee ?? 0); // To avoid double counting
            }
            else
            {
                ViewBag.IsOverdue = false;
            }

            return View(bill);
        }

        // GET: /Billing/CreateBill
        public async Task<IActionResult> CreateBill()
        {
            ViewBag.Tenants = await _context.Users.Where(u => u.RoomId != null).ToListAsync();
            var rooms = await _context.Rooms.Include(r => r.Floor).ThenInclude(f => f.Building).ToListAsync();
            ViewBag.Rooms = rooms;
            // Build a dictionary of fees per room
            var roomFees = rooms.ToDictionary(
                r => r.RoomId,
                r => new Dictionary<string, decimal>
                {
                    { "MonthlyRent", r.CustomMonthlyRent ?? r.Floor?.Building?.DefaultMonthlyRent ?? 0 },
                    { "WaterFee", r.CustomWaterFee ?? r.Floor?.Building?.DefaultWaterFee ?? 0 },
                    { "ElectricityFee", r.CustomElectricityFee ?? r.Floor?.Building?.DefaultElectricityFee ?? 0 },
                    { "WifiFee", r.CustomWifiFee ?? r.Floor?.Building?.DefaultWifiFee ?? 0 }
                }
            );
            ViewBag.RoomFees = roomFees;
            return View();
        }

        // POST: /Billing/CreateBill
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBill(Bill bill)
        {
            if (ModelState.IsValid)
            {
                // Set BillingMonth and BillingYear from BillingDate
                if (bill.BillingDate != default)
                {
                    bill.BillingMonth = bill.BillingDate.Month;
                    bill.BillingYear = bill.BillingDate.Year;
                }
                _context.Bills.Add(bill);
                await _context.SaveChangesAsync();
                return RedirectToAction("Billing", "Landlord");
            }
            ViewBag.Tenants = await _context.Users.Where(u => u.RoomId != null).ToListAsync();
            ViewBag.Rooms = await _context.Rooms.ToListAsync();
            return View(bill);
        }

        // GET: /Billing/GenerateBills
        public IActionResult GenerateBills()
        {
            return View();
        }

        // POST: /Billing/GenerateBills
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateBills(DateTime? billingDate)
        {
            DateTime effectiveBillingDate = billingDate ?? DateTime.Now;

            try
            {
                int billsGenerated = await _billingService.GenerateMonthlyBillsAsync(effectiveBillingDate);
                TempData["SuccessMessage"] = $"Successfully generated {billsGenerated} bills.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error generating bills: {ex.Message}";
            }

            return RedirectToAction("Billing", "Landlord");
        }

        // GET: /Billing/RecordPayment/5
        public async Task<IActionResult> RecordPayment(int id)
        {
            var bill = await _context.Bills
                .Include(b => b.Tenant)
                .Include(b => b.Room)
                .FirstOrDefaultAsync(b => b.BillId == id);

            if (bill == null)
            {
                return NotFound();
            }

            return View(bill);
        }

        // POST: /Billing/RecordPayment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordPayment(int billId, decimal amount, DateTime paymentDate, string paymentMethod, string paymentReference, string? notes = null)
        {
            if (amount <= 0)
            {
                ModelState.AddModelError("", "Payment amount must be greater than zero.");
                var bill = await _context.Bills
                    .Include(b => b.Tenant)
                    .Include(b => b.Room)
                    .FirstOrDefaultAsync(b => b.BillId == billId);
                if (bill == null)
                {
                    return NotFound();
                }
                return View(bill);
            }

            try
            {
                var bill = await _context.Bills
                    .Include(b => b.Tenant)
                    .FirstOrDefaultAsync(b => b.BillId == billId);

                if (bill == null)
                {
                    TempData["ErrorMessage"] = "Bill not found.";
                    return RedirectToAction("Bills");
                }

                // Create new payment record
                var payment = new Payment
                {
                    BillId = billId,
                    TenantId = bill.TenantId,
                    Amount = amount,
                    PaymentDate = paymentDate,
                    PaymentMethod = paymentMethod,
                    ReferenceNumber = paymentReference,
                    Notes = notes
                };

                _context.Payments.Add(payment);

                // Update bill information
                decimal totalPaid = amount;
                if (bill.AmountPaid.HasValue)
                {
                    totalPaid += bill.AmountPaid.Value;
                }

                bill.AmountPaid = totalPaid;
                bill.PaymentDate = paymentDate;
                bill.PaymentReference = paymentReference;

                // Update bill status based on amount paid
                if (totalPaid >= bill.TotalAmount)
                {
                    bill.Status = BillStatus.Paid;
                    // Create payment confirmation notification
                    await _notificationService.CreateBillNotificationAsync(bill, NotificationType.PaymentConfirmed);

                    // Send SMS notification for payment confirmation
                    if (bill.Tenant != null &&
                        !string.IsNullOrEmpty(bill.Tenant.PhoneNumber) &&
                        bill.Tenant.SmsNotificationsEnabled &&
                        bill.Tenant.SmsPaymentConfirmationEnabled)
                    {
                        try
                        {
                            var firstName = bill.Tenant.FirstName;
                            var message = SmsMessageTemplates.PaymentConfirmed(bill, firstName, amount);

                            bool smsSent = await _smsService.SendSmsAsync(bill.Tenant.PhoneNumber, message);

                            if (smsSent)
                            {
                                _logger.LogInformation("Payment confirmation SMS sent to {TenantName} at {PhoneNumber}",
                                    $"{bill.Tenant.FirstName} {bill.Tenant.LastName}", bill.Tenant.PhoneNumber);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to send payment confirmation SMS to tenant {TenantId}", bill.TenantId);
                            // Don't rethrow - continue with payment process
                        }
                    }
                }
                else if (totalPaid > 0)
                {
                    bill.Status = BillStatus.Pending;
                }

                _context.Update(bill);
                await _context.SaveChangesAsync();

                string tenantName = bill.Tenant != null ? $"{bill.Tenant.FirstName} {bill.Tenant.LastName}" : "Unknown tenant";
                TempData["SuccessMessage"] = $"Payment of {amount:C} for {tenantName} recorded successfully.";
                return RedirectToAction("BillDetails", new { id = billId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error recording payment: {ex.Message}";
                return RedirectToAction("RecordPayment", new { id = billId });
            }
        }

        // POST: /Billing/UpdateBillStatuses
        [HttpPost]
        public async Task<IActionResult> UpdateBillStatuses()
        {
            await _billingService.UpdateBillStatusesAsync();
            TempData["SuccessMessage"] = "Bill statuses updated successfully.";
            return RedirectToAction("Billing", "Landlord");
        }

        // GET: /Billing/GenerateInitialBill
        public async Task<IActionResult> GenerateInitialBill(string tenantId)
        {
            // Get the tenant with room and building information
            var tenant = await _context.Users
                .Include(u => u.CurrentRoom!)
                    .ThenInclude(r => r.Floor!)
                        .ThenInclude(f => f.Building!)
                .FirstOrDefaultAsync(u => u.Id == tenantId);

            if (tenant == null || tenant.CurrentRoom == null || tenant.CurrentRoom.Floor == null || tenant.CurrentRoom.Floor.Building == null)
            {
                return NotFound("Tenant, room, or building information not found.");
            }

            var building = tenant.CurrentRoom.Floor.Building;
            var today = DateTime.Today;
            var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);

            // Create the bill with proper property names
            var bill = new Bill
            {
                TenantId = tenantId,
                RoomId = tenant.CurrentRoom.RoomId,
                BillingDate = firstDayOfMonth,
                DueDate = new DateTime(today.Year, today.Month, tenant.BillingCycleDay),
                MonthlyRent = tenant.CurrentRoom.CustomMonthlyRent ?? building.DefaultMonthlyRent,
                WaterFee = tenant.CurrentRoom.CustomWaterFee ?? building.DefaultWaterFee,
                ElectricityFee = 0, // Will be calculated based on meter readings
                WifiFee = tenant.CurrentRoom.CustomWifiFee ?? building.DefaultWifiFee,
                Status = BillStatus.Pending,
                Notes = "Initial bill generated on " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            // TotalAmount is calculated automatically

            _context.Bills.Add(bill);
            await _context.SaveChangesAsync();

            return RedirectToAction("Bills", "Tenant");
        }

        // POST: /Billing/ApprovePayment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApprovePayment(int billId, decimal amount)
        {
            var bill = await _context.Bills
                .Include(b => b.Tenant)
                .FirstOrDefaultAsync(b => b.BillId == billId);

            if (bill == null)
            {
                TempData["ErrorMessage"] = "Bill not found.";
                return RedirectToAction("Bills");
            }

            if (bill.Status != BillStatus.Pending)
            {
                TempData["ErrorMessage"] = "Only pending payments can be approved.";
                return RedirectToAction("BillDetails", new { id = billId });
            }

            // Update the bill status
            bill.Status = BillStatus.Paid;
            bill.PaymentDate = DateTime.Now;
            bill.AmountPaid = amount;

            // Record the payment
            var payment = new Payment
            {
                BillId = billId,
                TenantId = bill.TenantId,
                Amount = amount,
                PaymentDate = DateTime.Now,
                PaymentMethod = "Cash",
                ReferenceNumber = bill.PaymentReference ?? "Approved by landlord",
                Notes = $"Cash payment approved by landlord on {DateTime.Now}"
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            // Create payment confirmation notification
            await _notificationService.CreateBillNotificationAsync(bill, NotificationType.PaymentConfirmed);

            string tenantName = "tenant";
            if (bill.Tenant != null)
            {
                tenantName = $"{bill.Tenant.FirstName} {bill.Tenant.LastName}";
            }

            TempData["SuccessMessage"] = $"Cash payment for {tenantName} has been approved.";
            return RedirectToAction("BillDetails", new { id = billId });
        }

        // POST: /Billing/BatchRecordPayment
        [HttpPost]
        public async Task<IActionResult> BatchRecordPayment(string billIds, DateTime paymentDate, string paymentMethod, string? paymentReference = null, string? notes = null)
        {
            if (string.IsNullOrWhiteSpace(billIds))
            {
                return Json(new { success = false, message = "No bills selected" });
            }

            try
            {
                // Split the comma-separated string of bill IDs
                var billIdArray = billIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
                var processedCount = 0;

                foreach (var idStr in billIdArray)
                {
                    if (int.TryParse(idStr, out int billId))
                    {
                        var bill = await _context.Bills
                            .Include(b => b.Tenant)
                            .FirstOrDefaultAsync(b => b.BillId == billId);

                        if (bill == null)
                        {
                            continue; // Skip non-existent bills
                        }

                        // Skip already paid bills
                        if (bill.Status == BillStatus.Paid)
                        {
                            continue;
                        }

                        // Mark the bill as paid
                        bill.Status = BillStatus.Paid;
                        bill.PaymentDate = paymentDate;
                        bill.PaymentMethod = paymentMethod;
                        bill.PaymentReference = paymentReference;
                        bill.Notes = string.IsNullOrEmpty(bill.Notes)
                            ? notes
                            : $"{bill.Notes}\nPayment notes: {notes}";
                        bill.AmountPaid = bill.TotalAmount;

                        // Create payment record
                        var payment = new Payment
                        {
                            BillId = billId,
                            Amount = bill.TotalAmount,
                            PaymentDate = paymentDate,
                            PaymentMethod = paymentMethod,
                            ReferenceNumber = paymentReference,
                            Notes = notes
                        };

                        _context.Payments.Add(payment);

                        // Create notification for tenant
                        await _notificationService.CreateBillNotificationAsync(bill, NotificationType.PaymentConfirmed);

                        // Send SMS notification if tenant has a phone number and has enabled SMS notifications
                        if (bill.Tenant != null &&
                            !string.IsNullOrEmpty(bill.Tenant.PhoneNumber) &&
                            bill.Tenant.SmsNotificationsEnabled &&
                            bill.Tenant.SmsPaymentConfirmationEnabled)
                        {
                            try
                            {
                                var firstName = bill.Tenant.FirstName;
                                var message = SmsMessageTemplates.PaymentConfirmed(bill, firstName, bill.TotalAmount);

                                await _smsService.SendSmsAsync(bill.Tenant.PhoneNumber, message);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to send batch payment SMS to tenant {TenantId}", bill.TenantId);
                                // Don't interrupt batch processing
                            }
                        }

                        processedCount++;
                    }
                }

                await _context.SaveChangesAsync();
                return Json(new { success = true, message = $"Successfully processed payment for {processedCount} bills" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing batch payment for bills {BillIds}", billIds);
                return Json(new { success = false, message = $"Error processing payment: {ex.Message}" });
            }
        }

        // GET: /Billing/GetAvailableMeterReadings?billId=123
        [HttpGet]
        public async Task<IActionResult> GetAvailableMeterReadings(int billId)
        {
            var bill = await _context.Bills.FindAsync(billId);
            if (bill == null)
                return NotFound();

            // Find the base (earliest) reading for this tenant
            var baseReading = await _context.MeterReadings
                .Where(m => m.TenantId == bill.TenantId)
                .OrderBy(m => m.ReadingDate)
                .FirstOrDefaultAsync();

            var readings = await _context.MeterReadings
                .Where(m => m.TenantId == bill.TenantId
                    && m.BillId == null
                    && (baseReading == null || m.ReadingId != baseReading.ReadingId))
                .OrderBy(m => m.ReadingDate)
                .Select(m => new
                {
                    m.ReadingId,
                    m.ReadingDate,
                    m.CurrentReading,
                    m.UsageKwh,
                    m.TotalCharge
                })
                .ToListAsync();

            return Json(readings);
        }

        // POST: /Billing/LinkMeterReading
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LinkMeterReading(int billId, int readingId)
        {
            var bill = await _context.Bills.FindAsync(billId);
            var reading = await _context.MeterReadings.FindAsync(readingId);

            if (bill == null || reading == null)
                return Json(new { success = false, message = "Bill or reading not found." });

            if (reading.BillId != null)
                return Json(new { success = false, message = "This reading is already linked to a bill." });

            // Link the reading to the bill
            reading.BillId = billId;
            bill.ElectricityFee = reading.TotalCharge;

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Meter reading linked successfully.", charge = reading.TotalCharge });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveBill(int billId)
        {
            var bill = await _context.Bills
                .Include(b => b.Tenant)
                .Include(b => b.Room)
                .FirstOrDefaultAsync(b => b.BillId == billId);

            if (bill == null)
            {
                return NotFound();
            }

            bill.IsApproved = true;
            await _context.SaveChangesAsync();

            // Create notification for tenant
            await _notificationService.CreateBillNotificationAsync(bill, NotificationType.NewBill);

            // Send SMS notification if tenant has a phone number and has enabled SMS notifications
            string smsStatusMessage = null;
            if (bill.Tenant != null &&
                !string.IsNullOrEmpty(bill.Tenant.PhoneNumber) &&
                bill.Tenant.SmsNotificationsEnabled &&
                bill.Tenant.SmsNewBillsEnabled)
            {
                try
                {
                    // Use the SMS message template
                    var tenantName = bill.Tenant.FirstName;
                    var message = SmsMessageTemplates.BillApproved(bill, tenantName);

                    // Send SMS using the SMS service
                    bool smsSent = await _smsService.SendSmsAsync(bill.Tenant.PhoneNumber, message);

                    if (smsSent)
                    {
                        _logger.LogInformation("SMS notification sent to {TenantName} at {PhoneNumber}",
                            $"{bill.Tenant.FirstName} {bill.Tenant.LastName}", bill.Tenant.PhoneNumber);
                        smsStatusMessage = "SMS notification sent to tenant.";
                    }
                    else
                    {
                        smsStatusMessage = "Bill approved, but failed to send SMS notification to tenant.";
                    }
                }
                catch (Exception ex)
                {
                    // Log the error but don't prevent bill approval
                    _logger.LogError(ex, "Failed to send SMS notification for bill {BillId} to tenant {TenantName}",
                        billId, $"{bill.Tenant.FirstName} {bill.Tenant.LastName}");
                    smsStatusMessage = "Bill approved, but failed to send SMS notification to tenant.";
                }
            }

            TempData["SuccessMessage"] = "Bill has been approved and is now visible to the tenant." + (smsStatusMessage != null ? " " + smsStatusMessage : "");
            return RedirectToAction(nameof(BillDetails), new { id = billId });
        }
    }
}