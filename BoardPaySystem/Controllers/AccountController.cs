using BoardPaySystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

// Required for Cookie Authentication

namespace BoardPaySystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
        }

        [HttpGet]
        public async Task<IActionResult> CreateLandlord()
        {
            // Check if landlord already exists
            var landlords = await _userManager.GetUsersInRoleAsync("Landlord");
            if (landlords.Any())
            {
                return RedirectToAction("Login");
            }

            // Check if landlord role exists
            if (!await _roleManager.RoleExistsAsync("Landlord"))
            {
                // Create landlord role
                await _roleManager.CreateAsync(new IdentityRole("Landlord"));
            }

            // Check if tenant role exists
            if (!await _roleManager.RoleExistsAsync("Tenant"))
            {
                // Create tenant role
                await _roleManager.CreateAsync(new IdentityRole("Tenant"));
            }

            return View(new RegisterViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> CreateLandlord(RegisterViewModel model)
        {
            // Check if landlord already exists
            var landlords = await _userManager.GetUsersInRoleAsync("Landlord");
            if (landlords.Any())
            {
                return RedirectToAction("Login");
            }

            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = model.Username,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    PhoneNumber = model.PhoneNumber
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Landlord");
                    TempData["Success"] = "Landlord account created successfully. Please log in.";
                    return RedirectToAction("Login");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Login()
        {
            // If user is already logged in, redirect to appropriate page
            if (User?.Identity?.IsAuthenticated ?? false)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    var isLandlord = await _userManager.IsInRoleAsync(user, "Landlord");
                    return RedirectToAction("Index", isLandlord ? "Landlord" : "Tenant");
                }
            }

            // Check if landlord exists, if not redirect to create landlord
            var landlords = await _userManager.GetUsersInRoleAsync("Landlord");
            if (!landlords.Any())
            {
                return RedirectToAction("CreateLandlord");
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(model.Username, model.Password, model.RememberMe, lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    var user = await _userManager.FindByNameAsync(model.Username);
                    if (user == null)
                    {
                        ModelState.AddModelError(string.Empty, "User not found.");
                        return RedirectToAction("Index", "Home");
                    }

                    if (await _userManager.IsInRoleAsync(user, "Landlord"))
                    {
                        return RedirectToAction("Overview", "Landlord");
                    }
                    else if (await _userManager.IsInRoleAsync(user, "Tenant"))
                    {
                        return RedirectToAction("Bills", "Tenant");
                    }
                }

                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            }

            // If we got this far, something failed, redisplay form
            TempData["Error"] = "Invalid login attempt. Please check your username and password.";
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // --- Optional: Access Denied Action ---
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View(); // Create a simple AccessDenied.cshtml view if needed
        }
    }
}

