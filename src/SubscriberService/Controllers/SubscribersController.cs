using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SubscriberService.Data;
using SubscriberService.Models;
using SubscriberService.Services;

namespace SubscriberService.Controllers
{
    [ApiController]
    [Route("subscribers")]
    public class SubscribersController : ControllerBase
    {
        private readonly SubscriberDbContext _context;
        private readonly RabbitMqPublisher _publisher;
        private readonly IOptionsMonitor<FeatureFlags> _flags;
        private readonly ILogger<SubscribersController> _logger;

        public SubscribersController(
            SubscriberDbContext context,
            RabbitMqPublisher publisher,
            IOptionsMonitor<FeatureFlags> flags,
            ILogger<SubscribersController> logger)
        {
            _context   = context;
            _publisher = publisher;
            _flags     = flags;
            _logger    = logger;
        }

        private IActionResult ServiceDisabled() =>
            StatusCode(503, "SubscriberService is currently disabled via feature flag.");

        [HttpPost]
        public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest request)
        {
            if (!_flags.CurrentValue.SubscriberServiceEnabled)
                return ServiceDisabled();

            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest("Field 'email' is required.");
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Field 'name' is required.");

            if (await _context.Subscribers.AnyAsync(s => s.Email == request.Email))
            {
                _logger.LogWarning("Subscribe attempt for already-subscribed email {email}", request.Email);
                return Conflict($"Email '{request.Email}' is already subscribed.");
            }

            var subscriber = new Subscriber
            {
                Email        = request.Email,
                Name         = request.Name,
                SubscribedAt = DateTime.UtcNow
            };

            _context.Subscribers.Add(subscriber);
            await _context.SaveChangesAsync();

            _publisher.Publish(subscriber);

            _logger.LogInformation("New subscriber added: {name} <{email}>", subscriber.Name, subscriber.Email);
            return CreatedAtAction(nameof(GetSubscriber), new { id = subscriber.Id }, subscriber);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllSubscribers()
        {
            if (!_flags.CurrentValue.SubscriberServiceEnabled)
                return ServiceDisabled();

            var subscribers = await _context.Subscribers.ToListAsync();
            return Ok(subscribers);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetSubscriber(int id)
        {
            if (!_flags.CurrentValue.SubscriberServiceEnabled)
                return ServiceDisabled();

            var subscriber = await _context.Subscribers.FindAsync(id);
            if (subscriber == null)
            {
                _logger.LogWarning("Subscriber with id={id} not found", id);
                return NotFound($"Subscriber with id={id} not found.");
            }

            return Ok(subscriber);
        }

        [HttpDelete("{email}")]
        public async Task<IActionResult> Unsubscribe(string email)
        {
            if (!_flags.CurrentValue.SubscriberServiceEnabled)
                return ServiceDisabled();

            var subscriber = await _context.Subscribers
                .FirstOrDefaultAsync(s => s.Email == email);

            if (subscriber == null)
            {
                _logger.LogWarning("Unsubscribe attempt for unknown email {email}", email);
                return NotFound($"No subscriber found with email '{email}'.");
            }

            _context.Subscribers.Remove(subscriber);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Subscriber removed: {name} <{email}>", subscriber.Name, subscriber.Email);
            return NoContent();
        }
    }
}
