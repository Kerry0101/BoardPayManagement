using System.Diagnostics;
using BoardPaySystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BoardPaySystem.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;

        public HomeController(
            ILogger<HomeController> logger,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context)
        {
            _logger = logger;
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Check if user is authenticated
            if (User?.Identity?.IsAuthenticated ?? false)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    var isLandlord = await _userManager.IsInRoleAsync(user, "Landlord");
                    return RedirectToAction("Overview", isLandlord ? "Landlord" : "Tenant");
                }
            }

            // Check if there are any landlords in the system (modified approach)
            // Instead of using GetUsersInRoleAsync, check if the Landlord role has any entries in AspNetUserRoles
            var landlordRole = await _roleManager.FindByNameAsync("Landlord");
            bool landlordExists = false;

            if (landlordRole != null)
            {
                // Query AspNetUserRoles directly to see if any user has the landlord role
                landlordExists = await _context.Database.ExecuteSqlRawAsync(
                    "SELECT COUNT(*) FROM AspNetUserRoles WHERE RoleId = {0}",
                    landlordRole.Id) > 0;
            }

            if (!landlordExists)
            {
                return RedirectToAction("CreateLandlord", "Account");
            }

            // If not authenticated and landlord exists, show login form
            // Changed to explicitly specify "Index" as the view name
            return View("Index", new LoginViewModel
            {
                Username = string.Empty,
                Password = string.Empty
            });
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
