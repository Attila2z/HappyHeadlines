using StackExchange.Redis;
using ArticleService.Models;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace ArticleService.Services
{
    public class ArticleCachePreloader : BackgroundService
    {
        private readonly DatabaseRouter _router;
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<ArticleCachePreloader> _logger;

        private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan ArticleWindow   = TimeSpan.FromDays(14);
        private static readonly TimeSpan CacheTtl        = TimeSpan.FromDays(15);

        public ArticleCachePreloader(
            DatabaseRouter router,
            IConnectionMultiplexer redis,
            ILogger<ArticleCachePreloader> logger)
        {
            _router = router;
            _redis  = redis;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await LoadAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "ArticleCache preload cycle failed");
                }

                await Task.Delay(RefreshInterval, stoppingToken);
            }
        }

        private async Task LoadAsync(CancellationToken ct)
        {
            var cutoff  = DateTime.UtcNow.Subtract(ArticleWindow);
            var db      = _redis.GetDatabase();
            int loaded  = 0;

            foreach (var continent in _router.GetContinents())
            {
                using var context = _router.CreateContextFor(continent);

                var recent = await context.Articles
                    .Where(a => a.CreatedAt >= cutoff)
                    .ToListAsync(ct);

                foreach (var article in recent)
                {
                    var key  = $"article-cache:{article.Continent}:{article.Id}";
                    var json = JsonSerializer.Serialize(article);
                    await db.StringSetAsync(key, json, CacheTtl);
                    loaded++;
                }
            }

            _logger.LogInformation("ArticleCache preloaded {count} articles (window={days}d)",
                loaded, ArticleWindow.Days);
        }
    }
}
