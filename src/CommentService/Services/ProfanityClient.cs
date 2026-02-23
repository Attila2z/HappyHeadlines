// =============================================================================
// Services/ProfanityClient.cs
// =============================================================================
// Calls ProfanityService with a Circuit Breaker using Polly v8 syntax.
//
// CIRCUIT BREAKER STATES:
//   CLOSED    → normal, calls go through
//   OPEN      → too many failures, calls blocked instantly (fast fail)
//   HALF-OPEN → testing if service recovered
// =============================================================================

using Polly;
using Polly.CircuitBreaker;

namespace CommentService.Services
{
    public class ProfanityClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ProfanityClient> _logger;

        // Polly v8 uses ResiliencePipeline instead of Policy
        private readonly ResiliencePipeline<string?> _pipeline;

        public ProfanityClient(HttpClient httpClient, ILogger<ProfanityClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            // Build the circuit breaker pipeline using Polly v8 API
            _pipeline = new ResiliencePipelineBuilder<string?>()
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions<string?>
                {
                    // Open after 3 failures
                    MinimumThroughput        = 3,
                    FailureRatio             = 1.0,
                    SamplingDuration         = TimeSpan.FromSeconds(10),
                    // Stay open for 30 seconds
                    BreakDuration            = TimeSpan.FromSeconds(30),
                    ShouldHandle             = args => ValueTask.FromResult(
                        args.Outcome.Exception is HttpRequestException
                        or TaskCanceledException
                    ),
                    OnOpened = args =>
                    {
                        logger.LogWarning(
                            "Circuit OPENED. ProfanityService is down. " +
                            "Retrying in {Duration}s.", args.BreakDuration.TotalSeconds);
                        return ValueTask.CompletedTask;
                    },
                    OnClosed = args =>
                    {
                        logger.LogInformation("Circuit CLOSED. ProfanityService is back up.");
                        return ValueTask.CompletedTask;
                    },
                    OnHalfOpened = args =>
                    {
                        logger.LogInformation("Circuit HALF-OPEN. Testing ProfanityService...");
                        return ValueTask.CompletedTask;
                    }
                })
                .Build();
        }

        // -----------------------------------------------------------------------
        // FilterProfanity
        // Returns filtered text, or null if ProfanityService is unavailable.
        // -----------------------------------------------------------------------
        public async Task<string?> FilterProfanity(string text)
        {
            try
            {
                return await _pipeline.ExecuteAsync(async cancellationToken =>
                {
                    _httpClient.Timeout = TimeSpan.FromSeconds(5);

                    var response = await _httpClient.PostAsJsonAsync(
                        "/profanity/filter",
                        new { text },
                        cancellationToken
                    );

                    response.EnsureSuccessStatusCode();

                    var filtered = await response.Content
                        .ReadFromJsonAsync<FilterResponse>(cancellationToken);

                    return filtered?.FilteredText ?? text;
                });
            }
            catch (BrokenCircuitException)
            {
                _logger.LogWarning("Circuit is OPEN. Skipping profanity check — marking PendingReview.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("ProfanityService failed: {Error}. Marking PendingReview.", ex.Message);
                return null;
            }
        }

        private class FilterResponse
        {
            public string? FilteredText { get; set; }
        }
    }
}