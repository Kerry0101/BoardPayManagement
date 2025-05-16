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
        if (!isTenant)
        {
            return Json(new { success = false, message = "User is not a tenant." });
        }

        // First, delete related entities
        
        // Delete the tenant's bills
        var bills = await _context.Bills.Where(b => b.TenantId == id).ToListAsync();
        _context.Bills.RemoveRange(bills);
        
        // Delete the tenant's contracts
        var contracts = await _context.Contracts.Where(c => c.TenantId == id).ToListAsync();
        _context.Contracts.RemoveRange(contracts);
        
        // Skip MeterReadings handling as the table might not exist yet
        
        // Delete the tenant's payments (if any exist)
        try 
        {
            var payments = await _context.Payments
                .Where(p => p.TenantId == id)
                .ToListAsync();
            
            if (payments != null && payments.Any())
            {
                _context.Payments.RemoveRange(payments);
            }
        }
        catch (Exception ex)
        {
            // Log but continue if Payments table doesn't exist yet
            _logger.LogWarning("Could not delete payments: " + ex.Message);
        }
        
        // Save changes before updating the room to avoid conflicts
        await _context.SaveChangesAsync();

        // Update the room if the tenant is assigned to one
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

        // Finally, delete the tenant
        var result = await _userManager.DeleteAsync(tenant);
        if (result.Succeeded)
        {
            return Json(new { success = true });
        }

        return Json(new { success = false, message = "Error deleting tenant." });
    }
    catch (Exception ex)
    {
        _logger.LogError("Error deleting tenant: " + ex.Message);
        return Json(new { success = false, message = "Error deleting tenant." });
    }
}