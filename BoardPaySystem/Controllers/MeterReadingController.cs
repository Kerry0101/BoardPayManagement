using BoardPaySystem.Models;
using BoardPaySystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoardPaySystem.Controllers
{
    [Authorize(Roles = "Landlord")]
    public class MeterReadingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMeterReadingService _meterReadingService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<MeterReadingController> _logger;

        public MeterReadingController(
            ApplicationDbContext context,
            IMeterReadingService meterReadingService,
            UserManager<ApplicationUser> userManager,
            ILogger<MeterReadingController> logger)
        {
            _context = context;
            _meterReadingService = meterReadingService;
            _userManager = userManager;
            _logger = logger;
        }

        // GET: MeterReading/Index
        public async Task<IActionResult> Index()
        {
            _logger.LogWarning("DEBUG: Actual connection string: " + _context.Database.GetDbConnection().ConnectionString);
            _logger.LogWarning("DEBUG: Database name: " + _context.Database.GetDbConnection().Database);

            var readings = await _context.MeterReadings
                .Include(m => m.Tenant)
                .Include(m => m.Room)
                    .ThenInclude(r => r.Floor!)
                        .ThenInclude(f => f.Building)
                .OrderByDescending(m => m.ReadingDate)
                .Take(50)
                .ToListAsync();

            // Use only RoomId filter (like Billing)
            var tenantsWithRooms = await _context.Users
                .Where(u => u.RoomId.HasValue)
                .Include(u => u.CurrentRoom!)
                    .ThenInclude(r => r.Floor!)
                        .ThenInclude(f => f.Building)
                .ToListAsync();
            _logger.LogWarning($"DEBUG: tenantsWithRooms count: {tenantsWithRooms.Count}");

            // Raw SQL query for deep diagnostics
            var rawUsers = await _context.Users.FromSqlRaw("SELECT * FROM AspNetUsers WHERE RoomId IS NOT NULL").ToListAsync();
            _logger.LogWarning($"DEBUG: Raw SQL users count: {rawUsers.Count}");

            ViewBag.Tenants = tenantsWithRooms;
            ViewBag.Buildings = await _context.Buildings.ToListAsync();

            return View(readings);
        }

        // GET: MeterReading/Create
        public IActionResult Create()
        {
            return RedirectToAction("MeterReadings", "Landlord");
        }

        // POST: MeterReading/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(string tenantId, decimal currentReading, DateTime readingDate, string? notes)
        {
            return RedirectToAction("MeterReadings", "Landlord");
        }

        // GET: MeterReading/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var reading = await _context.MeterReadings
                .Include(m => m.Tenant)
                .Include(m => m.Room)
                    .ThenInclude(r => r.Floor!)
                        .ThenInclude(f => f.Building)
                .FirstOrDefaultAsync(m => m.ReadingId == id);

            if (reading == null)
            {
                return NotFound();
            }

            // Get previous readings for this tenant
            var previousReadings = await _context.MeterReadings
                .Where(m => m.TenantId == reading.TenantId && m.ReadingDate < reading.ReadingDate)
                .OrderByDescending(m => m.ReadingDate)
                .Take(5)
                .ToListAsync();

            ViewBag.PreviousReadings = previousReadings;

            return View(reading);
        }

        // GET: MeterReading/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var reading = await _context.MeterReadings
                .Include(m => m.Tenant)
                .Include(m => m.Room)
                    .ThenInclude(r => r.Floor!)
                        .ThenInclude(f => f.Building)
                .FirstOrDefaultAsync(m => m.ReadingId == id);

            if (reading == null)
            {
                return NotFound();
            }

            return View(reading);
        }

        // POST: MeterReading/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, decimal currentReading, DateTime readingDate, string? notes)
        {
            var reading = await _context.MeterReadings
                .Include(m => m.Tenant)
                .Include(m => m.Room)
                    .ThenInclude(r => r.Floor!)
                        .ThenInclude(f => f.Building)
                .FirstOrDefaultAsync(m => m.ReadingId == id);

            if (reading == null)
            {
                return NotFound();
            }

            try
            {
                if (currentReading <= 0)
                {
                    ModelState.AddModelError("", "Reading value must be greater than zero.");
                    return View(reading);
                }

                reading.CurrentReading = currentReading;
                reading.ReadingDate = readingDate;
                reading.Notes = notes;

                _context.Update(reading);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Meter reading updated successfully.";
                return RedirectToAction(nameof(Details), new { id = reading.ReadingId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating meter reading");
                ModelState.AddModelError("", $"Error updating meter reading: {ex.Message}");
                return View(reading);
            }
        }

        // POST: MeterReading/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var reading = await _context.MeterReadings.FindAsync(id);
            if (reading == null)
            {
                return Json(new { success = false, message = "Reading not found." });
            }

            try
            {
                // Check if there's a bill associated with this reading
                if (reading.BillId.HasValue)
                {
                    return Json(new { success = false, message = "Cannot delete this reading as it is associated with a bill." });
                }

                _context.MeterReadings.Remove(reading);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Meter reading deleted successfully." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting meter reading");
                return Json(new { success = false, message = $"Error deleting meter reading: {ex.Message}" });
            }
        }

        // GET: MeterReading/Tenants
        public async Task<IActionResult> Tenants(int? buildingId)
        {
            var query = _context.Users
                .Where(u => u.RoomId.HasValue)
                .Include(u => u.CurrentRoom!)
                    .ThenInclude(r => r.Floor!)
                        .ThenInclude(f => f.Building)
                .AsQueryable();

            if (buildingId.HasValue)
            {
                query = query.Where(u => u.CurrentRoom!.Floor!.BuildingId == buildingId);
            }

            var tenants = await query.ToListAsync();

            // Get the latest reading for each tenant
            var tenantIds = tenants.Select(t => t.Id).ToList();
            var latestReadings = await _context.MeterReadings
                .Where(m => tenantIds.Contains(m.TenantId))
                .GroupBy(m => m.TenantId)
                .Select(g => g.OrderByDescending(m => m.ReadingDate).FirstOrDefault())
                .ToListAsync();

            var buildings = await _context.Buildings.ToListAsync();

            ViewBag.Buildings = buildings;
            ViewBag.SelectedBuildingId = buildingId;

            // Filter out null readings (shouldn't happen, but just to be safe)
            var filteredReadings = latestReadings.Where(r => r != null).ToDictionary(r => r.TenantId);
            ViewBag.LatestReadings = filteredReadings;

            // Get current month to check if tenant has reading for this month
            var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var nextMonth = currentMonth.AddMonths(1);

            var tenantsWithCurrentReading = await _context.MeterReadings
                .Where(m => m.ReadingDate >= currentMonth && m.ReadingDate < nextMonth)
                .Select(m => m.TenantId)
                .ToListAsync();

            ViewBag.TenantsWithCurrentReading = tenantsWithCurrentReading;

            return View(tenants);
        }

        // GET: MeterReading/TenantHistory/id
        public async Task<IActionResult> TenantHistory(string id)
        {
            var tenant = await _context.Users
                .Include(u => u.CurrentRoom!)
                    .ThenInclude(r => r.Floor!)
                        .ThenInclude(f => f.Building)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (tenant == null)
            {
                return NotFound();
            }

            var readings = await _context.MeterReadings
                .Where(m => m.TenantId == id)
                .OrderByDescending(m => m.ReadingDate)
                .ToListAsync();

            ViewBag.Tenant = tenant;

            return View(readings);
        }

        // AJAX: Get Floors by Building
        [HttpGet]
        public async Task<IActionResult> GetFloors(int buildingId)
        {
            var floors = await _context.Floors
                .Where(f => f.BuildingId == buildingId)
                .Select(f => new { f.FloorId, f.FloorName })
                .ToListAsync();
            return Json(floors);
        }

        // AJAX: Get Tenants by Floor
        [HttpGet]
        public async Task<IActionResult> GetTenants(int floorId)
        {
            var tenants = await _context.Rooms
                .Where(r => r.FloorId == floorId && r.TenantId != null)
                .Join(_context.Users,
                    room => room.TenantId,
                    user => user.Id,
                    (room, user) => new {
                        TenantId = user.Id,
                        TenantName = user.FirstName + " " + user.LastName
                    })
                .ToListAsync();
            return Json(tenants);
        }
    }
}