using System.Text.Json;
using NewsletterService.Models;

namespace NewsletterService.Services
{
    public class ArticleClient
    {
        private readonly HttpClient _http;
        private readonly ILogger<ArticleClient> _logger;

        public ArticleClient(HttpClient http, ILogger<ArticleClient> logger)
        {
            _http   = http;
            _logger = logger;
        }

        public async Task<List<ArticleDto>?> GetRecentArticlesAsync(int limit = 5)
        {
            try
            {
                var response = await _http.GetAsync($"/articles/recent?limit={limit}");
                response.EnsureSuccessStatusCode();
                var articles = await response.Content.ReadFromJsonAsync<List<ArticleDto>>(
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return articles;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ArticleService unavailable — newsletter will be sent without article highlights");
                return null;
            }
        }
    }
}
