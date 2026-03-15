using Prometheus;

namespace CommentService.Services
{
    public class CommentCacheMetrics
    {
        public Counter Hits { get; } = Metrics.CreateCounter(
            "comment_cache_hits_total",
            "Total number of comment cache hits (served from Redis).");

        public Counter Misses { get; } = Metrics.CreateCounter(
            "comment_cache_misses_total",
            "Total number of comment cache misses (fetched from PostgreSQL).");
    }
}
