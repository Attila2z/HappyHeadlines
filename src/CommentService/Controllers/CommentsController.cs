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

        public CommentsController(CommentDbContext context, ProfanityClient profanityClient)
        {
            _context = context;
            _profanityClient = profanityClient;
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
                // If filtering worked use filtered text, otherwise use original
                Content   = filteredContent ?? request.Content,
                // If filtering failed mark as PendingReview so it can be reviewed later
                Status    = filteredContent != null
                    ? CommentStatus.Approved
                    : CommentStatus.PendingReview
            };

            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

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
                return NotFound($"Comment with id={id} was not found.");
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
                return NotFound($"Comment with id={id} was not found.");

            _context.Comments.Remove(comment);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}