using System.Net.Http.Json;
using NewsletterService.Models;

namespace NewsletterService.Services
{
    public class SubscriberClient
    {
        private readonly HttpClient _http;
        private readonly ILogger<SubscriberClient> _logger;

        public SubscriberClient(HttpClient http, ILogger<SubscriberClient> logger)
        {
            _http   = http;
            _logger = logger;
        }

        public async Task<List<SubscriberDto>?> GetAllSubscribersAsync()
        {
            try
            {
                var response = await _http.GetAsync("/subscribers");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<List<SubscriberDto>>();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SubscriberService unreachable — newsletter aborted");
                return null;
            }
        }
    }
}
