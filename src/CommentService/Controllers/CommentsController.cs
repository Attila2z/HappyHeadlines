using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CommentService.Data;
using CommentService.Models;
using CommentService.Services;

namespace CommentService.Controllers
{
    [ApiController]
    [Route("comments")]
    public class CommentsController : ControllerBase
    {
        private readonly CommentDbContext _context;
        private readonly ProfanityClient _profanityClient;
        private readonly CommentCacheService _cache;
        private readonly CommentCacheMetrics _metrics;
        private readonly ILogger<CommentsController> _logger;

        public CommentsController(
            CommentDbContext context,
            ProfanityClient profanityClient,
            CommentCacheService cache,
            CommentCacheMetrics metrics,
            ILogger<CommentsController> logger)
        {
            _context         = context;
            _profanityClient = profanityClient;
            _cache           = cache;
            _metrics         = metrics;
            _logger          = logger;
        }

        // -----------------------------------------------------------------------
        // CREATE — POST /comments
        // 1. Send content to ProfanityService for filtering
        // 2. If ProfanityService is UP  → save filtered content as "Approved"
        // 3. If ProfanityService is DOWN → save original content as "PendingReview"
        // After saving, invalidate the cache for that article.
        // -----------------------------------------------------------------------
        [HttpPost]
        public async Task<IActionResult> CreateComment([FromBody] CommentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Author))
                return BadRequest("Field 'author' is required.");
            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest("Field 'content' is required.");

            // Try to filter profanity — returns null if ProfanityService is down
            var filteredContent = await _profanityClient.FilterProfanity(request.Content);

            var comment = new Comment
            {
                ArticleId = request.ArticleId,
                Author    = request.Author,
                Content   = filteredContent ?? request.Content,
                Status    = filteredContent != null
                    ? CommentStatus.Approved
                    : CommentStatus.PendingReview
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            // Invalidate cached comment list for this article
            try { await _cache.InvalidateAsync(request.ArticleId); }
            catch (Exception ex) { _logger.LogWarning(ex, "CommentCache invalidation failed for articleId={id}", request.ArticleId); }

            if (filteredContent != null)
                _logger.LogInformation("Comment created and approved with id={id}", comment.Id);
            else
                _logger.LogWarning("Comment created with status=PendingReview due to profanity service unavailable, id={id}", comment.Id);

            return CreatedAtAction(nameof(GetComment), new { id = comment.Id }, comment);
        }

        // -----------------------------------------------------------------------
        // READ ALL — GET /comments
        // -----------------------------------------------------------------------
        [HttpGet]
        public async Task<IActionResult> GetAllComments()
        {
            var comments = await _context.Comments.ToListAsync();
            return Ok(comments);
        }

        // -----------------------------------------------------------------------
        // READ BY ARTICLE — GET /comments/article/{articleId}
        // Checks the Redis LRU cache first; falls back to the database on miss.
        // -----------------------------------------------------------------------
        [HttpGet("article/{articleId}")]
        public async Task<IActionResult> GetCommentsByArticle(int articleId)
        {
            // --- Cache lookup ---
            try
            {
                var cached = await _cache.GetAsync(articleId);
                if (cached != null)
                {
                    _metrics.Hits.Inc();
                    _logger.LogDebug("CommentCache HIT for articleId={id}", articleId);
                    return Ok(cached);
                }

                _metrics.Misses.Inc();
                _logger.LogDebug("CommentCache MISS for articleId={id}", articleId);
            }
            catch (Exception ex)
            {
                // Redis unavailable — fall through to database
                _logger.LogWarning(ex, "CommentCache unavailable, falling back to database");
            }

            // --- Database fallback ---
            var comments = await _context.Comments
                .Where(c => c.ArticleId == articleId)
                .ToListAsync();

            // Populate the cache (best-effort)
            try { await _cache.SetAsync(articleId, comments); }
            catch (Exception ex) { _logger.LogWarning(ex, "CommentCache set failed for articleId={id}", articleId); }

            return Ok(comments);
        }

        // -----------------------------------------------------------------------
        // READ ONE — GET /comments/{id}
        // -----------------------------------------------------------------------
        [HttpGet("{id}")]
        public async Task<IActionResult> GetComment(int id)
        {
            var comment = await _context.Comments.FindAsync(id);
            if (comment == null)
            {
                _logger.LogWarning("Comment with id={id} was not found", id);
                return NotFound($"Comment with id={id} was not found.");
            }
            _logger.LogInformation("Comment retrieved with id={id}", id);
            return Ok(comment);
        }

        // -----------------------------------------------------------------------
        // UPDATE — PUT /comments/{id}
        // -----------------------------------------------------------------------
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateComment(int id, [FromBody] CommentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest("Field 'content' is required.");

            var comment = await _context.Comments.FindAsync(id);
            if (comment == null)
                return NotFound($"Comment with id={id} was not found.");

            var filteredContent = await _profanityClient.FilterProfanity(request.Content);

            comment.Content = filteredContent ?? request.Content;
            comment.Status  = filteredContent != null
                ? CommentStatus.Approved
                : CommentStatus.PendingReview;

            await _context.SaveChangesAsync();

            try { await _cache.InvalidateAsync(comment.ArticleId); }
            catch (Exception ex) { _logger.LogWarning(ex, "CommentCache invalidation failed for articleId={id}", comment.ArticleId); }

            if (filteredContent != null)
                _logger.LogInformation("Comment updated and approved with id={id}", id);
            else
                _logger.LogWarning("Comment updated with status=PendingReview due to profanity service unavailable, id={id}", id);

            return Ok(comment);
        }

        // -----------------------------------------------------------------------
        // DELETE — DELETE /comments/{id}
        // -----------------------------------------------------------------------
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteComment(int id)
        {
            var comment = await _context.Comments.FindAsync(id);
            if (comment == null)
            {
                _logger.LogWarning("Comment with id={id} was not found for deletion", id);
                return NotFound($"Comment with id={id} was not found.");
            }

            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();

            try { await _cache.InvalidateAsync(comment.ArticleId); }
            catch (Exception ex) { _logger.LogWarning(ex, "CommentCache invalidation failed for articleId={id}", comment.ArticleId); }

            _logger.LogInformation("Comment deleted with id={id}", id);
            return NoContent();
        }
    }
}