using BoardPaySystem.Models;
using Microsoft.EntityFrameworkCore;

namespace BoardPaySystem.Services
{
    public class MeterReadingService : IMeterReadingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MeterReadingService> _logger;

        public MeterReadingService(
            ApplicationDbContext context,
            ILogger<MeterReadingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<MeterReading> RecordReadingAsync(string tenantId, int roomId, decimal currentReading, DateTime readingDate, string? notes = null)
        {
            _logger.LogInformation("Recording meter reading for tenant {TenantId}, room {RoomId}, reading {Reading}, date {Date}",
                tenantId, roomId, currentReading, readingDate);

            // Get rate per kWh from the building or a default value
            var tenant = await _context.Users
                .Include(t => t.CurrentRoom)
                    .ThenInclude(r => r.Floor)
                        .ThenInclude(f => f.Building)
                .FirstOrDefaultAsync(t => t.Id == tenantId);

            if (tenant == null || tenant.CurrentRoom == null || tenant.CurrentRoom.Floor == null || tenant.CurrentRoom.Floor.Building == null)
            {
                throw new ArgumentException($"Tenant {tenantId} or room {roomId} not found or incomplete data");
            }

            // Get the previous reading for this tenant, if any
            var previousReading = await _context.MeterReadings
                .Where(m => m.TenantId == tenantId)
                .OrderByDescending(m => m.ReadingDate)
                .FirstOrDefaultAsync();

            // Use the default electricity fee per kWh from the building
            decimal ratePerKwh = tenant.CurrentRoom.CustomElectricityFee ??
                                 tenant.CurrentRoom.Floor.Building.DefaultElectricityFee;

            var reading = new MeterReading
            {
                TenantId = tenantId,
                RoomId = roomId,
                ReadingDate = readingDate,
                CurrentReading = currentReading,
                PreviousReading = previousReading != null ? previousReading.CurrentReading : (decimal?)null,
                RatePerKwh = ratePerKwh,
                Notes = notes
            };

            _context.MeterReadings.Add(reading);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Meter reading recorded successfully: ID {ReadingId}, Usage {UsageKwh} kWh",
                reading.ReadingId, reading.UsageKwh);

            return reading;
        }

        public async Task<MeterReading?> GetLatestReadingAsync(string tenantId)
        {
            return await _context.MeterReadings
                .Where(m => m.TenantId == tenantId)
                .OrderByDescending(m => m.ReadingDate)
                .FirstOrDefaultAsync();
        }

        public async Task<List<MeterReading>> GetReadingsForPeriodAsync(string tenantId, DateTime startDate, DateTime endDate)
        {
            return await _context.MeterReadings
                .Where(m => m.TenantId == tenantId &&
                            m.ReadingDate >= startDate &&
                            m.ReadingDate <= endDate)
                .OrderBy(m => m.ReadingDate)
                .ToListAsync();
        }

        public async Task<decimal> CalculateElectricityChargeAsync(string tenantId, DateTime billingDate)
        {
            // Get the first day of the current month
            var firstDayOfMonth = new DateTime(billingDate.Year, billingDate.Month, 1);

            // Get the last day of the current month
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            // Find the latest reading in the billing period
            var latestReading = await _context.MeterReadings
                .Where(m => m.TenantId == tenantId &&
                            m.ReadingDate >= firstDayOfMonth &&
                            m.ReadingDate <= lastDayOfMonth)
                .OrderByDescending(m => m.ReadingDate)
                .FirstOrDefaultAsync();

            if (latestReading == null)
            {
                _logger.LogWarning("No meter reading found for tenant {TenantId} in billing period {BillingMonth}",
                    tenantId, billingDate.ToString("yyyy-MM"));
                return 0; // No reading, no charge
            }

            return latestReading.TotalCharge;
        }

        public async Task<List<ApplicationUser>> GetTenantsWithCompletedReadingsAsync(DateTime currentDate)
        {
            // Get the first day of the current month
            var firstDayOfMonth = new DateTime(currentDate.Year, currentDate.Month, 1);

            // Get the last day of the current month
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            // Find all tenants who have readings for the current month
            var tenantsWithReadings = await _context.MeterReadings
                .Where(m => m.ReadingDate >= firstDayOfMonth &&
                            m.ReadingDate <= lastDayOfMonth)
                .Select(m => m.TenantId)
                .Distinct()
                .ToListAsync();

            // Get the full tenant information
            var tenants = await _context.Users
                .Where(u => tenantsWithReadings.Contains(u.Id))
                .ToListAsync();

            return tenants;
        }

        public async Task<bool> HasCompletedReadingForBillingPeriodAsync(string tenantId, DateTime billingDate)
        {
            // Get the first day of the billing month
            var firstDayOfMonth = new DateTime(billingDate.Year, billingDate.Month, 1);

            // Get the last day of the billing month
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);

            try
            {
                // Check if there's at least one reading in this period
                return await _context.MeterReadings
                    .AnyAsync(m => m.TenantId == tenantId &&
                                  m.ReadingDate >= firstDayOfMonth &&
                                  m.ReadingDate <= lastDayOfMonth);
            }
            catch (Exception ex)
            {
                // If the table doesn't exist yet, or any other error occurs, log and return false
                _logger.LogWarning(ex, "Error checking for meter readings for tenant {TenantId}: {ErrorMessage}", tenantId, ex.Message);
                return false;
            }
        }
    }
}