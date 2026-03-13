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
        private readonly ILogger<CommentsController> _logger;

        public CommentsController(CommentDbContext context, ProfanityClient profanityClient, ILogger<CommentsController> logger)
        {
            _context = context;
            _profanityClient = profanityClient;
            _logger = logger;
        }

        // -----------------------------------------------------------------------
        // CREATE — POST /comments
        // 1. Send content to ProfanityService for filtering
        // 2. If ProfanityService is UP  → save filtered content as "Approved"
        // 3. If ProfanityService is DOWN → save original content as "PendingReview"
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
        // Get all comments for a specific article
        // -----------------------------------------------------------------------
        [HttpGet("article/{articleId}")]
        public async Task<IActionResult> GetCommentsByArticle(int articleId)
        {
            var comments = await _context.Comments
                .Where(c => c.ArticleId == articleId)
                .ToListAsync();
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

            _logger.LogInformation("Comment deleted with id={id}", id);
            return NoContent();
        }
    }
}