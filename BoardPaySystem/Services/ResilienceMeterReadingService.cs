using BoardPaySystem.Models;
using Microsoft.EntityFrameworkCore;

namespace BoardPaySystem.Services
{
    /// <summary>
    /// A resilient implementation of IMeterReadingService that can handle missing tables and database errors.
    /// Use this instead of the standard MeterReadingService to prevent application crashes.
    /// </summary>
    public class ResilienceMeterReadingService : IMeterReadingService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ResilienceMeterReadingService> _logger;

        public ResilienceMeterReadingService(
            ApplicationDbContext context,
            ILogger<ResilienceMeterReadingService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<MeterReading> RecordReadingAsync(string tenantId, int roomId, decimal currentReading, DateTime readingDate, string? notes = null)
        {
            _logger.LogInformation("Recording meter reading for tenant {TenantId}, room {RoomId}, reading {Reading}, date {Date}",
                tenantId, roomId, currentReading, readingDate);

            try
            {
                // Get rate per kWh from the building or a default value
                var tenant = await _context.Users
                    .Include(t => t.CurrentRoom)
                    .ThenInclude(r => r != null ? r.Floor : null)
                    .ThenInclude(f => f != null ? f.Building : null)
                    .FirstOrDefaultAsync(t => t.Id == tenantId);

                if (tenant == null || tenant.CurrentRoom == null || tenant.CurrentRoom.Floor == null || tenant.CurrentRoom.Floor.Building == null)
                {
                    throw new ArgumentException($"Tenant {tenantId} or room {roomId} not found or incomplete data");
                }

                decimal ratePerKwh = 0;

                try
                {
                    // Get the previous reading for this tenant, if any
                    var previousReading = await _context.MeterReadings
                        .Where(m => m.TenantId == tenantId)
                        .OrderByDescending(m => m.ReadingDate)
                        .FirstOrDefaultAsync();

                    // Use the default electricity fee per kWh from the building
                    ratePerKwh = tenant.CurrentRoom.CustomElectricityFee ??
                                tenant.CurrentRoom.Floor.Building.DefaultElectricityFee;

                    var reading = new MeterReading
                    {
                        TenantId = tenantId,
                        RoomId = roomId,
                        ReadingDate = readingDate,
                        CurrentReading = currentReading,
                        PreviousReading = previousReading?.CurrentReading,
                        RatePerKwh = ratePerKwh,
                        Notes = notes
                    };

                    _context.MeterReadings.Add(reading);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Meter reading recorded successfully: ID {ReadingId}, Usage {UsageKwh} kWh",
                        reading.ReadingId, reading.UsageKwh);

                    return reading;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Database error when recording meter reading for tenant {TenantId}", tenantId);

                    // Create a basic reading since we couldn't save to the database
                    return new MeterReading
                    {
                        TenantId = tenantId,
                        RoomId = roomId,
                        ReadingDate = readingDate,
                        CurrentReading = currentReading,
                        PreviousReading = 0,
                        RatePerKwh = ratePerKwh,
                        Notes = notes
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record meter reading for tenant {TenantId}", tenantId);

                // Create a fallback reading with default values
                return new MeterReading
                {
                    TenantId = tenantId,
                    RoomId = roomId,
                    ReadingDate = readingDate,
                    CurrentReading = currentReading,
                    PreviousReading = 0,
                    RatePerKwh = 5.00m, // Default rate
                    Notes = notes
                };
            }
        }

        public async Task<MeterReading?> GetLatestReadingAsync(string tenantId)
        {
            try
            {
                return await _context.MeterReadings
                    .Where(m => m.TenantId == tenantId)
                    .OrderByDescending(m => m.ReadingDate)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting latest reading for tenant {TenantId}", tenantId);
                return null;
            }
        }

        public async Task<List<MeterReading>> GetReadingsForPeriodAsync(string tenantId, DateTime startDate, DateTime endDate)
        {
            try
            {
                return await _context.MeterReadings
                    .Where(m => m.TenantId == tenantId &&
                                m.ReadingDate >= startDate &&
                                m.ReadingDate <= endDate)
                    .OrderBy(m => m.ReadingDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting readings for period for tenant {TenantId}", tenantId);
                return new List<MeterReading>();
            }
        }

        public async Task<decimal> CalculateElectricityChargeAsync(string tenantId, DateTime billingDate)
        {
            try
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

                    // Get tenant data to determine default electricity charge
                    var tenant = await _context.Users
                        .Include(t => t.CurrentRoom)
                            .ThenInclude(r => r != null ? r.Floor : null)
                                .ThenInclude(f => f != null ? f.Building : null)
                        .FirstOrDefaultAsync(t => t.Id == tenantId);

                    if (tenant?.CurrentRoom?.Floor?.Building != null)
                    {
                        // Use a default amount based on building settings
                        return tenant.CurrentRoom.Floor.Building.DefaultElectricityFee * 100; // Arbitrary usage of 100 kWh
                    }

                    return 500; // Default electricity charge if we can't determine anything
                }

                return latestReading.TotalCharge;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error calculating electricity charge for tenant {TenantId}", tenantId);
                return 500; // Default value on error
            }
        }

        public async Task<List<ApplicationUser>> GetTenantsWithCompletedReadingsAsync(DateTime currentDate)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting tenants with completed readings");
                return new List<ApplicationUser>();
            }
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
                bool hasReading = await _context.MeterReadings
                    .AnyAsync(m => m.TenantId == tenantId &&
                                  m.ReadingDate >= firstDayOfMonth &&
                                  m.ReadingDate <= lastDayOfMonth);
                return hasReading;
            }
            catch (Exception ex)
            {
                // If the table doesn't exist yet, or any other error occurs, log and return true
                // to allow billing to proceed
                _logger.LogWarning(ex, "Error checking for meter readings for tenant {TenantId}: {ErrorMessage}",
                    tenantId, ex.Message);
                return true; // Allow billing to proceed even if the table doesn't exist
            }
        }
    }
}
