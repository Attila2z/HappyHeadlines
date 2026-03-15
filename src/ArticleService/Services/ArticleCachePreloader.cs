using StackExchange.Redis;
using ArticleService.Models;
using System.Text.Json;

namespace ArticleService.Services
{
    public class ArticleCachePreloader : BackgroundService
    {
        private readonly IServiceProvider _sp;
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<ArticleCachePreloader> _logger;

        private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan ArticleWindow   = TimeSpan.FromDays(14);
        private static readonly TimeSpan CacheTtl        = TimeSpan.FromDays(15);

        public ArticleCachePreloader(
            IServiceProvider sp,
            IConnectionMultiplexer redis,
            ILogger<ArticleCachePreloader> logger)
        {
            _sp     = sp;
            _redis  = redis;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Give the application a moment to finish startup before first run.
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
            var router  = _sp.GetRequiredService<DatabaseRouter>();
            int loaded  = 0;

            foreach (var context in router.GetAllContexts())
            {
                var recent = context.Articles
                    .Where(a => a.CreatedAt >= cutoff)
                    .ToList();

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
