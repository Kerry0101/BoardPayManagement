namespace BoardPaySystem.Services
{
    public class BillingBackgroundService : BackgroundService
    {
        private readonly ILogger<BillingBackgroundService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private Timer? _timer; // Mark as nullable to fix the non-nullable field error

        public BillingBackgroundService(
            ILogger<BillingBackgroundService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Billing Background Service starting...");

            // Check if background services are disabled in config
            var disableBackgroundServices = Environment.GetEnvironmentVariable("DisableBackgroundServices");
            if (disableBackgroundServices?.ToLower() == "true")
            {
                _logger.LogInformation("Background services are disabled by configuration. BillingBackgroundService will not run.");
                return Task.CompletedTask;
            }

            // Only run if not in shutdown mode
            if (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("BillingBackgroundService enabled. Will NOT run any timer to prevent auto-restart issues.");
                // Disabled for now to prevent auto-restart issues
                // ProcessBills();

                // Comment out timer setup to prevent auto-restart issues
                /*
                var now = DateTime.Now;
                var midnight = new DateTime(now.Year, now.Month, now.Day).AddDays(1);
                var timeToMidnight = midnight - now;

                _timer = new Timer(async (state) =>
                {
                    if (!stoppingToken.IsCancellationRequested)
                    {
                        await ProcessBillsAsync();
                        _timer?.Change(TimeSpan.FromHours(24), Timeout.InfiniteTimeSpan);
                    }
                }, null, timeToMidnight, Timeout.InfiniteTimeSpan);
                */
            }

            return Task.CompletedTask;
        }

        private void ProcessBills()
        {
            // Wrap in Task.Run to avoid blocking the startup thread
            Task.Run(async () => await ProcessBillsAsync());
        }

        private async Task ProcessBillsAsync()
        {
            try
            {
                _logger.LogInformation("Processing bills at {time}", DateTimeOffset.Now);

                using (var scope = _serviceProvider.CreateScope())
                {
                    var billingService = scope.ServiceProvider.GetRequiredService<IBillingService>();

                    // Step 1: Update statuses of existing bills (mark as overdue, apply late fees)
                    await billingService.UpdateBillStatusesAsync();
                    _logger.LogInformation("Bill statuses updated successfully");

                    // Step 2: Generate new bills for tenants who need them today
                    // Fix: Add the missing billingDate parameter
                    int billsGenerated = await billingService.GenerateMonthlyBillsAsync(DateTime.Today);
                    _logger.LogInformation("Generated {count} new bills", billsGenerated);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing bills in background service");
            }
        }
        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Billing Background Service stopping...");

            // Stop the timer immediately
            if (_timer != null)
            {
                await _timer.DisposeAsync();
                _timer = null;
            }

            await base.StopAsync(stoppingToken);
        }

        public override void Dispose()
        {
            _timer?.Dispose();
            _timer = null;
            base.Dispose();

            _logger.LogInformation("Billing Background Service resources released");
        }
    }
}