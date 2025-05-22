using BoardPaySystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoardPaySystem.Controllers
{
    public class RoomController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<RoomController> _logger;

        public RoomController(ApplicationDbContext context, ILogger<RoomController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: Room/ManageRooms/5 (floorId)
        public async Task<IActionResult> ManageRooms(int floorId)
        {
            var floor = await _context.Floors
                .Include(f => f.Building)
                .Include(f => f.Rooms)
                    .ThenInclude(r => r.CurrentTenant)
                .FirstOrDefaultAsync(f => f.FloorId == floorId);

            if (floor == null)
            {
                _logger.LogWarning("Floor not found. FloorId: {FloorId}", floorId);
                return NotFound();
            }

            if (floor.Building == null)
            {
                _logger.LogWarning("Building not found for Floor. FloorId: {FloorId}", floorId);
                return NotFound();
            }

            ViewBag.Floor = floor;
            ViewBag.Building = floor.Building;
            return View(floor.Rooms?.OrderBy(r => r.RoomNumber).ToList() ?? new List<Room>());
        }

        // GET: Room/AddRoom/5 (floorId)
        public async Task<IActionResult> AddRoom(int floorId)
        {
            var floor = await _context.Floors
                .Include(f => f.Building)
                .FirstOrDefaultAsync(f => f.FloorId == floorId);

            if (floor == null || floor.Building == null)
            {
                _logger.LogWarning("Floor or Building not found. FloorId: {FloorId}", floorId);
                return NotFound();
            }

            ViewBag.Floor = floor;
            ViewBag.Building = floor.Building;
            var room = new Room
            {
                FloorId = floorId,
                IsOccupied = false
            };
            return View(room);
        }

        // POST: Room/AddRoom
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRoom([Bind("RoomNumber,Description,FloorId,CustomMonthlyRent,CustomWaterFee,CustomElectricityFee,CustomWifiFee")] Room room, bool useCustomFees = false)
        {
            try
            {
                _logger.LogInformation("Adding new room. FloorId: {FloorId}, RoomNumber: {RoomNumber}, UseCustomFees: {UseCustomFees}",
                    room?.FloorId, room?.RoomNumber?.ToString() ?? "null", useCustomFees);

                if (!ModelState.IsValid)
                {
                    _logger.LogWarning("Invalid model state when adding room");
                    // Collect all model errors for debugging
                    var allErrors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                    ViewBag.DebugModelErrors = allErrors;
                    var floor = await _context.Floors.Include(f => f.Building).FirstOrDefaultAsync(f => f.FloorId == room.FloorId);
                    ViewBag.Floor = floor;
                    ViewBag.Building = floor?.Building;
                    return View(room);
                }

                var floorExists = await _context.Floors.Include(f => f.Building).FirstOrDefaultAsync(f => f.FloorId == room.FloorId);
                if (floorExists == null)
                {
                    _logger.LogWarning("Floor not found. FloorId: {FloorId}", room.FloorId);
                    TempData["Error"] = "Floor not found.";
                    return RedirectToAction(nameof(ManageRooms), new { floorId = room.FloorId });
                }

                // Check if room number already exists on this floor
                if (await _context.Rooms.AnyAsync(r => r.FloorId == room.FloorId && r.RoomNumber == room.RoomNumber))
                {
                    ModelState.AddModelError("RoomNumber", "This room number already exists on this floor.");
                    ViewBag.Floor = floorExists;
                    ViewBag.Building = floorExists.Building;
                    return View(room);
                }

                // Handle custom fees
                if (!useCustomFees)
                {
                    room.CustomMonthlyRent = null;
                    room.CustomWaterFee = null;
                    room.CustomElectricityFee = null;
                    room.CustomWifiFee = null;
                }

                _context.Rooms.Add(room);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Room added successfully!";
                return RedirectToAction(nameof(ManageRooms), new { floorId = room.FloorId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding room. FloorId: {FloorId}, RoomNumber: {RoomNumber}", room?.FloorId, room?.RoomNumber);
                TempData["Error"] = "An error occurred while adding the room. Please try again.";
                return RedirectToAction(nameof(AddRoom), new { floorId = room.FloorId });
            }
        }

        // GET: Room/EditRoom/5
        public async Task<IActionResult> EditRoom(int id)
        {
            var room = await _context.Rooms
                .Include(r => r.Floor)
                .Include(r => r.Floor.Building)
                .Include(r => r.CurrentTenant)
                .FirstOrDefaultAsync(r => r.RoomId == id);

            if (room == null)
            {
                return NotFound();
            }

            if (room.Floor == null || room.Floor.Building == null)
            {
                _logger.LogWarning("Floor or Building not found for Room. RoomId: {RoomId}", id);
                return NotFound();
            }

            ViewBag.Floor = room.Floor;
            ViewBag.Building = room.Floor.Building;
            return View(room);
        }

        // POST: Room/EditRoom/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRoom(Room room)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    // Check if room number already exists on this floor (excluding current room)
                    if (await _context.Rooms.AnyAsync(r =>
                        r.FloorId == room.FloorId &&
                        r.RoomNumber == room.RoomNumber &&
                        r.RoomId != room.RoomId))
                    {
                        ModelState.AddModelError("RoomNumber", "This room number already exists on this floor.");
                        var floor = await _context.Floors.Include(f => f.Building).FirstOrDefaultAsync(f => f.FloorId == room.FloorId);
                        ViewBag.Floor = floor;
                        ViewBag.Building = floor?.Building;
                        return View(room);
                    }

                    _context.Update(room);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Room updated successfully!";
                    return RedirectToAction(nameof(ManageRooms), new { floorId = room.FloorId });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RoomExists(room.RoomId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            var floorData = await _context.Floors.Include(f => f.Building).FirstOrDefaultAsync(f => f.FloorId == room.FloorId);
            ViewBag.Floor = floorData;
            ViewBag.Building = floorData?.Building;
            return View(room);
        }

        // POST: Room/DeleteRoom/5
        [HttpPost]
        public async Task<IActionResult> DeleteRoom(int id)
        {
            // Start a transaction to ensure all operations complete together
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    _logger.LogInformation("Starting deletion of room with ID {0}", id);

                    var room = await _context.Rooms
                        .Include(r => r.Floor)
                        .Include(r => r.CurrentTenant)
                        .FirstOrDefaultAsync(r => r.RoomId == id);

                    if (room == null)
                    {
                        return Json(new { success = false, message = "Room not found." });
                    }

                    if (room.IsOccupied || room.CurrentTenant != null)
                    {
                        return Json(new { success = false, message = "Cannot delete room that has a tenant. Please remove the tenant first." });
                    }

                    var floorId = room.FloorId; // Save for redirect URL

                    // 1. Delete any bills associated with this room
                    var billsToDelete = await _context.Bills
                        .Where(b => b.RoomId == id)
                        .ToListAsync();

                    if (billsToDelete.Any())
                    {
                        // Delete any payments related to these bills
                        var billIds = billsToDelete.Select(b => b.BillId).ToList();
                        var paymentsToDelete = await _context.Payments
                            .Where(p => billIds.Contains(p.BillId))
                            .ToListAsync();

                        if (paymentsToDelete.Any())
                        {
                            _logger.LogInformation("Deleting {0} payments related to room {1}", paymentsToDelete.Count, id);
                            _context.Payments.RemoveRange(paymentsToDelete);
                            await _context.SaveChangesAsync();
                        }

                        _logger.LogInformation("Deleting {0} bills related to room {1}", billsToDelete.Count, id);
                        _context.Bills.RemoveRange(billsToDelete);
                        await _context.SaveChangesAsync();
                    }

                    // 2. Delete any contracts associated with this room
                    var contractsToDelete = await _context.Contracts
                        .Where(c => c.RoomId == id)
                        .ToListAsync();

                    if (contractsToDelete.Any())
                    {
                        _logger.LogInformation("Deleting {0} contracts related to room {1}", contractsToDelete.Count, id);
                        _context.Contracts.RemoveRange(contractsToDelete);
                        await _context.SaveChangesAsync();
                    }

                    // 3. Try to delete meter readings if the table exists
                    try
                    {
                        var meterReadingsExist = await _context.Database.ExecuteSqlRawAsync("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'MeterReadings'") > 0;

                        if (meterReadingsExist)
                        {
                            var meterReadingsToDelete = await _context.MeterReadings
                                .Where(m => m.RoomId == id)
                                .ToListAsync();

                            if (meterReadingsToDelete.Any())
                            {
                                _logger.LogInformation("Deleting {0} meter readings for room {1}", meterReadingsToDelete.Count, id);
                                _context.MeterReadings.RemoveRange(meterReadingsToDelete);
                                await _context.SaveChangesAsync();
                            }
                        }
                        else
                        {
                            _logger.LogWarning("MeterReadings table does not exist - skipping meter readings deletion for room {0}", id);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue with deletion process
                        _logger.LogWarning("Error accessing MeterReadings table: {0}. Continuing with room deletion.", ex.Message);
                    }

                    // 4. Finally delete the room
                    _logger.LogInformation("Deleting room {0}", room.RoomNumber);
                    _context.Rooms.Remove(room);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    return Json(new
                    {
                        success = true,
                        message = $"Room {room.RoomNumber} has been successfully deleted.",
                        redirectUrl = Url.Action("ManageRooms", new { floorId })
                    });
                }
                catch (Exception ex)
                {
                    // Rollback the transaction on error
                    await transaction.RollbackAsync();
                    _logger.LogError(ex, "Error deleting room. RoomId: {RoomId}, Error: {Error}", id, ex.Message);
                    return Json(new { success = false, message = "An error occurred while deleting the room: " + ex.Message });
                }
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateRoom(int id, [Bind("RoomId,RoomNumber,Description,IsOccupied,FloorId,CustomMonthlyRent,CustomWaterFee,CustomElectricityFee,CustomWifiFee")] Room room)
        {
            if (room == null || id != room.RoomId)
            {
                _logger.LogWarning("ID mismatch in UpdateRoom. Provided ID: {ProvidedId}, Room ID: {RoomId}", id, room != null ? room.RoomId.ToString() : "null");
                return NotFound();
            }

            try
            {
                var existingRoom = await _context.Rooms
                    .Include(r => r.Floor)
                        .ThenInclude(f => f.Building)
                    .Include(r => r.CurrentTenant)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.RoomId == id);

                if (existingRoom == null)
                {
                    _logger.LogWarning("Room not found for update. RoomId: {RoomId}", id);
                    return NotFound();
                }

                if (existingRoom.Floor == null)
                {
                    _logger.LogWarning("Floor not found for Room. RoomId: {RoomId}", id);
                    return NotFound();
                }

                // Preserve the current tenant
                room.CurrentTenant = existingRoom.CurrentTenant;

                _context.Update(room);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Room updated successfully. RoomId: {RoomId}", id);
                TempData["Success"] = "Room updated successfully.";

                return RedirectToAction(nameof(ManageRooms), new { floorId = room.FloorId });
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error updating room. RoomId: {RoomId}", id);

                if (!RoomExists(id))
                {
                    return NotFound();
                }

                TempData["Error"] = "An error occurred while updating the room. Please try again.";
                return RedirectToAction(nameof(EditRoom), new { id = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating room. RoomId: {RoomId}, Error: {Error}", id, ex.Message);
                TempData["Error"] = "An error occurred while updating the room. Please try again.";
                return RedirectToAction(nameof(EditRoom), new { id = id });
            }
        }

        // GET: Room/GetAvailableRooms/5 (floorId)
        [HttpGet]
        public async Task<JsonResult> GetAvailableRooms(int floorId)
        {
            var rooms = await _context.Rooms
                .Where(r => r.FloorId == floorId && !r.IsOccupied)
                .OrderBy(r => r.RoomNumber)
                .Select(r => new
                {
                    RoomId = r.RoomId,
                    displayName = $"Room {r.RoomNumber}"
                })
                .ToListAsync();

            return Json(new { success = true, data = rooms });
        }

        [HttpGet]
        public async Task<JsonResult> GetOccupiedRooms(int floorId)
        {
            var rooms = await _context.Rooms
                .Where(r => r.FloorId == floorId)
                .ToListAsync();

            // Log all rooms for this floor
            foreach (var room in rooms)
            {
                _logger.LogInformation($"Room {room.RoomId}: IsOccupied={room.IsOccupied}, TenantId={room.TenantId}");
            }

            var occupiedRooms = rooms
                .Where(r => r.IsOccupied && r.TenantId != null)
                .OrderBy(r => r.RoomNumber)
                .Select(r => new {
                    roomId = r.RoomId,
                    displayName = $"Room {r.RoomNumber}"
                })
                .ToList();

            _logger.LogInformation($"Occupied rooms found: {occupiedRooms.Count}");

            return Json(new { success = true, data = occupiedRooms });
        }

        [HttpGet]
        public async Task<IActionResult> GetRoomInfo(int roomId)
        {
            var room = await _context.Rooms
                .Include(r => r.Floor)
                .ThenInclude(f => f.Building)
                .FirstOrDefaultAsync(r => r.RoomId == roomId);

            if (room == null)
                return Json(new { success = false });

            // Get the tenant
            var tenantId = room.TenantId;
            decimal? previousReading = null;
            if (!string.IsNullOrEmpty(tenantId))
            {
                var lastReading = await _context.MeterReadings
                    .Where(m => m.TenantId == tenantId)
                    .OrderByDescending(m => m.ReadingDate)
                    .FirstOrDefaultAsync();
                previousReading = lastReading?.CurrentReading;
            }

            // Get the rate (custom or default)
            decimal rate = room.CustomElectricityFee
                ?? room.Floor?.Building?.DefaultElectricityFee
                ?? 0;

            return Json(new
            {
                success = true,
                rate,
                previousReading
            });
        }

        private bool RoomExists(int id)
        {
            return _context.Rooms.Any(e => e.RoomId == id);
        }
    }
}