using BoardPaySystem.Models;
using BoardPaySystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages(); // Add this for Identity UI pages

// Configure database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Configure Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure cookie settings
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// Add role initialization service
builder.Services.AddScoped<IRoleInitializer, RoleInitializer>();

// Add billing service
builder.Services.AddScoped<IBillingService, BillingService>();

// Add notification service
builder.Services.AddScoped<INotificationService, NotificationService>();

// Add meter reading service with resilience to missing tables
builder.Services.AddScoped<IMeterReadingService, ResilienceMeterReadingService>();

// Add database update service to fix schema
builder.Services.AddHostedService<DbUpdateService>();

// Add billing background service (but only if not running in a special debug mode)
var disableBackgroundServices = builder.Configuration.GetValue<bool>("DisableBackgroundServices");
if (!disableBackgroundServices)
{
    builder.Services.AddHostedService<BillingBackgroundService>();
    builder.Services.AddHostedService<NotificationBackgroundService>();
}

// Add landlord service
builder.Services.AddScoped<ILandlordService, LandlordService>();

// Add SmsService with AddHttpClient for DI
builder.Services.AddHttpClient<SmsService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Serves files from wwwroot
app.UseRouting();
// ****************************************
// ***** ADD AUTHENTICATION MIDDLEWARE ****
// ***** ORDER MATTERS: Before AuthZ *****
// ****************************************
app.UseAuthentication(); // Determines *who* the user is (reads the cookie)
// ****************************************
// ********** END ADDED SECTION ***********
// ****************************************

app.UseAuthorization();

// Initialize roles
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var roleInitializer = services.GetRequiredService<IRoleInitializer>();
        await roleInitializer.InitializeAsync();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing roles.");
    }
}

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages(); // Add this for Identity UI pages

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.Run();

