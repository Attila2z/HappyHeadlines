using StackExchange.Redis;
using CommentService.Models;
using System.Text.Json;

namespace CommentService.Services
{
    public class CommentCacheService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<CommentCacheService> _logger;

        private const int    Capacity = 30;
        private const string LruKey   = "comment-lru";

        public CommentCacheService(
            IConnectionMultiplexer redis,
            ILogger<CommentCacheService> logger)
        {
            _redis  = redis;
            _logger = logger;
        }

        public async Task<List<Comment>?> GetAsync(int articleId)
        {
            var db  = _redis.GetDatabase();
            var key = CacheKey(articleId);
            var raw = await db.StringGetAsync(key);

            if (!raw.HasValue)
                return null;

            // Touch: update recency score in the LRU sorted set
            await db.SortedSetAddAsync(LruKey, articleId.ToString(), NowMs());

            return JsonSerializer.Deserialize<List<Comment>>((string)raw!);
        }

        public async Task SetAsync(int articleId, List<Comment> comments)
        {
            var db   = _redis.GetDatabase();
            var key  = CacheKey(articleId);
            var json = JsonSerializer.Serialize(comments);

            // Add / update in the LRU sorted set
            await db.SortedSetAddAsync(LruKey, articleId.ToString(), NowMs());

            // Evict while over capacity (the new entry may already raise it by 1)
            var count = await db.SortedSetLengthAsync(LruKey);
            if (count > Capacity)
            {
                var oldest = await db.SortedSetRangeByRankAsync(LruKey, 0, 0);
                if (oldest.Length > 0)
                {
                    var evictId = oldest[0].ToString();
                    await db.KeyDeleteAsync(CacheKey(evictId));
                    await db.SortedSetRemoveAsync(LruKey, evictId);
                    _logger.LogDebug(
                        "CommentCache evicted articleId={evictId} (capacity={cap})",
                        evictId, Capacity);
                }
            }

            await db.StringSetAsync(key, json);
        }

        public async Task InvalidateAsync(int articleId)
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(CacheKey(articleId));
            await db.SortedSetRemoveAsync(LruKey, articleId.ToString());
        }

        private static string CacheKey(int    articleId) => $"comment-cache:{articleId}";
        private static string CacheKey(string articleId) => $"comment-cache:{articleId}";
        private static double NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
