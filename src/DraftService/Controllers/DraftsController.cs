using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DraftService.Data;
using DraftService.Models;
using Serilog.Context;

namespace DraftService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DraftsController : ControllerBase
    {
        private readonly DraftDbContext _context;
        private readonly ILogger<DraftsController> _logger;

        public DraftsController(DraftDbContext context, ILogger<DraftsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Get all drafts for a specific author
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Draft>>> GetDrafts([FromQuery] string authorId = null)
        {
            var requestId = HttpContext.TraceIdentifier;
            using (LogContext.PushProperty("requestId", requestId))
            {
                _logger.LogInformation("Fetching drafts for authorId: {authorId}", authorId);

                try
                {
                    IQueryable<Draft> query = _context.Drafts;

                    if (!string.IsNullOrEmpty(authorId))
                    {
                        query = query.Where(d => d.AuthorId == authorId);
                    }

                    var drafts = await query.OrderByDescending(d => d.CreatedAt).ToListAsync();
                    _logger.LogInformation("Retrieved {count} drafts for authorId: {authorId}", drafts.Count, authorId);

                    return Ok(drafts);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving drafts for authorId: {authorId}", authorId);
                    return StatusCode(StatusCodes.Status500InternalServerError, "Error retrieving drafts");
                }
            }
        }

        /// <summary>
        /// Get a specific draft by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<Draft>> GetDraft(Guid id)
        {
            var requestId = HttpContext.TraceIdentifier;
            using (LogContext.PushProperty("requestId", requestId))
            {
                _logger.LogInformation("Fetching draft with id: {draftId}", id);

                try
                {
                    var draft = await _context.Drafts.FindAsync(id);

                    if (draft == null)
                    {
                        _logger.LogWarning("Draft not found with id: {draftId}", id);
                        return NotFound(new { message = "Draft not found" });
                    }

                    _logger.LogInformation("Successfully retrieved draft with id: {draftId}", id);
                    return Ok(draft);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error retrieving draft with id: {draftId}", id);
                    return StatusCode(StatusCodes.Status500InternalServerError, "Error retrieving draft");
                }
            }
        }

        /// <summary>
        /// Create a new draft
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<Draft>> CreateDraft([FromBody] CreateDraftRequest request)
        {
            var requestId = HttpContext.TraceIdentifier;
            using (LogContext.PushProperty("requestId", requestId))
            {
                _logger.LogInformation("Creating new draft for authorId: {authorId}", request.AuthorId);

                try
                {
                    var draft = new Draft
                    {
                        Id = Guid.NewGuid(),
                        Title = request.Title,
                        Content = request.Content,
                        AuthorId = request.AuthorId,
                        Summary = request.Summary,
                        Tags = request.Tags,
                        Status = "Draft",
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Drafts.Add(draft);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Successfully created draft with id: {draftId} for authorId: {authorId}", draft.Id, request.AuthorId);

                    return CreatedAtAction(nameof(GetDraft), new { id = draft.Id }, draft);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating draft for authorId: {authorId}", request.AuthorId);
                    return StatusCode(StatusCodes.Status500InternalServerError, "Error creating draft");
                }
            }
        }

        /// <summary>
        /// Update an existing draft
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDraft(Guid id, [FromBody] UpdateDraftRequest request)
        {
            var requestId = HttpContext.TraceIdentifier;
            using (LogContext.PushProperty("requestId", requestId))
            {
                _logger.LogInformation("Updating draft with id: {draftId}", id);

                try
                {
                    var draft = await _context.Drafts.FindAsync(id);

                    if (draft == null)
                    {
                        _logger.LogWarning("Draft not found for update with id: {draftId}", id);
                        return NotFound(new { message = "Draft not found" });
                    }

                    draft.Title = request.Title ?? draft.Title;
                    draft.Content = request.Content ?? draft.Content;
                    draft.Summary = request.Summary ?? draft.Summary;
                    draft.Tags = request.Tags ?? draft.Tags;
                    draft.Status = request.Status ?? draft.Status;
                    draft.UpdatedAt = DateTime.UtcNow;

                    _context.Drafts.Update(draft);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Successfully updated draft with id: {draftId}", id);

                    return Ok(draft);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating draft with id: {draftId}", id);
                    return StatusCode(StatusCodes.Status500InternalServerError, "Error updating draft");
                }
            }
        }

        /// <summary>
        /// Delete a draft
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDraft(Guid id)
        {
            var requestId = HttpContext.TraceIdentifier;
            using (LogContext.PushProperty("requestId", requestId))
            {
                _logger.LogInformation("Deleting draft with id: {draftId}", id);

                try
                {
                    var draft = await _context.Drafts.FindAsync(id);

                    if (draft == null)
                    {
                        _logger.LogWarning("Draft not found for deletion with id: {draftId}", id);
                        return NotFound(new { message = "Draft not found" });
                    }

                    _context.Drafts.Remove(draft);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Successfully deleted draft with id: {draftId}", id);

                    return NoContent();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting draft with id: {draftId}", id);
                    return StatusCode(StatusCodes.Status500InternalServerError, "Error deleting draft");
                }
            }
        }

        /// <summary>
        /// Submit a draft for review
        /// </summary>
        [HttpPost("{id}/submit")]
        public async Task<IActionResult> SubmitDraft(Guid id)
        {
            var requestId = HttpContext.TraceIdentifier;
            using (LogContext.PushProperty("requestId", requestId))
            {
                _logger.LogInformation("Submitting draft with id: {draftId}", id);

                try
                {
                    var draft = await _context.Drafts.FindAsync(id);

                    if (draft == null)
                    {
                        _logger.LogWarning("Draft not found for submission with id: {draftId}", id);
                        return NotFound(new { message = "Draft not found" });
                    }

                    if (draft.Status != "Draft")
                    {
                        _logger.LogWarning("Cannot submit draft with id: {draftId} - current status: {status}", id, draft.Status);
                        return BadRequest(new { message = $"Cannot submit draft with status: {draft.Status}" });
                    }

                    draft.Status = "Submitted";
                    draft.SubmittedAt = DateTime.UtcNow;
                    draft.UpdatedAt = DateTime.UtcNow;

                    _context.Drafts.Update(draft);
                    await _context.SaveChangesAsync();

                    _logger.LogInformation("Successfully submitted draft with id: {draftId}", id);

                    return Ok(draft);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error submitting draft with id: {draftId}", id);
                    return StatusCode(StatusCodes.Status500InternalServerError, "Error submitting draft");
                }
            }
        }
    }

    public class CreateDraftRequest
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public string AuthorId { get; set; }
        public string Summary { get; set; }
        public string Tags { get; set; }
    }

    public class UpdateDraftRequest
    {
        public string Title { get; set; }
        public string Content { get; set; }
        public string Summary { get; set; }
        public string Tags { get; set; }
        public string Status { get; set; }
    }
}
