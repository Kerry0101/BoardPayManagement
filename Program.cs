using Microsoft.Extensions.DependencyInjection;

namespace BoardPaySystem.Services
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var services = new ServiceCollection();

            // Add services to the container.
            services.AddHttpClient<BoardPaySystem.Services.SmsService>();

            var serviceProvider = services.BuildServiceProvider();

            // Configure the HTTP request pipeline.
            if (!serviceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
            {
                var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
                logger.LogError("This app should not be run in production mode!");
                return;
            }

            var app = serviceProvider.GetRequiredService<IHostEnvironment>();
            app.Run();
        }
    }
} 