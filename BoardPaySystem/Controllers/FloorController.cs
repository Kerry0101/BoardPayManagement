using BoardPaySystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoardPaySystem.Controllers
{
    [Authorize(Roles = "Landlord")]
    public class FloorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FloorController> _logger;

        public FloorController(ApplicationDbContext context, ILogger<FloorController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Floor/ManageFloors/5 (buildingId)
        public async Task<IActionResult> ManageFloors(int buildingId)
        {
            var building = await _context.Buildings
                .Include(b => b.Floors)
                .ThenInclude(f => f.Rooms)
                .FirstOrDefaultAsync(b => b.BuildingId == buildingId);

            if (building == null)
                return NotFound();

            ViewBag.Building = building;
            ViewBag.FloorsData = building.Floors.Select(f => new
            {
                f.FloorId,
                RoomCount = f.Rooms?.Count ?? 0
            }).ToList();

            return View(building.Floors);
        }

        // GET: Floor/AddFloor/5 (buildingId)
        public async Task<IActionResult> AddFloor(int buildingId)
        {
            var building = await _context.Buildings.FindAsync(buildingId);
            if (building == null)
            {
                return NotFound();
            }

            ViewBag.Building = building;
            return View(new Floor { BuildingId = buildingId });
        }

        // POST: Floor/AddFloor
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddFloor([Bind("FloorName,FloorNumber,BuildingId")] Floor floor)
        {
            _logger.LogInformation($"Attempting to add floor. BuildingId: {floor.BuildingId}, FloorName: {floor.FloorName}, FloorNumber: {floor.FloorNumber}");

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state when adding floor");
                ViewBag.Building = await _context.Buildings.FindAsync(floor.BuildingId);
                return View(floor);
            }

            // Check if floor number already exists in the building
            if (await _context.Floors
                .AnyAsync(f => f.BuildingId == floor.BuildingId && f.FloorNumber == floor.FloorNumber))
            {
                ModelState.AddModelError("FloorNumber", "This floor number already exists in the building");
                _logger.LogWarning($"Floor number {floor.FloorNumber} already exists in building {floor.BuildingId}");
                ViewBag.Building = await _context.Buildings.FindAsync(floor.BuildingId);
                return View(floor);
            }

            // Check if floor name already exists in the building
            if (await _context.Floors
                .AnyAsync(f => f.BuildingId == floor.BuildingId && f.FloorName.ToLower() == floor.FloorName.ToLower()))
            {
                ModelState.AddModelError("FloorName", "This floor name already exists in the building");
                _logger.LogWarning($"Floor name '{floor.FloorName}' already exists in building {floor.BuildingId}");
                ViewBag.Building = await _context.Buildings.FindAsync(floor.BuildingId);
                return View(floor);
            }

            try
            {
                _context.Add(floor);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Floor '{floor.FloorName}' added successfully with ID: {floor.FloorId}");
                TempData["Success"] = $"Floor '{floor.FloorName}' has been added successfully!";
                return RedirectToAction(nameof(ManageFloors), new { buildingId = floor.BuildingId });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error adding floor: {ex.Message}");
                ModelState.AddModelError("", "An error occurred while adding the floor. Please try again.");
                ViewBag.Building = await _context.Buildings.FindAsync(floor.BuildingId);
                return View(floor);
            }
        }

        // GET: Floor/EditFloor/5
        public async Task<IActionResult> EditFloor(int id)
        {
            var floor = await _context.Floors
                .Include(f => f.Building)
                .FirstOrDefaultAsync(f => f.FloorId == id);

            if (floor == null)
            {
                return NotFound();
            }

            ViewBag.Building = floor.Building;
            return View(floor);
        }

        // POST: Floor/EditFloor/5
        [HttpPost]
        public async Task<IActionResult> EditFloor(Floor floor)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(floor);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Floor updated successfully!";
                    return RedirectToAction(nameof(ManageFloors), new { buildingId = floor.BuildingId });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.Floors.AnyAsync(f => f.FloorId == floor.FloorId))
                    {
                        return NotFound();
                    }
                    throw;
                }
            }

            ViewBag.Building = await _context.Buildings.FindAsync(floor.BuildingId);
            return View(floor);
        }

        // POST: Floor/DeleteFloor/5
        [HttpPost]
        public async Task<IActionResult> DeleteFloor(int id)
        {
            // Start a transaction to ensure all operations complete together
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    _logger.LogInformation("Starting deletion of floor with ID {0}", id);

                    var floor = await _context.Floors
                        .Include(f => f.Rooms)
                            .ThenInclude(r => r.CurrentTenant)
                        .FirstOrDefaultAsync(f => f.FloorId == id);

                    if (floor == null)
                    {
                        return Json(new { success = false, message = "Floor not found." });
                    }

                    var buildingId = floor.BuildingId; // Save for redirect URL

                    // Check if floor has rooms
                    if (floor.Rooms != null && floor.Rooms.Any(r => r.IsOccupied))
                    {
                        return Json(new { success = false, message = $"Cannot delete floor '{floor.FloorName}'. There are occupied rooms on this floor. Please reassign tenants first." });
                    }

                    // Process each room in the floor
                    if (floor.Rooms != null && floor.Rooms.Any())
                    {
                        // Get room IDs for related data deletion
                        var roomIds = floor.Rooms.Select(r => r.RoomId).ToList();

                        // 1. Delete payments related to bills for these rooms
                        var billsForRooms = await _context.Bills
                            .Where(b => roomIds.Contains(b.RoomId))
                            .ToListAsync();

                        if (billsForRooms.Any())
                        {
                            var billIds = billsForRooms.Select(b => b.BillId).ToList();
                            var paymentsToDelete = await _context.Payments
                                .Where(p => billIds.Contains(p.BillId))
                                .ToListAsync();

                            if (paymentsToDelete.Any())
                            {
                                _logger.LogInformation("Deleting {0} payments related to floor {1}", paymentsToDelete.Count, id);
                                _context.Payments.RemoveRange(paymentsToDelete);
                                await _context.SaveChangesAsync();
                            }

                            // 2. Delete bills
                            _logger.LogInformation("Deleting {0} bills related to floor {1}", billsForRooms.Count, id);
                            _context.Bills.RemoveRange(billsForRooms);
                            await _context.SaveChangesAsync();
                        }

                        // 3. Delete contracts
                        var contractsToDelete = await _context.Contracts
                            .Where(c => roomIds.Contains(c.RoomId))
                            .ToListAsync();

                        if (contractsToDelete.Any())
                        {
                            _logger.LogInformation("Deleting {0} contracts related to floor {1}", contractsToDelete.Count, id);
                            _context.Contracts.RemoveRange(contractsToDelete);
                            await _context.SaveChangesAsync();
                        }

                        // 4. Try to delete meter readings if the table exists
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
                                    _logger.LogInformation("Deleting {0} meter readings related to floor {1}", meterReadingsToDelete.Count, id);
                                    _context.MeterReadings.RemoveRange(meterReadingsToDelete);
                                    await _context.SaveChangesAsync();
                                }
                            }
                            else
                            {
                                _logger.LogWarning("MeterReadings table does not exist - skipping meter readings deletion for floor {0}", id);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log error but continue with deletion process
                            _logger.LogWarning("Error checking/deleting meter readings: {0}. Continuing with floor deletion.", ex.Message);
                        }

                        // 5. Update any users that reference these rooms
                        foreach (var room in floor.Rooms.Where(r => r.CurrentTenant != null))
                        {
                            room.CurrentTenant.RoomId = null;
                            _context.Update(room.CurrentTenant);
                        }
                        await _context.SaveChangesAsync();

                        // 6. Remove all rooms
                        _logger.LogInformation("Deleting {0} rooms from floor {1}", floor.Rooms.Count, id);
                        _context.Rooms.RemoveRange(floor.Rooms);
                        await _context.SaveChangesAsync();
                    }

                    // 7. Finally delete the floor
                    _logger.LogInformation("Deleting floor: {0}", floor.FloorName);
                    _context.Floors.Remove(floor);
                    await _context.SaveChangesAsync();

                    // Commit the transaction
                    await transaction.CommitAsync();

                    return Json(new
                    {
                        success = true,
                        message = $"Floor '{floor.FloorName}' has been successfully deleted.",
                        redirectUrl = Url.Action("ManageFloors", new { buildingId })
                    });
                }
                catch (Exception ex)
                {
                    // Rollback the transaction on error
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error deleting floor. FloorId: {FloorId}, Error: {Error}", id, ex.Message);
                    return Json(new { success = false, message = "An error occurred while deleting the floor: " + ex.Message });
                }
            }
        }

        // GET: Floor/GetFloors/5 (buildingId)
        [HttpGet]
        public async Task<JsonResult> GetFloors(int id)
        {
            var floors = await _context.Floors
                .Where(f => f.BuildingId == id)
                .OrderBy(f => f.FloorNumber)
                .Select(f => new
                {
                    FloorId = f.FloorId,
                    displayName = $"{f.FloorName} (Floor {f.FloorNumber})"
                })
                .ToListAsync();

            return Json(new { success = true, data = floors });
        }
    }
}