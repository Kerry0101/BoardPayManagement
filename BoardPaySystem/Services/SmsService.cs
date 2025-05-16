using System.Text;
using Newtonsoft.Json;

namespace BoardPaySystem.Services
{
    public class SmsService
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private readonly string _defaultSenderName;

        public SmsService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _apiKey = config["Semaphore:ApiKey"];
            _defaultSenderName = config["Semaphore:SenderName"] ?? "BoardPay";
        }

        public async Task<bool> SendSmsAsync(string number, string message, string senderName = null)
        {
            var payload = new
            {
                apikey = _apiKey,
                number = number,
                message = message,
                sendername = senderName ?? _defaultSenderName
            };

            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("https://api.semaphore.co/api/v4/messages", content);
            return response.IsSuccessStatusCode;
        }
    }
}
