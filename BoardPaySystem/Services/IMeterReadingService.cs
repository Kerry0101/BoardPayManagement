using BoardPaySystem.Models;

namespace BoardPaySystem.Services
{
    public interface IMeterReadingService
    {
        // Record a new meter reading
        Task<MeterReading> RecordReadingAsync(string tenantId, int roomId, decimal currentReading, DateTime readingDate, string? notes = null);

        // Get the most recent reading for a tenant
        Task<MeterReading?> GetLatestReadingAsync(string tenantId);

        // Get readings for a tenant within a date range
        Task<List<MeterReading>> GetReadingsForPeriodAsync(string tenantId, DateTime startDate, DateTime endDate);

        // Calculate electricity charge for a tenant based on meter readings
        Task<decimal> CalculateElectricityChargeAsync(string tenantId, DateTime billingDate);

        // Get all tenants needing billing based on completed meter readings
        Task<List<ApplicationUser>> GetTenantsWithCompletedReadingsAsync(DateTime currentDate);

        // Check if a tenant has a completed meter reading for the current billing period
        Task<bool> HasCompletedReadingForBillingPeriodAsync(string tenantId, DateTime billingDate);
    }
}