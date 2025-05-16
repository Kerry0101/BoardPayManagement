using BoardPaySystem.Models;
using Microsoft.EntityFrameworkCore;

namespace BoardPaySystem.Services
{
    public class DbUpdateService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DbUpdateService> _logger;

        public DbUpdateService(
            IServiceProvider serviceProvider,
            ILogger<DbUpdateService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("DbUpdateService is starting");

            // Check if background services are disabled in config
            var disableBackgroundServices = Environment.GetEnvironmentVariable("DisableBackgroundServices");
            if (disableBackgroundServices?.ToLower() == "true")
            {
                _logger.LogInformation("Background services are disabled by configuration. DbUpdateService will not run.");
                return;
            }

            using (var scope = _serviceProvider.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                try
                {// Check if MeterReadings table exists
                    try
                    {
                        // This query will throw if the table doesn't exist
                        await dbContext.MeterReadings.FirstOrDefaultAsync(cancellationToken);
                        _logger.LogInformation("MeterReadings table already exists");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("MeterReadings table doesn't exist: {Message}", ex.Message);

                        // Execute SQL script to create MeterReadings table
                        try
                        {
                            string sqlFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "create_meter_readings_table.sql");
                            if (File.Exists(sqlFilePath))
                            {
                                string sqlScript = await File.ReadAllTextAsync(sqlFilePath, cancellationToken);
                                await dbContext.Database.ExecuteSqlRawAsync(sqlScript, cancellationToken);
                                _logger.LogInformation("Successfully created MeterReadings table");
                            }
                            else
                            {
                                _logger.LogWarning("MeterReadings SQL script not found at: {Path}", sqlFilePath);
                            }
                        }
                        catch (Exception sqlEx)
                        {
                            _logger.LogError(sqlEx, "Error creating MeterReadings table");
                        }
                    }

                    // Step 1: Check if bill columns already exist
                    bool billingMonthExists = false;
                    bool billingYearExists = false;

                    try
                    {
                        // This query will throw if columns don't exist
                        await dbContext.Bills.Select(b => new { b.BillingMonth, b.BillingYear }).FirstOrDefaultAsync(cancellationToken);
                        billingMonthExists = true;
                        billingYearExists = true;
                        _logger.LogInformation("BillingMonth and BillingYear columns already exist");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInformation("Need to check column existence: {Message}", ex.Message);
                    }

                    if (!billingMonthExists || !billingYearExists)
                    {
                        _logger.LogInformation("Adding missing columns to Bills table");

                        // Add BillingMonth if missing
                        if (!billingMonthExists)
                        {
                            await dbContext.Database.ExecuteSqlRawAsync(
                                "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Bills') AND name = 'BillingMonth') " +
                                "ALTER TABLE Bills ADD BillingMonth INT NOT NULL DEFAULT 0;",
                                cancellationToken);
                            _logger.LogInformation("Added BillingMonth column");
                        }

                        // Add BillingYear if missing
                        if (!billingYearExists)
                        {
                            await dbContext.Database.ExecuteSqlRawAsync(
                                "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Bills') AND name = 'BillingYear') " +
                                "ALTER TABLE Bills ADD BillingYear INT NOT NULL DEFAULT 0;",
                                cancellationToken);
                            _logger.LogInformation("Added BillingYear column");
                        }

                        // Update existing records to populate these fields from the BillingDate
                        await dbContext.Database.ExecuteSqlRawAsync(
                            "UPDATE Bills SET BillingMonth = MONTH(BillingDate), BillingYear = YEAR(BillingDate) " +
                            "WHERE BillingMonth = 0 OR BillingYear = 0;",
                            cancellationToken);
                        _logger.LogInformation("Updated existing bill records with month and year values");
                    }

                    // Step 2: Record migrations in history table if not already there
                    bool initialMigrationExists = false;
                    bool billMonthYearMigrationExists = false;

                    try
                    {
                        // Use scalar queries that return a count instead of a boolean directly
                        var initialMigrationCount = await dbContext.Database.ExecuteSqlRawAsync(
                            "SELECT COUNT(*) FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20250507062717_InitialCreate'",
                            cancellationToken);

                        initialMigrationExists = initialMigrationCount > 0;
                        _logger.LogInformation("Initial migration existence check result: {Result}", initialMigrationExists);

                        var billMonthYearMigrationCount = await dbContext.Database.ExecuteSqlRawAsync(
                            "SELECT COUNT(*) FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20250509064028_billMonthYear'",
                            cancellationToken);

                        billMonthYearMigrationExists = billMonthYearMigrationCount > 0;
                        _logger.LogInformation("BillMonthYear migration existence check result: {Result}", billMonthYearMigrationExists);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error checking migrations in history table");
                    }

                    if (!initialMigrationExists)
                    {
                        try
                        {
                            await dbContext.Database.ExecuteSqlRawAsync(
                                "IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20250507062717_InitialCreate') " +
                                "INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) " +
                                "VALUES ('20250507062717_InitialCreate', '9.0.0-preview.3.24154.5')",
                                cancellationToken);
                            _logger.LogInformation("Added InitialCreate migration to history");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error adding InitialCreate migration to history");
                        }
                    }
                    else
                    {
                        _logger.LogInformation("InitialCreate migration already exists in history");
                    }

                    if (!billMonthYearMigrationExists)
                    {
                        try
                        {
                            await dbContext.Database.ExecuteSqlRawAsync(
                                "IF NOT EXISTS (SELECT 1 FROM [__EFMigrationsHistory] WHERE [MigrationId] = '20250509064028_billMonthYear') " +
                                "INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion]) " +
                                "VALUES ('20250509064028_billMonthYear', '9.0.0-preview.3.24154.5')",
                                cancellationToken);
                            _logger.LogInformation("Added billMonthYear migration to history");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error adding billMonthYear migration to history");
                        }
                    }
                    else
                    {
                        _logger.LogInformation("BillMonthYear migration already exists in history");
                    }

                    _logger.LogInformation("Database schema update completed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating database schema");
                    // Don't throw - we want the application to continue starting
                    // but log the error so it can be addressed
                }
            }

            _logger.LogInformation("DbUpdateService completed");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("DbUpdateService is stopping");
            return Task.CompletedTask;
        }
    }
}