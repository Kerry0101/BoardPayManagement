using System.Text;
using Newtonsoft.Json;

namespace BoardPaySystem.Services
{
    public class SmsService
    {
        private readonly string _apiToken;
        private readonly HttpClient _httpClient;
        private readonly string _defaultSenderName;

        public SmsService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _apiToken = config["IPROG:ApiToken"];
            _defaultSenderName = config["Semaphore:SenderName"] ?? "BoardPay";

            // Print all config values for debugging
            foreach (var kvp in config.AsEnumerable())
            {
                System.Diagnostics.Debug.WriteLine($"Config: {kvp.Key} = {kvp.Value}");
                Console.WriteLine($"Config: {kvp.Key} = {kvp.Value}");
            }
        }

        public async Task<bool> SendSmsAsync(string number, string message, string senderName = null)
        {
            // IPROG API does not accept numbers with a leading '+'
            if (!string.IsNullOrEmpty(number) && number.StartsWith("+"))
            {
                number = number.Substring(1);
            }

            var payload = new
            {
                api_token = _apiToken,
                phone_number = number,
                message = message
            };
            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Print the API token for debugging
            System.Diagnostics.Debug.WriteLine($"Using API token: {_apiToken}");
            Console.WriteLine($"Using API token: {_apiToken}");

            var response = await _httpClient.PostAsync("https://sms.iprogtech.com/api/v1/sms_messages", content);

            var responseContent = await response.Content.ReadAsStringAsync();
            // Log the response for debugging
            System.Diagnostics.Debug.WriteLine($"IPROG SMS API response: {responseContent}");
            Console.WriteLine($"IPROG SMS API response: {responseContent}");

            if (!response.IsSuccessStatusCode)
            {
                // Log error
                System.Diagnostics.Debug.WriteLine($"IPROG SMS API HTTP error: {response.StatusCode}");
                return false;
            }

            // Try to parse the response for actual success
            try
            {
                dynamic result = JsonConvert.DeserializeObject(responseContent);
                // IPROG returns { status: 200, ... } for success
                if (result != null && result.status == 200)
                {
                    return true;
                }
                // Log error message if present
                if (result != null && result.message != null)
                {
                    System.Diagnostics.Debug.WriteLine($"IPROG SMS API error: {result.message}");
                    Console.WriteLine($"IPROG SMS API error: {result.message}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse IPROG response: {ex.Message}");
                Console.WriteLine($"Failed to parse IPROG response: {ex.Message}");
            }
            return false;
        }
    }
}
