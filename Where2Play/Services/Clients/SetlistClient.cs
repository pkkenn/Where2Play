using Where2Play.Models;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace Where2Play.Services.Clients;

public partial class SetlistClient(
    IHttpClientFactory httpFactory,
    IMemoryCache cache,
    ILogger<SetlistClient> logger)
{
    private static readonly SemaphoreSlim RequestSemaphore = new(1, 1);
    private static DateTime _lastRequestTime = DateTime.MinValue;

    private const int MinMillisecondsBetweenRequests = 700; // ~1.4 requests/sec to avoid 429
    private static readonly TimeSpan NotFoundTtl = TimeSpan.FromHours(12);
    private static readonly TimeSpan CityCacheDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ArtistLookupCacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan ShowsCacheDuration = TimeSpan.FromHours(1);

    private sealed class SetlistArtistSearch
    {
        public List<SetlistArtistItem>? artist { get; set; }
    }

    private sealed class SetlistArtistItem
    {
        public string? mbid { get; set; }
        public string? name { get; set; }
        public string? sortName { get; set; }
        public string? url { get; set; }
    }

    [GeneratedRegex(@"-([a-f0-9]+)\.html$", RegexOptions.IgnoreCase)]
    private static partial Regex SetlistArtistIdRegex();

    private static string? ExtractSetlistArtistId(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        var match = SetlistArtistIdRegex().Match(url);
        return match.Success ? match.Groups[1].Value : null;
    }

    private async Task<HttpResponseMessage> GetWithRetryAsync(
        HttpClient client,
        string url,
        CancellationToken cancellationToken,
        int maxRetries = 3)
    {
        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            await ThrottleRequestAsync(cancellationToken);
            var resp = await client.GetAsync(url, cancellationToken);

            if (resp.IsSuccessStatusCode)
            {
                return resp;
            }

            if (IsRetryableStatusCode(resp.StatusCode))
            {
                var retryAfter = GetRetryDelay(resp, attempt);

                logger.LogWarning(
                    "Setlist.fm throttled or server error (attempt {Attempt}): {Status}. Backing off {Delay}.",
                    attempt, resp.StatusCode, retryAfter);

                if (attempt == maxRetries)
                {
                    return resp; // give up
                }

                await Task.Delay(retryAfter, cancellationToken);
                continue;
            }

            return resp; // non-retryable
        }

        return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("Retries exhausted")
        };
    }

    private TimeSpan GetRetryDelay(HttpResponseMessage resp, int attempt)
    {
        var retryAfter = TimeSpan.FromMilliseconds(MinMillisecondsBetweenRequests * attempt);

        if (resp.Headers.TryGetValues("Retry-After", out var values))
        {
            var raw = values.FirstOrDefault();
            if (int.TryParse(raw, out var seconds))
            {
                retryAfter = TimeSpan.FromSeconds(seconds);
            }
            else if (DateTimeOffset.TryParse(raw, out var when))
            {
                var diff = when - DateTimeOffset.UtcNow;
                if (diff > TimeSpan.Zero) retryAfter = diff;
            }
        }

        return retryAfter;
    }

    private static bool IsRetryableStatusCode(System.Net.HttpStatusCode statusCode)
    {
        return statusCode == System.Net.HttpStatusCode.TooManyRequests
            || statusCode == System.Net.HttpStatusCode.ServiceUnavailable
            || (int)statusCode >= 500;
    }

    public async Task<(List<Setlist> Results, string Error)> SearchSetlistsByCityAsync(
        string city,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"setlist_city::{city.ToLowerInvariant()}";
        if (cache.TryGetValue(cacheKey, out (List<Setlist> Results, string Error) cached))
        {
            return cached;
        }

        var client = httpFactory.CreateClient("setlist");
        var url = $"search/setlists?cityName={Uri.EscapeDataString(city)}";

        var resp = await GetWithRetryAsync(client, url, cancellationToken);
        var body = await resp.Content.ReadAsStringAsync(cancellationToken);

        if (!resp.IsSuccessStatusCode)
        {
            logger.LogError("Setlist.fm city search failed: {Status} {Body}", resp.StatusCode, body);
            return ([], body);
        }

        var welcome = SetlistFmResponse.FromJson(body);
        var results = (welcome?.Setlist?.ToList() ?? [], string.Empty);
        cache.Set(cacheKey, results, CityCacheDuration);
        return results;
    }

    private async Task<(string? setlistId, string? mbid)> GetSetlistArtistAsync(
        string mbidOrName,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var cacheKey = $"setlist_artist_lookup::{mbidOrName.ToLowerInvariant()}";
            if (cache.TryGetValue(cacheKey, out (string? setlistId, string? mbid) cached))
            {
                return cached;
            }

            var notFoundKey = $"setlist_artist_lookup_404::{mbidOrName.ToLowerInvariant()}";
            if (cache.TryGetValue(notFoundKey, out bool previouslyNotFound) && previouslyNotFound)
            {
                return (null, null);
            }

            var client = httpFactory.CreateClient("setlist");
            client.DefaultRequestHeaders.Remove("x-api-key");
            if (!string.IsNullOrEmpty(apiKey))
            {
                client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            }

            bool looksLikeGuid = Guid.TryParse(mbidOrName, out _);
            var searchUrl = looksLikeGuid
                ? $"search/artists?artistMbid={mbidOrName}"
                : $"search/artists?artistName={Uri.EscapeDataString(mbidOrName)}";

            var resp = await GetWithRetryAsync(client, searchUrl, cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (!resp.IsSuccessStatusCode)
            {
                if (!IsRetryableStatusCode(resp.StatusCode))
                {
                    logger.LogWarning(
                        "Setlist.fm artist search failed {Status} for {Query}. Body: {Body}",
                        resp.StatusCode, mbidOrName, body);
                    cache.Set(notFoundKey, true, NotFoundTtl);
                }
                return (null, null);
            }

            var parsed = JsonConvert.DeserializeObject<SetlistArtistSearch>(body);
            var list = parsed?.artist ?? [];

            if (list.Count == 0)
            {
                logger.LogInformation("No Setlist.fm artist found for {Query}. Body: {Body}", mbidOrName, body);
                cache.Set(notFoundKey, true, NotFoundTtl);
                return (null, null);
            }

            var chosen = FindBestArtistMatch(list, mbidOrName);
            var extractedId = ExtractSetlistArtistId(chosen.url);

            logger.LogInformation(
                "Setlist.fm chosen artist: {Name} (extractedId={Id}, mbid={Mbid}, url={Url}) for query {Query}",
                chosen.name, extractedId, chosen.mbid, chosen.url, mbidOrName);

            var result = (extractedId, chosen.mbid);
            cache.Set(cacheKey, result, ArtistLookupCacheDuration);
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error resolving Setlist.fm artist for {Query}", mbidOrName);
            return (null, null);
        }
    }

    private static SetlistArtistItem FindBestArtistMatch(List<SetlistArtistItem> list, string mbidOrName)
    {
        return list.FirstOrDefault(a => string.Equals(a.name, mbidOrName, StringComparison.Ordinal))
            ?? list.FirstOrDefault(a => string.Equals(a.name, mbidOrName, StringComparison.OrdinalIgnoreCase))
            ?? list.FirstOrDefault(a => string.Equals(a.sortName, mbidOrName, StringComparison.OrdinalIgnoreCase))
            ?? list[0];
    }

    private async Task ThrottleRequestAsync(CancellationToken cancellationToken)
    {
        await RequestSemaphore.WaitAsync(cancellationToken);
        try
        {
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            var delayMs = MinMillisecondsBetweenRequests - (int)timeSinceLastRequest.TotalMilliseconds;
            if (delayMs > 0)
            {
                logger.LogDebug("Rate limiting: waiting {DelayMs}ms before next Setlist.fm API request", delayMs);
                await Task.Delay(delayMs, cancellationToken);
            }
            _lastRequestTime = DateTime.UtcNow;
        }
        finally
        {
            RequestSemaphore.Release();
        }
    }

    public async Task<Dictionary<string, List<Setlist>>> GetShowsForMultipleArtistsAsync(
        IEnumerable<string> mbidOrNames,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, List<Setlist>>();
        using var semaphore = new SemaphoreSlim(2);

        var tasks = mbidOrNames
            .Distinct()
            .Select(async mbidOrName =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var shows = await GetShowsForArtistCachedAsync(mbidOrName, cancellationToken);
                    return (mbidOrName, shows);
                }
                finally
                {
                    semaphore.Release();
                }
            })
            .ToList();

        var completed = await Task.WhenAll(tasks);

        foreach (var (mbidOrName, shows) in completed)
        {
            if (shows is { Count: > 0 })
            {
                results[mbidOrName] = shows;
            }
        }

        return results;
    }

    public async Task<List<Setlist>> GetShowsForArtistAsync(
        string mbidOrName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpFactory.CreateClient("setlist");

            var notFoundKey = $"setlist_setlists_404::{mbidOrName.ToLowerInvariant()}";
            if (cache.TryGetValue(notFoundKey, out bool notFoundCached) && notFoundCached)
            {
                return [];
            }

            var apiKey = client.DefaultRequestHeaders.TryGetValues("x-api-key", out var values)
                ? values.FirstOrDefault()
                : null;

            var (_, mbid) = await GetSetlistArtistAsync(mbidOrName, apiKey, cancellationToken);
            if (string.IsNullOrEmpty(mbid))
            {
                logger.LogInformation("No MBID resolved for {Query}", mbidOrName);
                return [];
            }

            string url = $"artist/{mbid}/setlists?p=1";
            var resp = await GetWithRetryAsync(client, url, cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Setlist.fm setlists failed {Status} for {Mbid}. Body: {Body}",
                    resp.StatusCode, mbid, body);

                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    cache.Set(notFoundKey, true, NotFoundTtl);
                }

                return [];
            }

            var welcome = SetlistFmResponse.FromJson(body);
            if (welcome?.Setlist is { Length: > 0 })
            {
                logger.LogInformation("Retrieved {Count} setlists for {Mbid}", welcome.Setlist.Length, mbid);
                return [.. welcome.Setlist];
            }

            return [];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SetlistClient GetShowsForArtist failed for {Query}", mbidOrName);
            return [];
        }
    }

    public async Task<List<Setlist>> GetShowsForArtistCachedAsync(
        string mbidOrName,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"setlist_artist::{mbidOrName}";
        if (cache.TryGetValue(cacheKey, out List<Setlist>? cached) && cached is not null)
        {
            return cached;
        }

        var shows = await GetShowsForArtistAsync(mbidOrName, cancellationToken);
        cache.Set(cacheKey, shows, ShowsCacheDuration);
        return shows;
    }
}
