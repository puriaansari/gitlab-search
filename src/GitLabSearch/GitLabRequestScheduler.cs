using System.Globalization;
using System.Net;

namespace GitLabSearch;

/// <summary>Spaces requests and applies one shared rate-limit cooldown across all workers.</summary>
public sealed class GitLabRequestScheduler(HttpClient http, int requestDelayMs)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset nextRequestAt = DateTimeOffset.MinValue;
    private const int MaxRetries = 5;

    public async Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            await gate.WaitAsync(ct);
            try
            {
                // Holding the gate ensures other projects also respect a server cooldown.
                var wait = nextRequestAt - DateTimeOffset.UtcNow;
                while (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait > TimeSpan.FromHours(1) ? TimeSpan.FromHours(1) : wait, ct);
                    wait = nextRequestAt - DateTimeOffset.UtcNow;
                }
                if (Environment.GetEnvironmentVariable("DEBUG") != null)
                    Console.Error.WriteLine($"Requesting: GET {uri}");
                var response = await http.GetAsync(uri, ct);
                nextRequestAt = DateTimeOffset.UtcNow.AddMilliseconds(requestDelayMs);
                if (response.StatusCode != HttpStatusCode.TooManyRequests || attempt == MaxRetries)
                    return response;

                var now = DateTimeOffset.UtcNow;
                var retry = response.Headers.RetryAfter;
                var delay = retry?.Delta
                    ?? (retry?.Date is { } date ? date - now : TimeSpan.FromSeconds(60 * Math.Pow(2, attempt)));
                // Retry-After: 0 or a past date must not create a retry storm.
                if (delay < TimeSpan.FromSeconds(1)) delay = TimeSpan.FromSeconds(1);
                var retryAt = now + delay;
                if (retryAt > nextRequestAt) nextRequestAt = retryAt;
                response.Dispose();
                var seconds = Math.Ceiling((nextRequestAt - now).TotalSeconds).ToString(CultureInfo.InvariantCulture);
                Console.Error.WriteLine($"GitLab rate limit (HTTP 429). Waiting {seconds}s before retry {attempt + 1}/{MaxRetries}. Ctrl+C to cancel.");
            }
            finally { gate.Release(); }
        }
    }
}
