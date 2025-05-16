using BoardPaySystem.Models;
using Microsoft.EntityFrameworkCore;

namespace BoardPaySystem.Services
{
    public class BillingService : IBillingService
    {
        private readonly ApplicationDbContext _context;
        private readonly IMeterReadingService _meterReadingService;
        private readonly ILogger<BillingService> _logger;

        public BillingService(
            ApplicationDbContext context,
            IMeterReadingService meterReadingService,
            ILogger<BillingService> logger)
        {
            _context = context;
            _meterReadingService = meterReadingService;
            _logger = logger;
        }

        public async Task<int> GenerateMonthlyBillsAsync(DateTime billingDate)
        {
            // Get tenants who need bills generated today based on their billing cycle
            var tenants = await GetTenantsWithDueBillsAsync(billingDate);
            int billsGenerated = 0;

            foreach (var tenant in tenants)
            {
                // Check if bill already exists for this tenant and month/year
                var existingBill = await _context.Bills
                    .FirstOrDefaultAsync(b =>
                        b.TenantId == tenant.Id &&
                        b.BillingDate.Month == billingDate.Month &&
                        b.BillingDate.Year == billingDate.Year);

                if (existingBill == null)
                {
                    // Generate bill for this tenant
                    await GenerateBillForTenantAsync(tenant.Id, billingDate);
                    billsGenerated++;
                }
            }

            return billsGenerated;
        }
        public async Task<Bill> GenerateBillForTenantAsync(string tenantId, DateTime billingDate)
        {
            // Get tenant with room information
            var tenant = await _context.Users
                .Include(u => u.CurrentRoom)
                    .ThenInclude(r => r != null ? r.Floor : null)
                        .ThenInclude(f => f != null ? f.Building : null)
                .FirstOrDefaultAsync(u => u.Id == tenantId);

            if (tenant == null || tenant.CurrentRoom == null)
            {
                throw new ArgumentException($"Tenant with ID {tenantId} not found or has no assigned room.");
            }

            if (tenant.CurrentRoom.Floor == null || tenant.CurrentRoom.Floor.Building == null)
            {
                throw new ArgumentException($"Tenant with ID {tenantId} has a room with incomplete floor or building data.");
            }

            try
            {
                // Check if this tenant has a meter reading for this billing period
                var hasReading = await _meterReadingService.HasCompletedReadingForBillingPeriodAsync(tenantId, billingDate);

                if (!hasReading)
                {
                    _logger.LogWarning("Tenant {TenantId} does not have a meter reading for {BillingPeriod}, but proceeding with bill generation",
                        tenantId, billingDate.ToString("yyyy-MM"));
                    // Continue with bill generation instead of throwing an exception
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error checking for meter readings for tenant {TenantId}, but proceeding with bill generation", tenantId);
                // Continue with bill generation despite errors
            }

            // Use default values if custom values are not set
            decimal monthlyRent = (tenant.CurrentRoom.CustomMonthlyRent.HasValue && tenant.CurrentRoom.CustomMonthlyRent.Value != 0)
                ? tenant.CurrentRoom.CustomMonthlyRent.Value
                : ((tenant.CurrentRoom.Floor.Building.DefaultMonthlyRent != 0)
                    ? tenant.CurrentRoom.Floor.Building.DefaultMonthlyRent
                    : 5000m);
            decimal waterFee = (tenant.CurrentRoom.CustomWaterFee.HasValue && tenant.CurrentRoom.CustomWaterFee.Value != 0)
                ? tenant.CurrentRoom.CustomWaterFee.Value
                : ((tenant.CurrentRoom.Floor.Building.DefaultWaterFee != 0)
                    ? tenant.CurrentRoom.Floor.Building.DefaultWaterFee
                    : 300m);
            decimal wifiFee = (tenant.CurrentRoom.CustomWifiFee.HasValue && tenant.CurrentRoom.CustomWifiFee.Value != 0)
                ? tenant.CurrentRoom.CustomWifiFee.Value
                : ((tenant.CurrentRoom.Floor.Building.DefaultWifiFee != 0)
                    ? tenant.CurrentRoom.Floor.Building.DefaultWifiFee
                    : 200m);

            // Log a warning if any fee is zero
            if (monthlyRent == 0 || waterFee == 0 || wifiFee == 0)
            {
                _logger.LogWarning($"Bill for tenant {tenantId} has a zero fee: Rent={monthlyRent}, Water={waterFee}, Wifi={wifiFee}. Check building and room settings.");
            }

            // Only charge if there are at least two readings for the period
            var firstDayOfMonth = new DateTime(billingDate.Year, billingDate.Month, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
            var readings = await _context.MeterReadings
                .Where(m => m.TenantId == tenantId && m.ReadingDate >= firstDayOfMonth && m.ReadingDate <= lastDayOfMonth)
                .OrderBy(m => m.ReadingDate)
                .ToListAsync();
            decimal electricityFee = 0;
            if (readings.Count >= 2)
            {
                electricityFee = readings.Last().TotalCharge;
            }

            // Create new bill using the room's rate information
            var bill = new Bill
            {
                TenantId = tenantId,
                RoomId = tenant.CurrentRoom.RoomId,
                BillingDate = billingDate,
                BillingMonth = billingDate.Month,
                BillingYear = billingDate.Year,
                // Calculate due date based on the tenant's start date
                DueDate = CalculateBillDueDate(tenant.StartDate, billingDate),
                MonthlyRent = monthlyRent,
                WaterFee = waterFee,
                ElectricityFee = electricityFee,
                WifiFee = wifiFee,
                Status = BillStatus.NotPaid
            };

            _context.Bills.Add(bill);
            await _context.SaveChangesAsync();
            return bill;
        }

        public async Task<Bill> GenerateInitialBillForTenantAsync(string tenantId)
        {
            // Get tenant with room information
            var tenant = await _context.Users
                .Include(u => u.CurrentRoom)
                    .ThenInclude(r => r != null ? r.Floor : null)
                        .ThenInclude(f => f != null ? f.Building : null)
                .FirstOrDefaultAsync(u => u.Id == tenantId);

            if (tenant == null || tenant.CurrentRoom == null)
            {
                throw new ArgumentException($"Tenant with ID {tenantId} not found or has no assigned room.");
            }

            if (tenant.CurrentRoom.Floor == null || tenant.CurrentRoom.Floor.Building == null)
            {
                throw new ArgumentException($"Tenant with ID {tenantId} has a room with incomplete floor or building data.");
            }

            var building = tenant.CurrentRoom.Floor.Building;

            // For initial bill, we use the tenant's start date
            var firstDayOfMonth = new DateTime(tenant.StartDate.Year, tenant.StartDate.Month, 1);

            // Set due date to one month after start date (e.g., start May 9, due June 9)
            DateTime dueDate;
            try
            {
                dueDate = tenant.StartDate.AddMonths(1);
            }
            catch
            {
                var nextMonth = tenant.StartDate.AddMonths(1);
                dueDate = new DateTime(nextMonth.Year, nextMonth.Month, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month));
            }

            // Create the bill with proper property names, but without electricity fee
            var bill = new Bill
            {
                TenantId = tenantId,
                RoomId = tenant.CurrentRoom.RoomId,
                BillingDate = firstDayOfMonth,
                BillingMonth = tenant.StartDate.Month,
                BillingYear = tenant.StartDate.Year,
                DueDate = dueDate, // Due one month after contract start date
                MonthlyRent = (tenant.CurrentRoom.CustomMonthlyRent.HasValue && tenant.CurrentRoom.CustomMonthlyRent.Value != 0)
                    ? tenant.CurrentRoom.CustomMonthlyRent.Value
                    : ((building.DefaultMonthlyRent != 0)
                        ? building.DefaultMonthlyRent
                        : 5000m),
                WaterFee = (tenant.CurrentRoom.CustomWaterFee.HasValue && tenant.CurrentRoom.CustomWaterFee.Value != 0)
                    ? tenant.CurrentRoom.CustomWaterFee.Value
                    : ((building.DefaultWaterFee != 0)
                        ? building.DefaultWaterFee
                        : 300m),
                ElectricityFee = 0, // Disregard electricity fee for now
                WifiFee = (tenant.CurrentRoom.CustomWifiFee.HasValue && tenant.CurrentRoom.CustomWifiFee.Value != 0)
                    ? tenant.CurrentRoom.CustomWifiFee.Value
                    : ((building.DefaultWifiFee != 0)
                        ? building.DefaultWifiFee
                        : 200m),
                Status = BillStatus.NotPaid,
                Notes = "Initial bill generated on " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            // Log a warning if any fee is zero for initial bill
            if (bill.MonthlyRent == 0 || bill.WaterFee == 0 || bill.WifiFee == 0)
            {
                _logger.LogWarning($"Initial bill for tenant {tenantId} has a zero fee: Rent={bill.MonthlyRent}, Water={bill.WaterFee}, Wifi={bill.WifiFee}. Check building and room settings.");
            }

            _context.Bills.Add(bill);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Initial bill {BillId} created for tenant {TenantId}", bill.BillId, tenantId);

            return bill;
        }

        public async Task<bool> ProcessPaymentAsync(int billId, decimal amount, string reference)
        {
            var bill = await _context.Bills.FindAsync(billId);
            if (bill == null)
            {
                return false;
            }

            // In-person payments get marked as Paid immediately
            bill.Status = BillStatus.Paid;
            bill.PaymentDate = DateTime.Now;
            bill.PaymentReference = reference;
            bill.AmountPaid = amount;

            // Create payment record
            var payment = new Payment
            {
                BillId = billId,
                Amount = amount,
                PaymentDate = DateTime.Now,
                PaymentMethod = "Cash/In-Person",
                ReferenceNumber = reference
            };

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task UpdateBillStatusesAsync()
        {
            var today = DateTime.Now.Date;

            // Find all bills that are NotPaid or Pending and past due date
            var overdueBills = await _context.Bills
                .Include(b => b.Room)
                    .ThenInclude(r => r != null ? r.Floor : null)
                        .ThenInclude(f => f != null ? f.Building : null)
                .Where(b => (b.Status == BillStatus.NotPaid || b.Status == BillStatus.Pending) &&
                            b.DueDate < today)
                .ToListAsync();

            foreach (var bill in overdueBills)
            {
                // Only mark as overdue if due date is before today
                if (bill.DueDate < today && (bill.Status == BillStatus.NotPaid || bill.Status == BillStatus.Pending))
                {
                    bill.Status = BillStatus.Overdue;

                    // Calculate and store late fee for each overdue bill
                    if (bill.Room?.Floor?.Building != null)
                    {
                        decimal lateFeePercentage = bill.Room.Floor.Building.LateFee;
                        decimal calculatedLateFee = bill.TotalAmount * (lateFeePercentage / 100);

                        // Only update the late fee if it's not already set
                        if (!bill.LateFee.HasValue || bill.LateFee.Value == 0)
                        {
                            bill.LateFee = calculatedLateFee;
                            _logger.LogInformation("Applied late fee of {LateFee:C} to bill {BillId}",
                                calculatedLateFee, bill.BillId);
                        }
                    }
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task<List<ApplicationUser>> GetTenantsWithDueBillsAsync(DateTime currentDate)
        {
            // First, get all tenants who have a room assigned
            var tenantsWithRooms = await _context.Users
                .Where(u => u.RoomId.HasValue)
                .ToListAsync();

            // Then filter to only include tenants whose billing day matches today's date
            // and who have a completed meter reading for this billing period
            var tenantsWithDueBills = new List<ApplicationUser>();

            foreach (var tenant in tenantsWithRooms)
            {
                // Calculate if today is their billing day based on start date
                if (IsBillingDayForTenant(tenant.StartDate, currentDate))
                {
                    // Only include tenants with completed meter readings
                    // (except for their first month)
                    bool isFirstMonth = IsFirstBillingMonth(tenant.StartDate, currentDate);
                    bool hasReading = await _meterReadingService.HasCompletedReadingForBillingPeriodAsync(tenant.Id, currentDate);

                    if (isFirstMonth || hasReading)
                    {
                        tenantsWithDueBills.Add(tenant);
                    }
                }
            }

            return tenantsWithDueBills;
        }

        public DateTime CalculateNextBillingDate(DateTime startDate, DateTime currentDate)
        {
            // Calculate the next billing date based on the start date
            // If today is before the billing day in the current month, use this month
            // Otherwise, use next month
            int startDay = startDate.Day;
            int currentMonth = currentDate.Month;
            int currentYear = currentDate.Year;

            // Try to create a date with the same day in the current month
            DateTime candidateDate = new DateTime(currentYear, currentMonth, 1);

            try
            {
                candidateDate = new DateTime(currentYear, currentMonth, startDay);
            }
            catch (ArgumentOutOfRangeException)
            {
                // If the day doesn't exist in the current month, use the last day of the month
                candidateDate = new DateTime(currentYear, currentMonth, 1).AddMonths(1).AddDays(-1);
            }

            // If the candidate date is in the past, move to next month
            if (candidateDate < currentDate)
            {
                currentMonth++;
                if (currentMonth > 12)
                {
                    currentMonth = 1;
                    currentYear++;
                }

                try
                {
                    candidateDate = new DateTime(currentYear, currentMonth, startDay);
                }
                catch (ArgumentOutOfRangeException)
                {
                    // If the day doesn't exist in the next month, use the last day of the month
                    candidateDate = new DateTime(currentYear, currentMonth, 1).AddMonths(1).AddDays(-1);
                }
            }

            return candidateDate;
        }

        // Helper method to determine if today is the billing day for a tenant
        private bool IsBillingDayForTenant(DateTime startDate, DateTime currentDate)
        {
            // If the day of the month matches, it's their billing day
            // Handle months with fewer days (e.g., if start date was 31st, Feb would use 28th)
            int startDay = startDate.Day;
            int daysInCurrentMonth = DateTime.DaysInMonth(currentDate.Year, currentDate.Month);

            // If the start day is beyond the days in the current month, use the last day
            int effectiveBillingDay = Math.Min(startDay, daysInCurrentMonth);

            return currentDate.Day == effectiveBillingDay;
        }

        // Helper method to calculate the due date for a bill
        private DateTime CalculateBillDueDate(DateTime startDate, DateTime billingDate)
        {
            // Due date is the same day as startDate, but in the month after billingDate
            var dueMonth = billingDate.Month + 1;
            var dueYear = billingDate.Year;
            if (dueMonth > 12)
            {
                dueMonth = 1;
                dueYear++;
            }
            int dueDay = Math.Min(startDate.Day, DateTime.DaysInMonth(dueYear, dueMonth));
            return new DateTime(dueYear, dueMonth, dueDay);
        }

        // Helper method to determine if this is the tenant's first billing month
        private bool IsFirstBillingMonth(DateTime startDate, DateTime currentDate)
        {
            return startDate.Year == currentDate.Year && startDate.Month == currentDate.Month;
        }

        // Ensures every tenant has a bill for every month from their start date to now
        public async Task<int> BackfillBillsForAllTenantsAsync()
        {
            int billsGenerated = 0;
            var tenants = await _context.Users.Where(u => u.RoomId.HasValue).ToListAsync();
            var today = DateTime.Today;
            foreach (var tenant in tenants)
            {
                var startDate = tenant.StartDate;
                var currentDate = new DateTime(startDate.Year, startDate.Month, 1);
                var tenantBills = await _context.Bills.Where(b => b.TenantId == tenant.Id).ToListAsync();
                var existingBillMonths = tenantBills.Select(b => (b.BillingMonth, b.BillingYear)).ToHashSet();
                while (currentDate <= today)
                {
                    var monthKey = (currentDate.Month, currentDate.Year);
                    if (!existingBillMonths.Contains(monthKey))
                    {
                        await GenerateBillForTenantAsync(tenant.Id, currentDate);
                        billsGenerated++;
                        // Update the set after adding
                        tenantBills = await _context.Bills.Where(b => b.TenantId == tenant.Id).ToListAsync();
                        existingBillMonths = tenantBills.Select(b => (b.BillingMonth, b.BillingYear)).ToHashSet();
                    }
                    currentDate = currentDate.AddMonths(1);
                }
            }
            return billsGenerated;
        }
    }
}
