using Prometheus;

namespace ArticleService.Services
{
    public class ArticleCacheMetrics
    {
        public Counter Hits { get; } = Metrics.CreateCounter(
            "article_cache_hits_total",
            "Total number of article cache hits (served from Redis).");

        public Counter Misses { get; } = Metrics.CreateCounter(
            "article_cache_misses_total",
            "Total number of article cache misses (fetched from PostgreSQL).");
    }
}
