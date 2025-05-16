namespace BoardPaySystem.Services
{
    public class NotificationBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _services;

        public NotificationBackgroundService(IServiceProvider services)
        {
            _services = services;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                using (var scope = _services.CreateScope())
                {
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    await notificationService.CheckAndCreateDueDateNotificationsAsync();
                }

                // Run once per day at midnight
                var now = DateTime.Now;
                var nextRun = now.Date.AddDays(1);
                var delay = nextRun - now;
                await Task.Delay(delay, stoppingToken);
            }
        }
    }
}