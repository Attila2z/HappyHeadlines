using Polly;
using Polly.CircuitBreaker;

namespace PublisherService.Services
{
    public class ProfanityClient
    {
        private readonly HttpClient _http;
        private readonly ILogger<ProfanityClient> _logger;
        private readonly ResiliencePipeline<string?> _pipeline;

        public ProfanityClient(HttpClient http, ILogger<ProfanityClient> logger)
        {
            _http   = http;
            _logger = logger;

            _pipeline = new ResiliencePipelineBuilder<string?>()
                .AddCircuitBreaker(new CircuitBreakerStrategyOptions<string?>
                {
                    MinimumThroughput = 3,
                    FailureRatio      = 1.0,
                    SamplingDuration  = TimeSpan.FromSeconds(10),
                    BreakDuration     = TimeSpan.FromSeconds(30),
                    ShouldHandle      = args => ValueTask.FromResult(
                        args.Outcome.Exception is HttpRequestException or TaskCanceledException),
                    OnOpened = args =>
                    {
                        logger.LogWarning("Circuit OPENED — ProfanityService down, retrying in {s}s", args.BreakDuration.TotalSeconds);
                        return ValueTask.CompletedTask;
                    },
                    OnClosed = args =>
                    {
                        logger.LogInformation("Circuit CLOSED — ProfanityService recovered");
                        return ValueTask.CompletedTask;
                    },
                    OnHalfOpened = args =>
                    {
                        logger.LogInformation("Circuit HALF-OPEN — testing ProfanityService");
                        return ValueTask.CompletedTask;
                    }
                })
                .Build();
        }

        public async Task<string?> FilterAsync(string text)
        {
            try
            {
                return await _pipeline.ExecuteAsync(async ct =>
                {
                    _http.Timeout = TimeSpan.FromSeconds(5);
                    var response  = await _http.PostAsJsonAsync("/profanity/filter", new { text }, ct);
                    response.EnsureSuccessStatusCode();
                    var result = await response.Content.ReadFromJsonAsync<FilterResponse>(ct);
                    return result?.FilteredText ?? text;
                });
            }
            catch (BrokenCircuitException)
            {
                _logger.LogWarning("Circuit is OPEN — ProfanityService unavailable");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "ProfanityService call failed");
                return null;
            }
        }

        private class FilterResponse
        {
            public string? FilteredText { get; set; }
        }
    }
}
