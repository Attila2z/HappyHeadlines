using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ArticleService.Models;
using ArticleService.Services;
using StackExchange.Redis;
using System.Text.Json;

namespace ArticleService.Controllers
{
    [ApiController]
    [Route("articles")]
    public class ArticlesController : ControllerBase
    {
        private readonly DatabaseRouter _router;
        private readonly IConnectionMultiplexer _redis;
        private readonly ArticleCacheMetrics _metrics;
        private readonly ILogger<ArticlesController> _logger;

        public ArticlesController(
            DatabaseRouter router,
            IConnectionMultiplexer redis,
            ArticleCacheMetrics metrics,
            ILogger<ArticlesController> logger)
        {
            _router  = router;
            _redis   = redis;
            _metrics = metrics;
            _logger  = logger;
        }

        [HttpPost]
        public async Task<IActionResult> CreateArticle([FromBody] ArticleRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest("Field 'title' is required.");
            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest("Field 'content' is required.");
            if (string.IsNullOrWhiteSpace(request.Author))
                return BadRequest("Field 'author' is required.");
            if (string.IsNullOrWhiteSpace(request.Continent))
                return BadRequest("Field 'continent' is required.");
            if (!Continents.All.Contains(request.Continent))
                return BadRequest($"Invalid continent. Valid values: {string.Join(", ", Continents.All)}");

            var contexts = _router.CreateContextsForSaving(request.Continent);

            Article? savedArticle = null;

            foreach (var context in contexts)
            {
                using var ctx = context;

                var article = new Article
                {
                    Title     = request.Title,
                    Content   = request.Content,
                    Author    = request.Author,
                    Continent = request.Continent
                };

                ctx.Articles.Add(article);
                await ctx.SaveChangesAsync();

                savedArticle ??= article;
                _logger.LogInformation("Article created in {continent} database with id={id}", request.Continent, article.Id);
            }

            return CreatedAtAction(nameof(GetArticle),
                new { continent = request.Continent, id = savedArticle!.Id },
                savedArticle);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllArticles()
        {
            var allArticles = new List<Article>();

            foreach (var continent in _router.GetContinents())
            {
                using var context = _router.CreateContextFor(continent);
                var articles = await context.Articles.ToListAsync();
                allArticles.AddRange(articles);
            }

            return Ok(allArticles);
        }

        [HttpGet("{continent}/{id}")]
        public async Task<IActionResult> GetArticle(string continent, int id)
        {
            if (!Continents.All.Contains(continent))
                return BadRequest($"Invalid continent. Valid values: {string.Join(", ", Continents.All)}");

            try
            {
                var db  = _redis.GetDatabase();
                var key = $"article-cache:{continent}:{id}";
                var raw = await db.StringGetAsync(key);

                if (raw.HasValue)
                {
                    _metrics.Hits.Inc();
                    _logger.LogDebug("ArticleCache HIT for {continent}/{id}", continent, id);
                    var cached = JsonSerializer.Deserialize<Article>((string)raw!);
                    return Ok(cached);
                }

                _metrics.Misses.Inc();
                _logger.LogDebug("ArticleCache MISS for {continent}/{id}", continent, id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ArticleCache unavailable, falling back to database");
            }

            using var context = _router.CreateContextFor(continent);
            var article = await context.Articles.FindAsync(id);

            if (article == null)
            {
                _logger.LogWarning("Article with id={id} not found in {continent} database", id, continent);
                return NotFound($"Article with id={id} not found in {continent} database.");
            }

            _logger.LogInformation("Article retrieved from {continent} database with id={id}", continent, id);
            return Ok(article);
        }

        [HttpPut("{continent}/{id}")]
        public async Task<IActionResult> UpdateArticle(string continent, int id, [FromBody] ArticleRequest request)
        {
            if (!Continents.All.Contains(continent))
                return BadRequest($"Invalid continent. Valid values: {string.Join(", ", Continents.All)}");

            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest("Field 'title' is required.");
            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest("Field 'content' is required.");
            if (string.IsNullOrWhiteSpace(request.Author))
                return BadRequest("Field 'author' is required.");

            using var context = _router.CreateContextFor(continent);
            var article = await context.Articles.FindAsync(id);

            if (article == null)
                return NotFound($"Article with id={id} not found in {continent} database.");

            article.Title   = request.Title;
            article.Content = request.Content;
            article.Author  = request.Author;

            await context.SaveChangesAsync();

            _logger.LogInformation("Article updated in {continent} database with id={id}", continent, id);
            return Ok(article);
        }

        [HttpDelete("{continent}/{id}")]
        public async Task<IActionResult> DeleteArticle(string continent, int id)
        {
            if (!Continents.All.Contains(continent))
                return BadRequest($"Invalid continent. Valid values: {string.Join(", ", Continents.All)}");

            using var context = _router.CreateContextFor(continent);
            var article = await context.Articles.FindAsync(id);

            if (article == null)
                return NotFound($"Article with id={id} not found in {continent} database.");

            context.Articles.Remove(article);
            await context.SaveChangesAsync();

            _logger.LogInformation("Article deleted from {continent} database with id={id}", continent, id);
            return NoContent();
        }
    }
}