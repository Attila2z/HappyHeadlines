using Microsoft.AspNetCore.Mvc;
using NewsletterService.Models;
using NewsletterService.Services;

namespace NewsletterService.Controllers
{
    [ApiController]
    [Route("newsletters")]
    public class NewsletterController : ControllerBase
    {
        private readonly SubscriberClient _subscriberClient;
        private readonly ArticleClient _articleClient;
        private readonly ILogger<NewsletterController> _logger;

        public NewsletterController(
            SubscriberClient subscriberClient,
            ArticleClient articleClient,
            ILogger<NewsletterController> logger)
        {
            _subscriberClient = subscriberClient;
            _articleClient    = articleClient;
            _logger           = logger;
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendNewsletter([FromBody] NewsletterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Subject))
                return BadRequest("Field 'subject' is required.");
            if (string.IsNullOrWhiteSpace(request.Body))
                return BadRequest("Field 'body' is required.");

            var subscribers = await _subscriberClient.GetAllSubscribersAsync();

            if (subscribers == null)
                return StatusCode(503, "SubscriberService is unavailable. Newsletter not sent.");

            if (subscribers.Count == 0)
                return Ok(new { message = "No subscribers to send to.", sent = 0 });

            var recentArticles = await _articleClient.GetRecentArticlesAsync(limit: 5);

            if (recentArticles != null && recentArticles.Count > 0)
                _logger.LogInformation(
                    "Newsletter '{subject}' will include {count} recent article highlight(s)",
                    request.Subject, recentArticles.Count);

            foreach (var sub in subscribers)
                _logger.LogInformation(
                    "Sending newsletter '{subject}' to {name} <{email}>",
                    request.Subject, sub.Name, sub.Email);

            _logger.LogInformation(
                "Newsletter '{subject}' dispatched to {count} subscriber(s)",
                request.Subject, subscribers.Count);

            return Ok(new
            {
                message  = "Newsletter sent.",
                sent     = subscribers.Count,
                articles = recentArticles?.Count ?? 0
            });
        }
    }
}
