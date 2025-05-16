using Microsoft.AspNetCore.Identity;

namespace BoardPaySystem.Services
{
    public class RoleInitializer : IRoleInitializer
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ILogger<RoleInitializer> _logger;

        public RoleInitializer(
            RoleManager<IdentityRole> roleManager,
            ILogger<RoleInitializer> logger)
        {
            _roleManager = roleManager;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            try
            {
                // Create roles if they don't exist
                string[] roleNames = { "Landlord", "Tenant" };
                foreach (var roleName in roleNames)
                {
                    if (!await _roleManager.RoleExistsAsync(roleName))
                    {
                        await _roleManager.CreateAsync(new IdentityRole(roleName));
                        _logger.LogInformation($"Created role: {roleName}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while initializing roles");
                throw;
            }
        }
    }
}