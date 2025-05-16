using Microsoft.Data.SqlClient;

namespace BoardPaySystem
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Running MeterReadings table creation script...");

            try
            {
                // Load configuration
                var configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

                // Get connection string
                string connectionString = configuration.GetConnectionString("DefaultConnection");

                if (string.IsNullOrEmpty(connectionString))
                {
                    Console.WriteLine("Connection string 'DefaultConnection' not found!");
                    return;
                }

                // Read SQL script
                string scriptPath = Path.Combine(Directory.GetCurrentDirectory(), "create_meter_readings_table.sql");
                string sqlScript = File.ReadAllText(scriptPath);

                // Execute script
                using (var connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand(sqlScript, connection))
                    {
                        await command.ExecuteNonQueryAsync();
                    }
                }

                Console.WriteLine("MeterReadings table created successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Error: {ex.InnerException.Message}");
                }
            }
        }
    }
}