using BoardPaySystem.Models;

namespace BoardPaySystem.Services
{
    public interface IBillingService
    {
        // Generate bills for all tenants
        Task<int> GenerateMonthlyBillsAsync(DateTime billingDate);

        // Generate bill for a single tenant
        Task<Bill> GenerateBillForTenantAsync(string tenantId, DateTime billingDate);

        // Generate initial bill for a new tenant (without meter reading requirement)
        Task<Bill> GenerateInitialBillForTenantAsync(string tenantId);

        // Process a payment for a bill
        Task<bool> ProcessPaymentAsync(int billId, decimal amount, string paymentReference);

        // Update status of bills (e.g., mark overdue bills)
        Task UpdateBillStatusesAsync();

        // Get bills due for generation based on tenant billing cycles and completed meter readings
        Task<List<ApplicationUser>> GetTenantsWithDueBillsAsync(DateTime currentDate);

        // Calculate the next billing date for a tenant based on their start date
        DateTime CalculateNextBillingDate(DateTime startDate, DateTime currentDate);

        // Ensures every tenant has a bill for every month from their start date to now
        Task<int> BackfillBillsForAllTenantsAsync();
    }
}