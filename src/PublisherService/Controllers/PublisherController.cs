using Microsoft.AspNetCore.Mvc;
using PublisherService.Models;
using PublisherService.Services;

namespace PublisherService.Controllers
{
    [ApiController]
    [Route("publish")]
    public class PublisherController : ControllerBase
    {
        private static readonly HashSet<string> ValidContinents = new(StringComparer.OrdinalIgnoreCase)
        {
            "Africa", "Antarctica", "Asia", "Australia",
            "Europe", "NorthAmerica", "SouthAmerica", "Global"
        };

        private readonly ProfanityClient _profanityClient;
        private readonly ArticleQueuePublisher _publisher;
        private readonly ILogger<PublisherController> _logger;

        public PublisherController(
            ProfanityClient profanityClient,
            ArticleQueuePublisher publisher,
            ILogger<PublisherController> logger)
        {
            _profanityClient = profanityClient;
            _publisher       = publisher;
            _logger          = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Publish([FromBody] PublishRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
                return BadRequest("Field 'title' is required.");
            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest("Field 'content' is required.");
            if (string.IsNullOrWhiteSpace(request.Author))
                return BadRequest("Field 'author' is required.");
            if (string.IsNullOrWhiteSpace(request.Continent))
                return BadRequest("Field 'continent' is required.");
            if (!ValidContinents.Contains(request.Continent))
                return BadRequest($"Invalid continent. Valid values: {string.Join(", ", ValidContinents)}");

            _logger.LogInformation("Publication request received for '{title}' by {author}", request.Title, request.Author);

            var filteredContent = await _profanityClient.FilterAsync(request.Content);

            if (filteredContent == null)
            {
                _logger.LogWarning("Publication rejected — ProfanityService unavailable for '{title}'", request.Title);
                return StatusCode(503, "Content moderation is unavailable. Publication rejected to ensure content safety.");
            }

            var queued = _publisher.Publish(new ArticleQueueMessage
            {
                Title     = request.Title,
                Content   = filteredContent,
                Author    = request.Author,
                Continent = request.Continent
            });

            if (!queued)
                return StatusCode(503, "Article queue is unavailable. Please try again later.");

            _logger.LogInformation("Article '{title}' approved and queued for continent {continent}", request.Title, request.Continent);
            return Accepted(new { message = "Article approved and queued for publication.", title = request.Title });
        }
    }
}
