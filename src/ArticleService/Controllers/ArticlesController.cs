using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ArticleService.Models;
using ArticleService.Services;

namespace ArticleService.Controllers
{
    [ApiController]
    [Route("articles")]
    public class ArticlesController : ControllerBase
    {
        private readonly DatabaseRouter _router;

        public ArticlesController(DatabaseRouter router)
        {
            _router = router;
        }

        // -----------------------------------------------------------------------
        // CREATE — POST /articles
        // Saves to the correct database based on Continent.
        // If Continent = "Global", saves to ALL 8 databases.
        // -----------------------------------------------------------------------
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

            // Get the databases to save to (1 or all 8 if Global)
            var contexts = _router.GetContextsForSaving(request.Continent);

            Article? savedArticle = null;

            foreach (var context in contexts)
            {
                var article = new Article
                {
                    Title     = request.Title,
                    Content   = request.Content,
                    Author    = request.Author,
                    Continent = request.Continent
                };

                context.Articles.Add(article);
                await context.SaveChangesAsync();

                // Return the article from the first database saved
                savedArticle ??= article;
            }

            return CreatedAtAction(nameof(GetArticle),
                new { continent = request.Continent, id = savedArticle!.Id },
                savedArticle);
        }

        // -----------------------------------------------------------------------
        // READ ALL — GET /articles
        // Fetches from ALL 8 databases and merges the results
        // -----------------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> GetAllArticles()
        {
            var allArticles = new List<Article>();

            foreach (var context in _router.GetAllContexts())
            {
                var articles = await context.Articles.ToListAsync();
                allArticles.AddRange(articles);
            }

            return Ok(allArticles);
        }

        // -----------------------------------------------------------------------
        // READ ONE — GET /articles/{continent}/{id}
        // Looks in the specific continent's database
        // -----------------------------------------------------------------------
        [HttpGet("{continent}/{id}")]
        public async Task<IActionResult> GetArticle(string continent, int id)
        {
            if (!Continents.All.Contains(continent))
                return BadRequest($"Invalid continent. Valid values: {string.Join(", ", Continents.All)}");

            var context = _router.GetContextFor(continent);
            var article = await context.Articles.FindAsync(id);

            if (article == null)
                return NotFound($"Article with id={id} not found in {continent} database.");

            return Ok(article);
        }

        // -----------------------------------------------------------------------
        // UPDATE — PUT /articles/{continent}/{id}
        // -----------------------------------------------------------------------
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

            var context = _router.GetContextFor(continent);
            var article = await context.Articles.FindAsync(id);

            if (article == null)
                return NotFound($"Article with id={id} not found in {continent} database.");

            article.Title   = request.Title;
            article.Content = request.Content;
            article.Author  = request.Author;

            await context.SaveChangesAsync();

            return Ok(article);
        }

        // -----------------------------------------------------------------------
        // DELETE — DELETE /articles/{continent}/{id}
        // -----------------------------------------------------------------------
        [HttpDelete("{continent}/{id}")]
        public async Task<IActionResult> DeleteArticle(string continent, int id)
        {
            if (!Continents.All.Contains(continent))
                return BadRequest($"Invalid continent. Valid values: {string.Join(", ", Continents.All)}");

            var context = _router.GetContextFor(continent);
            var article = await context.Articles.FindAsync(id);

            if (article == null)
                return NotFound($"Article with id={id} not found in {continent} database.");

            context.Articles.Remove(article);
            await context.SaveChangesAsync();

            return NoContent();
        }
    }
}