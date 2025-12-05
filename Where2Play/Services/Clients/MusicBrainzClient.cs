using Newtonsoft.Json;
using Where2Play.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Where2Play.Services.Clients;

/// <summary>
/// Lightweight wrapper for MusicBrainz HTTP calls and caching.
/// </summary>
public class MusicBrainzClient(
    IHttpClientFactory httpFactory,
    IMemoryCache cache,
    ILogger<MusicBrainzClient> logger)
{
    // Global rate limiter to avoid API throttling (MusicBrainz ~1 req/sec recommendation)
    private static readonly SemaphoreSlim RateLimiter = new(1, 1);
    private static DateTime _lastRequestUtc = DateTime.MinValue;
    private const int PoliteDelayMs = 1100; // slightly over 1s between requests
    private const int MaxRetries = 3;
    private static readonly TimeSpan ArtistCacheDuration = TimeSpan.FromHours(24);
    private static readonly TimeSpan GenreCacheDuration = TimeSpan.FromHours(6);

    public async Task<MusicBrainzArtist?> GetArtistDetailsAsync(Guid mbid, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"mb_artist::{mbid}";
            if (cache.TryGetValue(cacheKey, out MusicBrainzArtist? cached) && cached is not null)
            {
                return cached;
            }

            var client = httpFactory.CreateClient("musicbrainz");
            var url = $"artist/{mbid}?inc=ratings+genres&fmt=json";

            await RateLimiter.WaitAsync(cancellationToken);
            try
            {
                await EnforcePoliteDelayAsync(cancellationToken);

                HttpResponseMessage? resp = null;
                string body = string.Empty;

                for (int attempt = 1; attempt <= MaxRetries; attempt++)
                {
                    resp = await client.GetAsync(url, cancellationToken);
                    body = await resp.Content.ReadAsStringAsync(cancellationToken);
                    _lastRequestUtc = DateTime.UtcNow;

                    if (resp.IsSuccessStatusCode)
                    {
                        var artist = MusicBrainzArtist.FromJson(body);
                        cache.Set(cacheKey, artist, ArtistCacheDuration);
                        return artist;
                    }

                    if (IsRetryableStatusCode(resp.StatusCode))
                    {
                        var backoffMs = PoliteDelayMs * attempt;
                        logger.LogWarning(
                            "MusicBrainz throttled or error (attempt {Attempt}): {Status}. Backing off {DelayMs}ms. Body: {Body}",
                            attempt, resp.StatusCode, backoffMs, body);
                        await Task.Delay(backoffMs, cancellationToken);
                        continue;
                    }

                    logger.LogError("MusicBrainz GetArtistDetails failed: {Status} {Body}", resp.StatusCode, body);
                    return null;
                }

                logger.LogError(
                    "MusicBrainz GetArtistDetails retries exhausted for {Mbid}. Last status: {Status} {Body}",
                    mbid, resp?.StatusCode, body);
                return null;
            }
            finally
            {
                RateLimiter.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "MusicBrainz GetArtistDetails failed for {Mbid}", mbid);
            return null;
        }
    }

    public async Task<Dictionary<Guid, MusicBrainzArtist>> GetArtistDetailsBatchAsync(
        IEnumerable<Guid> mbids,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<Guid, MusicBrainzArtist>();
        var mbidsDistinct = mbids.Distinct().ToList();

        // First, satisfy from cache synchronously
        var mbidsToFetch = new List<Guid>();
        foreach (var mbid in mbidsDistinct)
        {
            var cacheKey = $"mb_artist::{mbid}";
            if (cache.TryGetValue(cacheKey, out MusicBrainzArtist? cached) && cached is not null)
            {
                results[mbid] = cached;
            }
            else
            {
                mbidsToFetch.Add(mbid);
            }
        }

        if (mbidsToFetch.Count == 0)
        {
            return results;
        }

        // Limit parallelism to avoid overwhelming external API
        const int concurrency = 2;
        using var semaphore = new SemaphoreSlim(concurrency);
        var tasks = new List<Task<(Guid id, MusicBrainzArtist? artist)>>();

        foreach (var mbid in mbidsToFetch)
        {
            await semaphore.WaitAsync(cancellationToken);
            tasks.Add(FetchArtistWithSemaphoreAsync(mbid, semaphore, cancellationToken));
        }

        var fetched = await Task.WhenAll(tasks);
        foreach (var (id, artist) in fetched)
        {
            if (artist is not null)
            {
                results[id] = artist;
            }
        }

        return results;
    }

    private async Task<(Guid id, MusicBrainzArtist? artist)> FetchArtistWithSemaphoreAsync(
        Guid mbid,
        SemaphoreSlim semaphore,
        CancellationToken cancellationToken)
    {
        try
        {
            var artist = await GetArtistDetailsAsync(mbid, cancellationToken);
            return (mbid, artist);
        }
        finally
        {
            semaphore.Release();
        }
    }

    public async Task<(List<MusicBrainzArtist> Artists, string Error)> FindArtistsByGenreAsync(
        string genre,
        int limit = 25,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpFactory.CreateClient("musicbrainz");
            string cleanGenre = genre.Trim().ToLower();
            string query = Uri.EscapeDataString($"tag:{cleanGenre} OR artist:{cleanGenre}");
            string url = $"artist?query={query}&fmt=json&limit={limit}";

            await RateLimiter.WaitAsync(cancellationToken);
            try
            {
                await EnforcePoliteDelayAsync(cancellationToken);

                var resp = await client.GetAsync(url, cancellationToken);
                var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                _lastRequestUtc = DateTime.UtcNow;

                if (!resp.IsSuccessStatusCode)
                {
                    logger.LogError("MusicBrainz search failed: {Status} {Body}", resp.StatusCode, body);
                    return ([], body);
                }

                var search = JsonConvert.DeserializeObject<MusicBrainzSearchResponse>(body);
                return (search?.Artists ?? [], string.Empty);
            }
            finally
            {
                RateLimiter.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MusicBrainz FindArtistsByGenre failed for {Genre}", genre);
            return ([], ex.Message);
        }
    }

    public async Task<(List<MusicBrainzArtist> Artists, string Error)> FindArtistsByNameAsync(
        string artistName,
        int limit = 25,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var client = httpFactory.CreateClient("musicbrainz");
            string clean = artistName.Trim();
            string query = Uri.EscapeDataString($"artist:\"{clean}\"");
            string url = $"artist?query={query}&fmt=json&limit={limit}";

            await RateLimiter.WaitAsync(cancellationToken);
            try
            {
                await EnforcePoliteDelayAsync(cancellationToken);

                var resp = await client.GetAsync(url, cancellationToken);
                var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                _lastRequestUtc = DateTime.UtcNow;

                if (!resp.IsSuccessStatusCode)
                {
                    logger.LogError("MusicBrainz artist-by-name search failed: {Status} {Body}", resp.StatusCode, body);
                    return ([], body);
                }

                var search = JsonConvert.DeserializeObject<MusicBrainzSearchResponse>(body);
                return (search?.Artists ?? [], string.Empty);
            }
            finally
            {
                RateLimiter.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "MusicBrainz FindArtistsByName failed for {Artist}", artistName);
            return ([], ex.Message);
        }
    }

    public async Task<(List<string> Genres, string Error)> GetArtistGenresAsync(
        string artistName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(artistName))
            {
                return ([], string.Empty);
            }

            var cacheKey = $"artist_genres::{artistName.ToLowerInvariant()}";
            if (cache.TryGetValue(cacheKey, out List<string>? cached) && cached is not null)
            {
                return (cached, string.Empty);
            }

            var client = httpFactory.CreateClient("musicbrainz");
            string clean = artistName.Trim();
            string query = Uri.EscapeDataString($"artist:{clean}");
            string url = $"artist?query={query}&fmt=json&limit=5";

            await RateLimiter.WaitAsync(cancellationToken);
            try
            {
                await EnforcePoliteDelayAsync(cancellationToken);

                var resp = await client.GetAsync(url, cancellationToken);
                var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                _lastRequestUtc = DateTime.UtcNow;

                if (!resp.IsSuccessStatusCode)
                {
                    logger.LogError("MusicBrainz artist search failed: {Status} {Body}", resp.StatusCode, body);
                    return ([], body);
                }

                var search = JsonConvert.DeserializeObject<MusicBrainzSearchResponse>(body);
                var artists = search?.Artists ?? [];

                var genres = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var a in artists)
                {
                    await CollectGenresFromArtistAsync(a, genres, cancellationToken);
                }

                var top = genres
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => kv.Key)
                    .Take(5)
                    .ToList();

                cache.Set(cacheKey, top, GenreCacheDuration);
                return (top, string.Empty);
            }
            finally
            {
                RateLimiter.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetArtistGenresAsync failed for {Artist}", artistName);
            return ([], ex.Message);
        }
    }

    private async Task CollectGenresFromArtistAsync(
        MusicBrainzArtist artist,
        Dictionary<string, int> genres,
        CancellationToken cancellationToken)
    {
        var genreList = artist.Genres;

        if (genreList is null or { Count: 0 })
        {
            var details = await GetArtistDetailsAsync(artist.Id, cancellationToken);
            genreList = details?.Genres;
        }

        if (genreList is null)
        {
            return;
        }

        foreach (var g in genreList.Where(g => !string.IsNullOrEmpty(g.Name)))
        {
            genres.TryGetValue(g.Name, out var cnt);
            genres[g.Name] = cnt + 1;
        }
    }

    private static async Task EnforcePoliteDelayAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var elapsed = now - _lastRequestUtc;
        if (elapsed.TotalMilliseconds < PoliteDelayMs)
        {
            await Task.Delay(PoliteDelayMs - (int)elapsed.TotalMilliseconds, cancellationToken);
        }
    }

    private static bool IsRetryableStatusCode(System.Net.HttpStatusCode statusCode)
    {
        return statusCode == System.Net.HttpStatusCode.TooManyRequests
            || statusCode == System.Net.HttpStatusCode.ServiceUnavailable
            || (int)statusCode >= 500;
    }
}
