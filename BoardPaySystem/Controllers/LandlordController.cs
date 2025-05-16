using BoardPaySystem.Models;
using BoardPaySystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoardPaySystem.Controllers
{
    [Authorize(Roles = "Landlord")]
    public class LandlordController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LandlordController> _logger;
        private readonly ILandlordService _landlordService;
        private readonly IBillingService _billingService;

        public LandlordController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<LandlordController> logger,
            ILandlordService landlordService,
            IBillingService billingService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _landlordService = landlordService;
            _billingService = billingService;
        }

        public async Task<IActionResult> Index()
        {
            var buildings = await _context.Buildings
                .Include(b => b.Floors)
                .ThenInclude(f => f.Rooms)
                .ToListAsync();

            var tenants = await _userManager.GetUsersInRoleAsync("Tenant");

            ViewBag.TotalBuildings = buildings.Count;
            ViewBag.TotalFloors = buildings.Sum(b => b.Floors.Count);
            ViewBag.TotalRooms = buildings.Sum(b => b.Floors.Sum(f => f.Rooms.Count));
            ViewBag.TotalTenants = tenants.Count;
            ViewBag.OccupiedRooms = buildings.Sum(b => b.Floors.Sum(f => f.Rooms.Count(r => r.IsOccupied)));
            ViewBag.VacantRooms = ViewBag.TotalRooms - ViewBag.OccupiedRooms;

            return View();
        }

        public async Task<IActionResult> LandlordProfile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }
            return View(user);
        }

        public async Task<IActionResult> Overview()
        {
            var stats = await _landlordService.GetDashboardStatsAsync();
            foreach (var kvp in stats)
            {
                ViewData[kvp.Key] = kvp.Value;
            }
            if (stats.ContainsKey("Error"))
            {
                TempData["Error"] = stats["Error"];
            }
            return View();
        }

        public async Task<IActionResult> ManageBuildings(int? buildingId = null)
        {
            var allBuildings = await _context.Buildings.ToListAsync();
            var buildingsQuery = _context.Buildings.AsQueryable();
            if (buildingId.HasValue)
            {
                buildingsQuery = buildingsQuery.Where(b => b.BuildingId == buildingId.Value);
            }
            var buildings = await buildingsQuery
                .Select(b => new BuildingListViewModel
                {
                    BuildingId = b.BuildingId,
                    BuildingName = b.BuildingName,
                    Address = b.Address,
                    TotalFloors = b.Floors.Count,
                    TotalRooms = b.Floors.Sum(f => f.Rooms.Count),
                    OccupiedRooms = b.Floors.Sum(f => f.Rooms.Count(r => r.IsOccupied))
                })
                .ToListAsync();
            ViewBag.AllBuildings = allBuildings;
            ViewBag.CurrentBuildingId = buildingId;
            return View(buildings);
        }

        public async Task<IActionResult> Billing(string status = "all", string month = "current", string building = "all")
        {
            await _billingService.BackfillBillsForAllTenantsAsync();
            await _billingService.UpdateBillStatusesAsync();

            var billsQuery = _context.Bills
                .Include(b => b.Tenant)
                .Include(b => b.Room)
                    .ThenInclude(r => r.Floor)
                        .ThenInclude(f => f.Building)
                .AsQueryable();

            // Filter by status
            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                switch (status.ToLower())
                {
                    case "notpaid":
                        billsQuery = billsQuery.Where(b => b.Status == BillStatus.NotPaid);
                        break;
                    case "overdue":
                        billsQuery = billsQuery.Where(b => b.Status == BillStatus.Overdue);
                        break;
                    case "paid":
                        billsQuery = billsQuery.Where(b => b.Status == BillStatus.Paid);
                        break;
                    case "writtenoff":
                        billsQuery = billsQuery.Where(b => b.Status == BillStatus.WrittenOff);
                        break;
                }
            }

            // Filter by month
            if (!string.IsNullOrEmpty(month) && month != "all")
            {
                int filterYear = DateTime.Now.Year;
                int filterMonth = DateTime.Now.Month;
                if (month == "current")
                {
                    // already set
                }
                else if (DateTime.TryParseExact(month + "-01", "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var parsedDate))
                {
                    filterYear = parsedDate.Year;
                    filterMonth = parsedDate.Month;
                }
                billsQuery = billsQuery.Where(b => b.BillingYear == filterYear && b.BillingMonth == filterMonth);
            }

            // Filter by building
            if (!string.IsNullOrEmpty(building) && building != "all")
            {
                if (int.TryParse(building, out int buildingId))
                {
                    billsQuery = billsQuery.Where(b => b.Room.Floor.BuildingId == buildingId);
                }
            }

            var bills = await billsQuery.OrderByDescending(b => b.DueDate).ToListAsync();

            var billsByTenant = bills.GroupBy(b => b.TenantId).ToDictionary(g => g.Key, g => g.ToList());

            var tenantsWithMultipleUnpaidBills = new List<ApplicationUser>();
            var tenantsWithOnlyCurrentUnpaidBill = new List<ApplicationUser>();
            var tenantTotalAmountsDue = new Dictionary<string, decimal>();
            var tenantUnpaidBillCount = new Dictionary<string, int>();

            foreach (var kvp in billsByTenant)
            {
                var tenantBills = kvp.Value;
                var unpaidBills = tenantBills.Where(b => b.Status != BillStatus.Paid && b.Status != BillStatus.Cancelled && b.Status != BillStatus.WrittenOff).ToList();
                var tenant = tenantBills.First().Tenant;
                if (unpaidBills.Count > 1)
                {
                    tenantsWithMultipleUnpaidBills.Add(tenant);
                }
                else if (unpaidBills.Count == 1)
                {
                    tenantsWithOnlyCurrentUnpaidBill.Add(tenant);
                }
                if (unpaidBills.Count > 0)
                {
                    tenantTotalAmountsDue[tenant.Id] = unpaidBills.Sum(b => b.TotalAmount + (b.LateFee ?? 0));
                    tenantUnpaidBillCount[tenant.Id] = unpaidBills.Count;
                }
            }

            var buildings = await _context.Buildings.ToListAsync();
            ViewBag.Buildings = buildings;
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentMonth = month;
            ViewBag.CurrentBuilding = building;
            ViewBag.TenantsWithMultipleUnpaidBills = tenantsWithMultipleUnpaidBills;
            ViewBag.TenantsWithOnlyCurrentUnpaidBill = tenantsWithOnlyCurrentUnpaidBill;
            ViewBag.BillsByTenant = billsByTenant;
            ViewBag.TenantTotalAmountsDue = tenantTotalAmountsDue;
            ViewBag.TenantUnpaidBillCount = tenantUnpaidBillCount;

            return View();
        }

        public async Task<IActionResult> AddTenant()
        {
            var buildings = await _context.Buildings.ToListAsync();
            ViewBag.Buildings = buildings;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddTenant([Bind("Username,Password,FirstName,LastName,PhoneNumber,BuildingId,RoomId,StartDate")] CreateTenantViewModel model)
        {
            try
            {
                _logger.LogInformation("Starting AddTenant action with data: FirstName={FirstName}, LastName={LastName}, Username={Username}, RoomId={RoomId}, StartDate={StartDate}",
                    model.FirstName, model.LastName, model.Username, model.RoomId, model.StartDate.ToString("yyyy-MM-dd"));

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state when adding tenant");
                    ViewBag.Buildings = await _context.Buildings.ToListAsync();
                    return View(model);
                }

                // Validate and update the room
                var room = await _context.Rooms
                    .Include(r => r.CurrentTenant)
                    .Include(r => r.Floor)
                    .FirstOrDefaultAsync(r => r.RoomId == model.RoomId);

                if (room == null)
                {
                    _logger.LogError("Room with ID {RoomId} not found", model.RoomId);
                    ModelState.AddModelError("RoomId", "Selected room not found.");
                    ViewBag.Buildings = await _context.Buildings.ToListAsync();
                    return View(model);
                }

                if (room.IsOccupied || room.CurrentTenant != null)
                {
                    _logger.LogWarning("Room {RoomId} is already occupied", model.RoomId);
                    ModelState.AddModelError("RoomId", "This room is already occupied.");
                    ViewBag.Buildings = await _context.Buildings.ToListAsync();
                    return View(model);
                }

                // Create the user
                var user = new ApplicationUser
                {
                    UserName = model.Username,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    PhoneNumber = model.PhoneNumber,
                    RoomId = model.RoomId,
                    BuildingId = room.Floor?.BuildingId,
                    StartDate = model.StartDate
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Tenant");
                    _logger.LogInformation("User created successfully with ID {UserId}, StartDate {StartDate}", user.Id, user.StartDate.ToString("yyyy-MM-dd"));

                    // Update the room
                    room.IsOccupied = true;
                    room.CurrentTenant = user;
                    _context.Update(room);
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Room {RoomId} updated with tenant {UserId}", room.RoomId, user.Id);

                    // Generate initial bill for the tenant
                    await GenerateInitialBillForTenant(user.Id);
                    _logger.LogInformation("Initial bill generated for tenant {UserId}", user.Id);

                    TempData["Success"] = "Tenant added successfully!";
                    return RedirectToAction(nameof(ManageTenants));
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding tenant: {ErrorMessage}", ex.Message);
                ModelState.AddModelError("", "Error adding tenant. Please try again.");
            }

            ViewBag.Buildings = await _context.Buildings.ToListAsync();
            return View(model);
        }

        // Helper method to generate initial bill for a new tenant
        private async Task GenerateInitialBillForTenant(string tenantId)
        {
            try
            {
                // Use BillingService for initial bill (ensures fallback logic)
                await _billingService.GenerateInitialBillForTenantAsync(tenantId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating initial bill for tenant {TenantId}: {ErrorMessage}", tenantId, ex.Message);
            }
        }

        public async Task<IActionResult> ManageTenants(int? buildingId = null)
        {
            try
            {
                var tenantUsers = await _userManager.GetUsersInRoleAsync("Tenant");
                var tenantIds = tenantUsers.Where(t => !t.IsArchived).Select(t => t.Id).ToList();
                var tenantsWithDetailsQuery = _context.Users
                    .Where(u => tenantIds.Contains(u.Id))
                    .Include(u => u.CurrentRoom)
                    .ThenInclude(r => r.Floor)
                    .ThenInclude(f => f.Building)
                    .AsQueryable();
                if (buildingId.HasValue)
                {
                    tenantsWithDetailsQuery = tenantsWithDetailsQuery.Where(t => t.BuildingId == buildingId.Value);
                }
                var tenantsWithDetails = await tenantsWithDetailsQuery.ToListAsync();
                var now = DateTime.Now;
                var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);
                var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
                var tenantsWithMissingReadings = tenantsWithDetails
                    .Where(t => t.CurrentRoom != null && !_context.MeterReadings.Any(m => m.TenantId == t.Id && m.ReadingDate >= firstDayOfMonth && m.ReadingDate <= lastDayOfMonth))
                    .Select(t => new { TenantName = t.FirstName + " " + t.LastName, RoomNumber = t.CurrentRoom.RoomNumber })
                    .ToList();
                var buildings = await _context.Buildings.ToListAsync();
                ViewBag.Buildings = buildings;
                ViewBag.CurrentBuildingId = buildingId;
                ViewBag.MissingReadings = tenantsWithMissingReadings;
                return View(tenantsWithDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ManageTenants: {Message}", ex.Message);
                TempData["Error"] = "An error occurred while loading tenants. Please try again.";
                return RedirectToAction("Index");
            }
        }

        // Archive a tenant (set IsArchived = true)
        [HttpPost]
        public async Task<IActionResult> ArchiveTenant(string id)
        {
            var tenant = await _userManager.FindByIdAsync(id);
            if (tenant == null)
                return Json(new { success = false, message = "Tenant not found." });

            // Unassign tenant from room and building, and mark room as vacant
            if (tenant.RoomId.HasValue)
            {
                var room = await _context.Rooms.Include(r => r.CurrentTenant).FirstOrDefaultAsync(r => r.RoomId == tenant.RoomId.Value);
                if (room != null)
                {
                    room.IsOccupied = false;
                    room.CurrentTenant = null;
                    _context.Update(room);
                }
                tenant.RoomId = null;
            }
            tenant.BuildingId = null;
            tenant.IsArchived = true;
            await _userManager.UpdateAsync(tenant);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // Restore a tenant (set IsArchived = false)
        [HttpPost]
        public async Task<IActionResult> RestoreTenant(string id)
        {
            var tenant = await _userManager.FindByIdAsync(id);
            if (tenant == null)
                return Json(new { success = false, message = "Tenant not found." });
            tenant.IsArchived = false;
            await _userManager.UpdateAsync(tenant);
            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateBills()
        {
            await _billingService.GenerateMonthlyBillsAsync(DateTime.Now);
            TempData["Success"] = "Monthly bills generated successfully!";
            return RedirectToAction("ManageTenants");
        }

        public async Task<IActionResult> MeterReadings()
        {
            var tenantsWithRooms = await _context.Users
                .Where(u => u.RoomId.HasValue)
                .Include(u => u.CurrentRoom!)
                    .ThenInclude(r => r.Floor!)
                        .ThenInclude(f => f.Building)
                .ToListAsync();
            ViewBag.Tenants = tenantsWithRooms;
            // Build a dictionary of rates per tenant
            var tenantRates = tenantsWithRooms.ToDictionary(
                t => t.Id,
                t => t.CurrentRoom?.CustomElectricityFee ?? t.CurrentRoom?.Floor?.Building?.DefaultElectricityFee ?? 0
            );
            ViewBag.TenantRates = tenantRates;
            // Load the 20 most recent meter readings
            var recentReadings = await _context.MeterReadings
                .Include(m => m.Tenant)
                .Include(m => m.Room)
                    .ThenInclude(r => r.Floor!)
                        .ThenInclude(f => f.Building)
                .OrderByDescending(m => m.ReadingDate)
                .Take(20)
                .ToListAsync();
            return View(recentReadings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MeterReadings(string tenantId, decimal currentReading, DateTime readingDate, string? notes)
        {
            // Check if a reading already exists for this tenant in the selected month
            var firstDayOfMonth = new DateTime(readingDate.Year, readingDate.Month, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
            var existingReading = await _context.MeterReadings
                .AnyAsync(m => m.TenantId == tenantId && m.ReadingDate >= firstDayOfMonth && m.ReadingDate <= lastDayOfMonth);
            if (existingReading)
            {
                TempData["ErrorMessage"] = "A meter reading already exists for this tenant in the selected month.";
                return RedirectToAction("MeterReadings");
            }
            // Add the new reading
            var tenant = await _context.Users
                .Include(u => u.CurrentRoom)
                    .ThenInclude(r => r.Floor)
                        .ThenInclude(f => f.Building)
                .FirstOrDefaultAsync(u => u.Id == tenantId);
            if (tenant == null || tenant.CurrentRoom == null)
            {
                TempData["ErrorMessage"] = "Selected tenant or room not found.";
                return RedirectToAction("MeterReadings");
            }
            // Find the most recent previous reading for this tenant
            var previousReadingEntity = await _context.MeterReadings
                .Where(m => m.TenantId == tenantId)
                .OrderByDescending(m => m.ReadingDate)
                .FirstOrDefaultAsync();
            var reading = new MeterReading
            {
                TenantId = tenantId,
                RoomId = tenant.CurrentRoom.RoomId,
                ReadingDate = readingDate,
                CurrentReading = currentReading,
                PreviousReading = previousReadingEntity != null ? previousReadingEntity.CurrentReading : (decimal?)null,
                RatePerKwh = tenant.CurrentRoom.CustomElectricityFee ?? tenant.CurrentRoom.Floor.Building.DefaultElectricityFee,
                Notes = notes
            };
            _context.MeterReadings.Add(reading);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Meter reading recorded successfully.";
            return RedirectToAction("MeterReadings");
        }

        public IActionResult Reports()
        {
            return View();
        }

        public IActionResult AddBuilding()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> AddBuilding(Building building)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Buildings.Add(building);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Building added successfully!";
                    return RedirectToAction(nameof(ManageBuildings));
                }
                catch (Exception)
                {
                    ModelState.AddModelError("", "Error adding building. Please try again.");
                }
            }
            return View(building);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBuilding(int id)
        {
            try
            {
                _logger.LogInformation("Starting deletion of building with ID {0}", id);
                // First, load the building with its related entities
                var building = await _context.Buildings
                    .Include("Floors.Rooms.CurrentTenant") // Using string-based include to avoid null reference errors
                    .Include(b => b.Tenants)
                    .FirstOrDefaultAsync(b => b.BuildingId == id);

                if (building == null)
                {
                    _logger.LogWarning("Building with ID {0} not found", id);
                    return Json(new { success = false, message = "Building not found." });
                }

                _logger.LogInformation("Building {0} found with {1} floors, {2} rooms, and {3} tenants",
                    building.BuildingName,
                    building.Floors?.Count ?? 0,
                    building.Floors?.Sum(f => f.Rooms?.Count ?? 0) ?? 0,
                    building.Tenants?.Count ?? 0);

                // Using a single transaction for deletion
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // 1. Clear tenant associations with this building
                        var usersToUpdate = await _context.Users
                            .Where(u => u.BuildingId == id)
                            .ToListAsync();

                        foreach (var user in usersToUpdate)
                        {
                            _logger.LogInformation("Detaching user {0} from building {1}", user.Id, id);
                            user.BuildingId = null;
                            _context.Update(user);
                        }
                        await _context.SaveChangesAsync();

                        // 2. Clear CurrentTenant references and IsOccupied flags on all rooms
                        if (building.Floors != null)
                        {
                            foreach (var floor in building.Floors)
                            {
                                if (floor.Rooms != null)
                                {
                                    foreach (var room in floor.Rooms.Where(r => r.CurrentTenant != null).ToList())
                                    {
                                        if (room.CurrentTenant != null)
                                        {
                                            room.CurrentTenant.RoomId = null;
                                            _context.Update(room.CurrentTenant);
                                        }
                                        room.CurrentTenant = null;
                                        room.TenantId = null;
                                        room.IsOccupied = false;
                                        _context.Update(room);
                                    }
                                }
                            }
                            await _context.SaveChangesAsync();
                        }

                        // 3. Get all room IDs in this building
                        var roomIds = building.Floors?
                            .SelectMany(f => f.Rooms ?? Enumerable.Empty<Room>())
                            .Select(r => r.RoomId)
                            .ToList() ?? new List<int>();

                        // 4. Delete all bills associated with these rooms
                        if (roomIds.Any())
                        {
                            var billsToDelete = await _context.Bills
                                .Where(b => roomIds.Contains(b.RoomId))
                                .ToListAsync();

                            if (billsToDelete.Any())
                            {
                                _logger.LogInformation("Deleting {0} bills", billsToDelete.Count);

                                // 5. Delete all payments associated with these bills
                                var billIds = billsToDelete.Select(b => (int?)b.BillId).ToList();
                                var paymentsToDelete = await _context.Payments
                                    .Where(p => billIds.Contains(p.BillId))
                                    .ToListAsync();

                                if (paymentsToDelete.Any())
                                {
                                    _logger.LogInformation("Deleting {0} payments", paymentsToDelete.Count);
                                    _context.Payments.RemoveRange(paymentsToDelete);
                                    await _context.SaveChangesAsync();
                                }

                                _context.Bills.RemoveRange(billsToDelete);
                                await _context.SaveChangesAsync();
                            }

                            // 6. Delete all contracts associated with these rooms
                            var contractsToDelete = await _context.Contracts
                                .Where(c => roomIds.Contains(c.RoomId))
                                .ToListAsync();

                            if (contractsToDelete.Any())
                            {
                                _logger.LogInformation("Deleting {0} contracts", contractsToDelete.Count);
                                _context.Contracts.RemoveRange(contractsToDelete);
                                await _context.SaveChangesAsync();
                            }

                            // 7. Delete all meter readings associated with these rooms
                            var meterReadingsToDelete = await _context.MeterReadings
                                .Where(m => roomIds.Contains(m.RoomId))
                                .ToListAsync();

                            if (meterReadingsToDelete.Any())
                            {
                                _logger.LogInformation("Deleting {0} meter readings", meterReadingsToDelete.Count);
                                _context.MeterReadings.RemoveRange(meterReadingsToDelete);
                                await _context.SaveChangesAsync();
                            }
                        }

                        // 8. For each floor, clear the rooms collection first
                        if (building.Floors != null)
                        {
                            foreach (var floor in building.Floors)
                            {
                                if (floor.Rooms != null && floor.Rooms.Any())
                                {
                                    _context.Rooms.RemoveRange(floor.Rooms);
                                }
                            }
                            await _context.SaveChangesAsync();

                            // 9. Now remove all floors
                            _context.Floors.RemoveRange(building.Floors);
                            await _context.SaveChangesAsync();
                        }

                        // 10. Finally delete the building
                        _context.Buildings.Remove(building);
                        await _context.SaveChangesAsync();

                        await transaction.CommitAsync();
                        _logger.LogInformation("Building {0} successfully deleted", building.BuildingName);
                        return Json(new { success = true, message = "Building successfully deleted" });
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError("Error deleting building: " + ex.Message);
                        return Json(new { success = false, message = "Error deleting building: " + ex.Message });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Unexpected error deleting building: " + ex.Message);
                return Json(new { success = false, message = "Unexpected error deleting building: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForceDeleteBuilding(int id)
        {
            try
            {
                _logger.LogInformation("Starting force deletion of building with ID {0}", id);

                // Direct database operations to clean up related entities
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // 1. Get all rooms in the building via floors
                        var floorsInBuilding = await _context.Floors
                            .Where(f => f.BuildingId == id)
                            .ToListAsync();

                        var floorIds = floorsInBuilding.Select(f => f.FloorId).ToList();

                        var roomsInBuilding = await _context.Rooms
                            .Where(r => floorIds.Contains(r.FloorId))
                            .ToListAsync();

                        var roomIds = roomsInBuilding.Select(r => r.RoomId).ToList();

                        // 2. Delete all payments associated with bills for these rooms
                        var billsForRooms = await _context.Bills
                            .Where(b => roomIds.Contains(b.RoomId))
                            .ToListAsync();

                        var billIds = billsForRooms.Select(b => b.BillId).ToList();

                        var paymentsToDelete = await _context.Payments
                            .Where(p => billIds.Contains(p.BillId))
                            .ToListAsync();

                        if (paymentsToDelete.Any())
                        {
                            _logger.LogInformation("Force deleting {0} payments", paymentsToDelete.Count);
                            _context.Payments.RemoveRange(paymentsToDelete);
                            await _context.SaveChangesAsync();
                        }

                        // 3. Delete all bills associated with these rooms
                        if (billsForRooms.Any())
                        {
                            _logger.LogInformation("Force deleting {0} bills", billsForRooms.Count);
                            _context.Bills.RemoveRange(billsForRooms);
                            await _context.SaveChangesAsync();
                        }

                        // 4. Delete all contracts associated with these rooms
                        var contractsToDelete = await _context.Contracts
                            .Where(c => roomIds.Contains(c.RoomId))
                            .ToListAsync();

                        if (contractsToDelete.Any())
                        {
                            _logger.LogInformation("Force deleting {0} contracts", contractsToDelete.Count);
                            _context.Contracts.RemoveRange(contractsToDelete);
                            await _context.SaveChangesAsync();
                        }

                        // 5. Try to delete meter readings - handle if table doesn't exist
                        try
                        {
                            var meterReadingsExist = await _context.Database.ExecuteSqlRawAsync("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MeterReadings'") > 0;

                            if (meterReadingsExist)
                            {
                                var meterReadingsToDelete = await _context.MeterReadings
                                    .Where(m => roomIds.Contains(m.RoomId))
                                    .ToListAsync();

                                if (meterReadingsToDelete.Any())
                                {
                                    _logger.LogInformation("Force deleting {0} meter readings", meterReadingsToDelete.Count);
                                    _context.MeterReadings.RemoveRange(meterReadingsToDelete);
                                    await _context.SaveChangesAsync();
                                }
                            }
                            else
                            {
                                _logger.LogWarning("MeterReadings table does not exist - skipping meter readings deletion");
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log the error but continue with the deletion process
                            _logger.LogWarning("Error accessing MeterReadings table: {0}. Continuing deletion process.", ex.Message);
                        }

                        // 6. Force update any users that reference this building or rooms
                        var usersToUpdate = await _context.Users
                            .Where(u => u.BuildingId == id || (u.RoomId.HasValue && roomIds.Contains(u.RoomId.Value)))
                            .ToListAsync();

                        foreach (var user in usersToUpdate)
                        {
                            _logger.LogWarning("Force detaching user {0} from building {1}", user.Id, id);
                            user.BuildingId = null;
                            user.RoomId = null;
                            _context.Update(user);
                        }
                        await _context.SaveChangesAsync();

                        // 7. Delete all rooms
                        if (roomsInBuilding.Any())
                        {
                            _logger.LogInformation("Force deleting {0} rooms", roomsInBuilding.Count);
                            _context.Rooms.RemoveRange(roomsInBuilding);
                            await _context.SaveChangesAsync();
                        }

                        // 8. Delete all floors
                        if (floorsInBuilding.Any())
                        {
                            _logger.LogInformation("Force deleting {0} floors", floorsInBuilding.Count);
                            _context.Floors.RemoveRange(floorsInBuilding);
                            await _context.SaveChangesAsync();
                        }

                        // 9. Finally delete the building
                        var building = await _context.Buildings.FindAsync(id);
                        if (building != null)
                        {
                            _logger.LogInformation("Force deleting building {0}", building.BuildingName);
                            _context.Buildings.Remove(building);
                            await _context.SaveChangesAsync();

                            // Commit transaction
                            await transaction.CommitAsync();
                            return Json(new { success = true, message = "Building and all related data successfully deleted" });
                        }
                        else
                        {
                            _logger.LogWarning("Building {0} not found for force deletion", id);
                            return Json(new { success = false, message = "Building not found" });
                        }
                    }
                    catch (Exception ex)
                    {
                        // Rollback transaction on error
                        await transaction.RollbackAsync();
                        _logger.LogError("Error in force delete: {0}", ex.Message);
                        return Json(new { success = false, message = "Error deleting building: " + ex.Message });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Critical error in ForceDeleteBuilding: {0}", ex.Message);
                return Json(new { success = false, message = "Critical error: " + ex.Message });
            }
        }

        public async Task<IActionResult> EditBuilding(int id)
        {
            var building = await _context.Buildings.FindAsync(id);
            if (building == null)
            {
                return NotFound();
            }
            return View(building);
        }

        [HttpPost]
        public async Task<IActionResult> EditBuilding(int id, Building building)
        {
            if (id != building.BuildingId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingBuilding = await _context.Buildings.AsNoTracking().FirstOrDefaultAsync(b => b.BuildingId == id);
                    if (existingBuilding == null)
                    {
                        return NotFound();
                    }

                    // Check if any changes were actually made
                    if (existingBuilding.BuildingName == building.BuildingName &&
                        existingBuilding.Address == building.Address &&
                        existingBuilding.DefaultMonthlyRent == building.DefaultMonthlyRent &&
                        existingBuilding.DefaultWaterFee == building.DefaultWaterFee &&
                        existingBuilding.DefaultElectricityFee == building.DefaultElectricityFee &&
                        existingBuilding.DefaultWifiFee == building.DefaultWifiFee &&
                        existingBuilding.LateFee == building.LateFee)
                    {
                        // No changes were made
                        TempData["Info"] = "No changes were made to the building.";
                        return RedirectToAction(nameof(ManageBuildings));
                    }

                    _context.Update(building);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Building updated successfully!";
                    return RedirectToAction(nameof(ManageBuildings));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Buildings.AnyAsync(b => b.BuildingId == id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        ModelState.AddModelError("", "Error updating building. Please try again.");
                    }
                }
            }
            return View(building);
        }

        public async Task<IActionResult> EditTenant(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var tenant = await _userManager.FindByIdAsync(id);
            if (tenant == null)
            {
                return NotFound();
            }

            var isTenant = await _userManager.IsInRoleAsync(tenant, "Tenant");
            if (!isTenant)
            {
                return NotFound();
            }

            // Do not overwrite tenant.StartDate; just use the value from the database

            return View(tenant);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditTenant(
            string Id,
            string FirstName,
            string LastName,
            string UserName,
            string Password,
            string ConfirmPassword,
            string PhoneNumber,
            string StartDate)
        {
            var tenant = await _userManager.FindByIdAsync(Id);
            if (tenant == null)
                return Json(new { success = false, message = "Tenant not found." });
            if (string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName) || string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(PhoneNumber) || string.IsNullOrWhiteSpace(StartDate))
                return Json(new { success = false, message = "All fields are required." });
            tenant.FirstName = FirstName;
            tenant.LastName = LastName;
            tenant.UserName = UserName;
            tenant.NormalizedUserName = UserName.ToUpperInvariant();
            tenant.PhoneNumber = PhoneNumber;
            if (DateTime.TryParse(StartDate, out var parsedDate))
                tenant.StartDate = parsedDate;
            // Change password if provided
            if (!string.IsNullOrWhiteSpace(Password) || !string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                if (Password != ConfirmPassword)
                    return Json(new { success = false, message = "Passwords do not match." });
                var token = await _userManager.GeneratePasswordResetTokenAsync(tenant);
                var result = await _userManager.ResetPasswordAsync(tenant, token, Password);
                if (!result.Succeeded)
                    return Json(new { success = false, message = string.Join("; ", result.Errors.Select(e => e.Description)) });
            }
            var updateResult = await _userManager.UpdateAsync(tenant);
            if (updateResult.Succeeded)
                return Json(new { success = true, message = "Tenant updated successfully." });
            return Json(new { success = false, message = string.Join("; ", updateResult.Errors.Select(e => e.Description)) });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteTenant(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return Json(new { success = false, message = "Invalid tenant ID." });
            }

            try
            {
                var tenant = await _userManager.FindByIdAsync(id);
                if (tenant == null)
                {
                    return Json(new { success = false, message = "Tenant not found." });
                }

                var isTenant = await _userManager.IsInRoleAsync(tenant, "Tenant");
                if (!isTenant && !tenant.IsArchived)
                {
                    return Json(new { success = false, message = "User is not a tenant or archived." });
                }

                // Delete notifications related to the tenant's bills
                var bills = await _context.Bills.Where(b => b.TenantId == id).ToListAsync();
                var billIds = bills.Select(b => (int?)b.BillId).ToList();
                var notifications = await _context.Notifications.Where(n => billIds.Contains(n.BillId)).ToListAsync();
                _context.Notifications.RemoveRange(notifications);
                // Delete bills
                _context.Bills.RemoveRange(bills);

                // Delete contracts
                var contracts = await _context.Contracts.Where(c => c.TenantId == id).ToListAsync();
                _context.Contracts.RemoveRange(contracts);

                // Delete meter readings for the tenant
                var tenantMeterReadings = await _context.MeterReadings.Where(m => m.TenantId == id).ToListAsync();
                _context.MeterReadings.RemoveRange(tenantMeterReadings);

                // Save changes
                await _context.SaveChangesAsync();

                // Update the room
                if (tenant.RoomId.HasValue)
                {
                    var room = await _context.Rooms.FindAsync(tenant.RoomId.Value);
                    if (room != null)
                    {
                        room.IsOccupied = false;
                        room.CurrentTenant = null;
                        _context.Update(room);
                        await _context.SaveChangesAsync();
                    }
                }

                // Delete the tenant
                var result = await _userManager.DeleteAsync(tenant);
                if (result.Succeeded)
                {
                    return Json(new { success = true });
                }

                var errorMsg = string.Join("; ", result.Errors.Select(e => e.Description));
                _logger.LogError("DeleteAsync failed: " + errorMsg);
                return Json(new { success = false, message = "Error deleting tenant: " + errorMsg });
            }
            catch (Exception ex)
            {
                _logger.LogError("Error deleting tenant: " + ex.ToString());
                return Json(new { success = false, message = "Error deleting tenant: " + ex.Message });
            }
        }

        public async Task<IActionResult> BuildingDetails(int id)
        {
            try
            {
                var building = await _context.Buildings
                    .Include("Floors.Rooms") // Using string-based include to avoid null reference errors
                    .FirstOrDefaultAsync(b => b.BuildingId == id);

                if (building == null)
                {
                    return NotFound();
                }

                return View(building);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading building details for BuildingId={0}: {1}", id, ex.Message);
                TempData["Error"] = "An error occurred while loading the building details.";
                return RedirectToAction(nameof(ManageBuildings));
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetFloors(int id)
        {
            _logger.LogInformation("GetFloors called with id={0}", id);

            var floors = await _context.Floors
                .Where(f => f.BuildingId == id)
                .OrderBy(f => f.FloorNumber)
                .Select(f => new
                {
                    FloorId = f.FloorId,
                    displayName = $"{f.FloorName} (Floor {f.FloorNumber})"
                })
                .ToListAsync();

            _logger.LogInformation("Found {0} floors for building {1}", floors.Count, id);

            return Json(new { success = true, data = floors });
        }

        [HttpPost]
        [Authorize(Roles = "Landlord")]
        public async Task<IActionResult> ResetDatabase()
        {
            try
            {
                _logger.LogWarning("Database reset initiated by landlord");

                // Use a transaction to ensure all-or-nothing behavior
                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // Step 1: Clear meter readings
                        var meterReadings = await _context.MeterReadings.ToListAsync();
                        _context.MeterReadings.RemoveRange(meterReadings);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Cleared {0} meter readings", meterReadings.Count);

                        // Step 2: Clear bills
                        var bills = await _context.Bills.ToListAsync();
                        _context.Bills.RemoveRange(bills);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Cleared {0} bills", bills.Count);

                        // Step 3: Clear payments
                        var payments = await _context.Payments.ToListAsync();
                        _context.Payments.RemoveRange(payments);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Cleared {0} payments", payments.Count);

                        // Step 4: Clear contracts
                        var contracts = await _context.Contracts.ToListAsync();
                        _context.Contracts.RemoveRange(contracts);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Cleared {0} contracts", contracts.Count);

                        // Step 5: Clear tenant references from rooms and users
                        var rooms = await _context.Rooms.ToListAsync();
                        foreach (var room in rooms)
                        {
                            room.IsOccupied = false;
                            room.CurrentTenant = null;
                        }
                        _context.UpdateRange(rooms);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Cleared tenant references from {0} rooms", rooms.Count);

                        // Step 6: Clear room and building references from users
                        var users = await _context.Users.ToListAsync();
                        foreach (var user in users)
                        {
                            user.RoomId = null;
                            user.BuildingId = null;
                        }
                        _context.UpdateRange(users);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Cleared room and building references from {0} users", users.Count);

                        // Step 7: Delete all rooms
                        _context.Rooms.RemoveRange(rooms);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Deleted {0} rooms", rooms.Count);

                        // Step 8: Delete all floors
                        var floors = await _context.Floors.ToListAsync();
                        _context.Floors.RemoveRange(floors);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Deleted {0} floors", floors.Count);

                        // Step 9: Delete all buildings
                        var buildings = await _context.Buildings.ToListAsync();
                        _context.Buildings.RemoveRange(buildings);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Deleted {0} buildings", buildings.Count);

                        // Step 10: Delete all tenant users
                        var tenantRoleId = (await _context.Roles.FirstOrDefaultAsync(r => r.Name == "Tenant"))?.Id;
                        if (tenantRoleId != null)
                        {
                            var tenantUserIds = await _context.UserRoles
                                .Where(ur => ur.RoleId == tenantRoleId)
                                .Select(ur => ur.UserId)
                                .ToListAsync();

                            foreach (var userId in tenantUserIds)
                            {
                                var user = await _userManager.FindByIdAsync(userId);
                                if (user != null)
                                {
                                    await _userManager.DeleteAsync(user);
                                }
                            }
                            await _context.SaveChangesAsync();
                            _logger.LogInformation("Deleted {0} tenant users", tenantUserIds.Count);
                        }

                        // Commit the transaction
                        await transaction.CommitAsync();
                        TempData["Success"] = "Database reset successfully. All buildings, floors, rooms, tenants, and related data have been removed.";
                        return RedirectToAction(nameof(Index));
                    }
                    catch (Exception ex)
                    {
                        // Rollback the transaction if there's an error
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Error during database reset: {Message}", ex.Message);
                        TempData["Error"] = "An error occurred while resetting the database: " + ex.Message;
                        return RedirectToAction(nameof(Index));
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during database reset: {Message}", ex.Message);
                TempData["Error"] = "A critical error occurred: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: /Landlord/TenantBills/id
        public async Task<IActionResult> TenantBills(string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    _logger.LogWarning("TenantBills called with null or empty id");
                    TempData["Error"] = "Tenant ID is required";
                    return RedirectToAction(nameof(ManageTenants));
                }
                // Find tenant by ID
                var tenant = await _context.Users
                    .Include("Room.Floor.Building") // Using string-based include to avoid null reference errors
                    .FirstOrDefaultAsync(u => u.Id == id);

                if (tenant == null)
                {
                    _logger.LogWarning("Tenant with ID {0} not found", id);
                    TempData["Error"] = "Tenant not found";
                    return RedirectToAction(nameof(ManageTenants));
                }

                // Get all bills for this tenant
                var bills = await _context.Bills
                    .Include(b => b.Room)
                    .Where(b => b.TenantId == id)
                    .OrderByDescending(b => b.BillingYear)
                    .ThenByDescending(b => b.BillingMonth)
                    .ToListAsync();

                // Calculate total amount due and unpaid months
                var unpaidBills = bills
                    .Where(b => b.Status != BillStatus.Paid && b.Status != BillStatus.Cancelled && b.Status != BillStatus.WrittenOff)
                    .ToList();

                decimal totalDue = unpaidBills.Sum(b => b.TotalAmount);

                // Group bills by month and year for the view
                var billsByMonth = bills
                    .GroupBy(b => new { b.BillingMonth, b.BillingYear })
                    .ToDictionary(g => g.Key, g => g.ToList());

                // Count distinct unpaid months
                var unpaidMonths = unpaidBills
                    .Select(b => new { b.BillingMonth, b.BillingYear })
                    .Distinct()
                    .Count();

                // Pass data to view
                ViewBag.Tenant = tenant;
                ViewBag.BillsByMonth = billsByMonth;
                ViewBag.TotalDue = totalDue;
                ViewBag.UnpaidMonths = unpaidMonths;

                return View(bills);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TenantBills action: {0}", ex.Message);
                TempData["Error"] = "An error occurred while loading tenant bills.";
                return RedirectToAction(nameof(ManageTenants));
            }
        }

        [HttpGet]
        public async Task<IActionResult> GeneratePastBillsForTenant(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }
            var tenant = await _context.Users
                .Include("Room.Floor.Building") // Using string-based include to avoid null reference errors
                .FirstOrDefaultAsync(u => u.Id == id);

            if (tenant == null || tenant.CurrentRoom == null)
            {
                TempData["Error"] = "Tenant not found or has no room assigned.";
                return RedirectToAction("ManageTenants");
            }

            try
            {
                // Get the tenant's start date
                var startDate = tenant.StartDate;

                // Get today's date
                var today = DateTime.Today;

                // Get existing bills to avoid duplicates
                var existingBills = await _context.Bills
                    .Where(b => b.TenantId == id)
                    .Select(b => new { b.BillingMonth, b.BillingYear })
                    .ToListAsync();

                var existingBillMonths = existingBills
                    .Select(b => new { b.BillingMonth, b.BillingYear })
                    .ToHashSet();

                // Generate bills from start date until now (one per month)
                var currentDate = new DateTime(startDate.Year, startDate.Month, 1);
                int billsGenerated = 0;

                while (currentDate <= today)
                {
                    // Check if bill already exists for this month/year
                    var monthKey = new { BillingMonth = currentDate.Month, BillingYear = currentDate.Year };

                    if (!existingBillMonths.Contains(monthKey))
                    {
                        // Calculate the due date (same day as start date)
                        var dueDate = new DateTime(currentDate.Year, currentDate.Month,
                            Math.Min(startDate.Day, DateTime.DaysInMonth(currentDate.Year, currentDate.Month)));
                        // Create the bill                        // Get building safely
                        var building = tenant.CurrentRoom.Floor.Building ?? new Building
                        {
                            DefaultMonthlyRent = 5000, // Default values if building not found
                            DefaultWaterFee = 300,
                            DefaultElectricityFee = 500,
                            DefaultWifiFee = 200,
                            LateFee = 5
                        };

                        var bill = new Bill
                        {
                            TenantId = tenant.Id,
                            RoomId = tenant.RoomId.Value,
                            BillingDate = currentDate,
                            BillingMonth = currentDate.Month,
                            BillingYear = currentDate.Year,
                            DueDate = dueDate,
                            MonthlyRent = tenant.CurrentRoom?.CustomMonthlyRent ?? building.DefaultMonthlyRent,
                            WaterFee = tenant.CurrentRoom?.CustomWaterFee ?? building.DefaultWaterFee,
                            ElectricityFee = 0,
                            WifiFee = tenant.CurrentRoom?.CustomWifiFee ?? building.DefaultWifiFee,
                            Status = BillStatus.NotPaid
                        };

                        // Add late fee for past due dates                        if (dueDate < today)
                        {
                            bill.Status = BillStatus.Overdue;
                            decimal lateFeePercentage = building.LateFee;
                            bill.LateFee = bill.TotalAmount * (lateFeePercentage / 100);
                        }

                        _context.Bills.Add(bill);
                        billsGenerated++;
                    }

                    // Move to next month
                    currentDate = currentDate.AddMonths(1);
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = $"Successfully generated {billsGenerated} past bills for {tenant.FirstName} {tenant.LastName}.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating past bills for tenant {TenantId}", id);
                TempData["Error"] = $"Error generating past bills: {ex.Message}";
            }

            return RedirectToAction("TenantBills", new { id });
        }

        // DEBUG: Check navigation property status for a tenant
        [HttpGet]
        public async Task<IActionResult> DebugTenantNav(string id)
        {
            var tenant = await _context.Users
                .Include(u => u.CurrentRoom)
                    .ThenInclude(r => r.Floor)
                        .ThenInclude(f => f.Building)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (tenant == null)
            {
                return Content($"Tenant not found for id: {id}");
            }

            var room = tenant.CurrentRoom;
            var floor = room?.Floor;
            var building = floor?.Building;

            string debugInfo = $@"Tenant: {tenant.FirstName} {tenant.LastName} (ID: {tenant.Id})\n" +
                $"RoomId: {tenant.RoomId}\n" +
                $"CurrentRoom: {(room != null ? room.RoomNumber : "null")}\n" +
                $"FloorId: {(room != null ? room.FloorId.ToString() : "null")}\n" +
                $"Floor: {(floor != null ? floor.FloorName : "null")}\n" +
                $"BuildingId: {(floor != null ? floor.BuildingId.ToString() : "null")}\n" +
                $"Building: {(building != null ? building.BuildingName : "null")}\n" +
                $"DefaultMonthlyRent: {(building != null ? building.DefaultMonthlyRent.ToString() : "null")}\n" +
                $"DefaultWaterFee: {(building != null ? building.DefaultWaterFee.ToString() : "null")}\n" +
                $"DefaultWifiFee: {(building != null ? building.DefaultWifiFee.ToString() : "null")}\n" +
                $"CustomMonthlyRent: {(room != null && room.CustomMonthlyRent.HasValue ? room.CustomMonthlyRent.Value.ToString() : "null")}\n" +
                $"CustomWaterFee: {(room != null && room.CustomWaterFee.HasValue ? room.CustomWaterFee.Value.ToString() : "null")}\n" +
                $"CustomWifiFee: {(room != null && room.CustomWifiFee.HasValue ? room.CustomWifiFee.Value.ToString() : "null")}\n";

            return Content(debugInfo, "text/plain");
        }

        // GET: /Landlord/TenantUnpaidCycles/id
        public async Task<IActionResult> TenantUnpaidCycles(string id)
        {
            _logger.LogInformation("TenantUnpaidCycles called with id={Id}", id);
            if (string.IsNullOrEmpty(id))
            {
                TempData["Error"] = "Tenant ID is required";
                return RedirectToAction(nameof(ManageTenants));
            }
            var tenant = await _context.Users
                .Include(u => u.CurrentRoom)
                    .ThenInclude(r => r.Floor)
                        .ThenInclude(f => f.Building)
                .FirstOrDefaultAsync(u => u.Id == id);
            if (tenant == null)
            {
                _logger.LogWarning("TenantUnpaidCycles: Tenant not found for id={Id}", id);
                TempData["Error"] = "Tenant not found";
                return RedirectToAction(nameof(ManageTenants));
            }
            var today = DateTime.Today;
            var bills = await _context.Bills
                .Include(b => b.Room)
                .Where(b => b.TenantId == id && b.Status != BillStatus.Paid && b.Status != BillStatus.Cancelled && b.Status != BillStatus.WrittenOff)
                .OrderByDescending(b => b.BillingYear)
                .ThenByDescending(b => b.BillingMonth)
                .ToListAsync();

            var displayBills = bills.OrderByDescending(b => b.DueDate).ToList();

            ViewBag.Tenant = tenant;
            return View("TenantUnpaidCycles", displayBills);
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
                return Json(new { success = false, message = "New passwords do not match." });
            }
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Json(new { success = false, message = "User not found." });
            var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
            if (result.Succeeded)
                return Json(new { success = true, message = "Password changed successfully." });
            var errorMsg = string.Join(" ", result.Errors.Select(e => e.Description));
            return Json(new { success = false, message = errorMsg });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProfile(string firstName, string lastName, string phoneNumber, string userName)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Json(new { success = false, message = "User not found." });
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(phoneNumber) || string.IsNullOrWhiteSpace(userName))
                return Json(new { success = false, message = "All fields are required." });
            user.FirstName = firstName;
            user.LastName = lastName;
            user.PhoneNumber = phoneNumber;
            user.UserName = userName;
            user.NormalizedUserName = userName.ToUpperInvariant();
            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
                return Json(new { success = true, message = "Profile updated successfully." });
            return Json(new { success = false, message = string.Join("; ", result.Errors.Select(e => e.Description)) });
        }

        public async Task<IActionResult> TenantDetails(string id)
        {
            if (string.IsNullOrEmpty(id))
                return NotFound();
            var tenant = await _context.Users
                .Include(u => u.CurrentRoom)
                .ThenInclude(r => r.Floor)
                .ThenInclude(f => f.Building)
                .FirstOrDefaultAsync(u => u.Id == id);
            if (tenant == null)
                return NotFound();
            // Optionally, load all buildings/floors/rooms for move modal
            ViewBag.Buildings = await _context.Buildings.Include(b => b.Floors).ThenInclude(f => f.Rooms).ToListAsync();
            return View(tenant);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveTenant(string tenantId, int buildingId, int floorId, int roomId, string startDate)
        {
            if (string.IsNullOrEmpty(tenantId))
                return Json(new { success = false, message = "Invalid tenant." });
            var tenant = await _context.Users.Include(u => u.CurrentRoom).FirstOrDefaultAsync(u => u.Id == tenantId);
            if (tenant == null)
                return Json(new { success = false, message = "Tenant not found." });
            var oldRoom = tenant.CurrentRoom;
            var newRoom = await _context.Rooms.Include(r => r.Floor).FirstOrDefaultAsync(r => r.RoomId == roomId);
            if (newRoom == null || newRoom.IsOccupied)
                return Json(new { success = false, message = "Selected room is not available." });
            // Update old room
            if (oldRoom != null)
            {
                oldRoom.IsOccupied = false;
                oldRoom.CurrentTenant = null;
                _context.Update(oldRoom);
            }
            // Update new room
            newRoom.IsOccupied = true;
            newRoom.CurrentTenant = tenant;
            _context.Update(newRoom);
            // Update tenant
            tenant.RoomId = newRoom.RoomId;
            tenant.BuildingId = newRoom.Floor?.BuildingId;
            if (!string.IsNullOrWhiteSpace(startDate) && DateTime.TryParse(startDate, out var parsedDate))
                tenant.StartDate = parsedDate;
            _context.Update(tenant);
            await _context.SaveChangesAsync();
            // Generate new bill for new room
            await _billingService.GenerateInitialBillForTenantAsync(tenant.Id);
            return Json(new { success = true, message = "Tenant moved and new bill generated." });
        }

        public async Task<IActionResult> MonthlyIncomeSummary(int year, int? buildingId)
        {
            var billsQuery = _context.Bills
                .Where(b => b.BillingYear == year);

            if (buildingId.HasValue)
                billsQuery = billsQuery.Where(b => b.Room.Floor.BuildingId == buildingId.Value);

            // Load into memory first so we can use computed properties
            var billList = await billsQuery.ToListAsync();

            var grouped = billList
                .GroupBy(b => b.BillingMonth)
                .Select(g => new MonthlyIncomeRow
                {
                    Year = year,
                    Month = g.Key,
                    Billed = g.Sum(b => b.TotalAmount),
                    Collected = g.Where(b => b.Status == BillStatus.Paid).Sum(b => b.TotalAmount),
                    Outstanding = g.Where(b => b.Status != BillStatus.Paid && b.Status != BillStatus.WrittenOff).Sum(b => b.TotalAmount),
                    Overdue = g.Where(b => b.Status == BillStatus.Overdue).Sum(b => b.TotalAmount),
                    WrittenOff = g.Where(b => b.Status == BillStatus.WrittenOff).Sum(b => b.TotalAmount)
                })
                .OrderBy(r => r.Month)
                .ToList();

            var totalWrittenOff = grouped.Sum(r => r.WrittenOff);
            var totalCollected = grouped.Sum(r => r.Collected);
            var totalIncome = totalCollected;

            var vm = new MonthlyIncomeSummaryViewModel
            {
                Rows = grouped,
                TotalBilled = grouped.Sum(r => r.Billed),
                TotalCollected = totalCollected,
                TotalOutstanding = grouped.Sum(r => r.Outstanding),
                TotalOverdue = grouped.Sum(r => r.Overdue),
                TotalWrittenOff = totalWrittenOff,
                TotalIncome = totalIncome,
                Year = year,
                BuildingId = buildingId
            };

            return View(vm);
        }

        public async Task<IActionResult> PaymentHistory(int? year, int? buildingId, string? tenantId)
        {
            var paymentsQuery = _context.Payments
                .Include(p => p.Bill)
                    .ThenInclude(b => b.Tenant)
                .Include(p => p.Bill)
                    .ThenInclude(b => b.Room)
                        .ThenInclude(r => r.Floor)
                            .ThenInclude(f => f.Building)
                .AsQueryable();

            if (year.HasValue)
                paymentsQuery = paymentsQuery.Where(p => p.PaymentDate.Year == year.Value);
            if (buildingId.HasValue)
                paymentsQuery = paymentsQuery.Where(p => p.Bill.Room.Floor.BuildingId == buildingId.Value);
            if (!string.IsNullOrEmpty(tenantId))
                paymentsQuery = paymentsQuery.Where(p => p.Bill.TenantId == tenantId);

            var payments = await paymentsQuery
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            var paymentRows = payments.Select(p => new PaymentHistoryRow
            {
                PaymentDate = p.PaymentDate,
                TenantName = p.Bill?.Tenant != null ? p.Bill.Tenant.FirstName + " " + p.Bill.Tenant.LastName : "",
                RoomName = p.Bill?.Room != null ? $"{p.Bill.Room.Floor?.Building?.BuildingName ?? ""} - {p.Bill.Room.RoomNumber}" : "",
                Amount = p.Amount,
                PaymentMethod = p.PaymentMethod ?? "",
                ReferenceNumber = p.ReferenceNumber ?? "",
                BillPeriod = p.Bill != null ? $"{System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(p.Bill.BillingMonth)} {p.Bill.BillingYear}" : "",
                Status = p.Bill?.Status.ToString() ?? ""
            }).ToList();

            var buildings = await _context.Buildings.ToListAsync();
            var tenants = await _context.Users
                .Where(u => u.RoomId != null)
                .ToListAsync();

            var vm = new PaymentHistoryViewModel
            {
                Payments = paymentRows,
                Year = year,
                BuildingId = buildingId,
                TenantId = tenantId,
                Buildings = buildings,
                Tenants = tenants
            };

            return View(vm);
        }

        public async Task<IActionResult> BillHistory(string? tenantId)
        {
            var tenants = await _context.Users
                .Where(u => u.RoomId != null)
                .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                .ToListAsync();

            var vm = new BillHistoryViewModel
            {
                TenantId = tenantId,
                Tenants = tenants,
                Bills = new List<BillHistoryRow>()
            };

            if (!string.IsNullOrEmpty(tenantId))
            {
                var bills = await _context.Bills
                    .Where(b => b.TenantId == tenantId)
                    .OrderByDescending(b => b.BillingYear)
                    .ThenByDescending(b => b.BillingMonth)
                    .ToListAsync();

                vm.Bills = bills.Select(b => new BillHistoryRow
                {
                    BillingPeriod = $"{System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(b.BillingMonth)} {b.BillingYear}",
                    DueDate = b.DueDate,
                    Rent = b.MonthlyRent,
                    Water = b.WaterFee,
                    Electricity = b.ElectricityFee,
                    Wifi = b.WifiFee,
                    LateFee = b.LateFee ?? 0,
                    Total = b.TotalAmount,
                    Status = b.Status.ToString(),
                    PaymentDate = b.PaymentDate,
                    Reference = b.PaymentReference,
                    Notes = b.Notes
                }).ToList();
            }

            return View(vm);
        }

        public async Task<IActionResult> ArchivedTenants()
        {
            var tenantUsers = await _userManager.GetUsersInRoleAsync("Tenant");
            var archivedTenantIds = tenantUsers.Where(t => t.IsArchived).Select(t => t.Id).ToList();
            var archivedTenants = await _context.Users
                .Where(u => archivedTenantIds.Contains(u.Id))
                .Include(u => u.CurrentRoom)
                .ThenInclude(r => r.Floor)
                .ThenInclude(f => f.Building)
                .ToListAsync();
            return View(archivedTenants);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> WriteOffTenantBills(string id)
        {
            var bills = await _context.Bills
                .Where(b => b.TenantId == id && (b.Status == BillStatus.NotPaid || b.Status == BillStatus.Pending || b.Status == BillStatus.Overdue))
                .ToListAsync();
            if (!bills.Any())
                return Json(new { success = false, message = "No unpaid bills to write off." });
            foreach (var bill in bills)
            {
                bill.Status = BillStatus.WrittenOff;
                bill.Notes = (bill.Notes ?? "") + "\nWritten off by landlord on " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> CheckUnpaidBills(string tenantId)
        {
            var bills = await _context.Bills
                .Where(b => b.TenantId == tenantId && (b.Status == BillStatus.NotPaid || b.Status == BillStatus.Pending || b.Status == BillStatus.Overdue))
                .ToListAsync();
            if (!bills.Any())
                return Json(new { hasUnpaid = false });
            var total = bills.Sum(b => b.TotalAmount);
            var periods = bills.Select(b => b.BillingPeriod).ToList();
            return Json(new { hasUnpaid = true, total, periods });
        }
    }
}
