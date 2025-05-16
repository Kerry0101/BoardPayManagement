using BoardPaySystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BoardPaySystem.Services
{
    public class LandlordService : ILandlordService
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LandlordService> _logger;

        public LandlordService(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ILogger<LandlordService> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<Dictionary<string, object>> GetDashboardStatsAsync()
        {
            var stats = new Dictionary<string, object>();
            try
            {
                stats["BuildingsCount"] = await _context.Buildings.CountAsync();
                stats["FloorsCount"] = await _context.Floors.CountAsync();
                stats["RoomsCount"] = await _context.Rooms.CountAsync();
                stats["TenantsCount"] = (await _userManager.GetUsersInRoleAsync("Tenant")).Count;

                // Room status
                var totalRooms = await _context.Rooms.CountAsync();
                var occupiedRooms = await _context.Rooms.Where(r => r.IsOccupied).CountAsync();
                stats["TotalRooms"] = totalRooms;
                stats["OccupiedRooms"] = occupiedRooms;
                stats["VacantRooms"] = totalRooms - occupiedRooms;
                stats["OccupancyRate"] = totalRooms > 0 ? (occupiedRooms * 100.0 / totalRooms).ToString("F1") : "0.0";

                // Contracts
                var today = DateTime.Today;
                var thirtyDaysFromNow = today.AddDays(30);
                stats["ActiveContracts"] = await _context.Contracts.Where(c => c.Status == ContractStatus.Active).CountAsync();
                stats["ExpiringContracts"] = await _context.Contracts.Where(c => c.Status == ContractStatus.Active && c.EndDate <= thirtyDaysFromNow).CountAsync();

                // Billing status for current month
                var currentMonth = DateTime.Today.Month;
                var currentYear = DateTime.Today.Year;
                var monthlyBills = await _context.Bills.Where(b => b.BillingDate.Month == currentMonth && b.BillingDate.Year == currentYear).ToListAsync();
                stats["TotalBills"] = monthlyBills.Count;
                stats["PaidBills"] = monthlyBills.Count(b => b.Status == BillStatus.Paid);
                stats["PendingBills"] = monthlyBills.Count(b => b.Status == BillStatus.Pending);
                stats["OverdueBills"] = monthlyBills.Count(b => b.Status == BillStatus.Overdue);
                stats["TotalBilledAmount"] = monthlyBills.Sum(b => b.TotalAmount).ToString("C");
                stats["TotalPaidAmount"] = monthlyBills.Where(b => b.Status == BillStatus.Paid).Sum(b => b.TotalAmount).ToString("C");
                stats["TotalPendingAmount"] = monthlyBills.Where(b => b.Status == BillStatus.Pending || b.Status == BillStatus.Overdue).Sum(b => b.TotalAmount).ToString("C");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetDashboardStatsAsync: {Message}", ex.Message);
                stats["Error"] = "An error occurred while loading the dashboard. Please try again.";
            }
            return stats;
        }
    }
}