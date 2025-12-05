using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using Where2Play.Models;
using Where2Play.Services.Clients;

namespace Where2Play.Services;

public class MusicService(
    MusicBrainzClient mbClient,
    SetlistClient setlistClient,
    ILogger<MusicService> logger,
    IMemoryCache cache) : IMusicService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public async Task<(List<EventSummary> Results, string Error)> SearchCityAsync(string city, CancellationToken cancellationToken = default)
    {
        try
        {
            var cacheKey = $"search_city::{city.ToLowerInvariant()}";
            if (cache.TryGetValue(cacheKey, out List<EventSummary>? cachedResults) && cachedResults is not null)
            {
                return (cachedResults, string.Empty);
            }

            var (setlists, error) = await setlistClient.SearchSetlistsByCityAsync(city, cancellationToken);
            if (!string.IsNullOrEmpty(error))
            {
                return ([], error);
            }

            if (setlists is null or { Count: 0 })
            {
                return ([], string.Empty);
            }

            var results = setlists
                .Where(s => s.Artist is not null)
                .Select(s => new EventSummary
                {
                    ArtistName = s.Artist?.Name ?? "Unknown",
                    Venue = s.Venue?.Name ?? "Unknown",
                    City = s.Venue?.City?.Name ?? city,
                    Date = ParseEventDate(s.EventDate),
                    Url = s.Url?.ToString()
                })
                .ToList();

            var uniqueMbids = setlists
                .Where(s => s.Artist?.Mbid != Guid.Empty)
                .Select(s => s.Artist!.Mbid)
                .Distinct()
                .ToList();

            var artistDetails = await mbClient.GetArtistDetailsBatchAsync(uniqueMbids, cancellationToken);

            foreach (var result in results)
            {
                var mbid = setlists.FirstOrDefault(s => s.Artist?.Name == result.ArtistName)?.Artist?.Mbid;
                if (mbid.HasValue && artistDetails.TryGetValue(mbid.Value, out var details))
                {
                    result.Country = details.Country;
                    result.Popularity = FormatPopularity(details.Rating?.Value);
                    result.Genre = FormatGenres(details.Genres);
                }
            }

            cache.Set(cacheKey, results, CacheDuration);

            return (results, string.Empty);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in SearchCityAsync for {City}", city);
            return ([], ex.Message);
        }
    }

    public async Task<(List<MusicBrainzArtist> Results, string Error)> FindArtistsByGenreAsync(string genre, int limit = 25, CancellationToken cancellationToken = default)
    {
        return await mbClient.FindArtistsByGenreAsync(genre, limit, cancellationToken);
    }

    public async Task<(List<CityFit> Results, string Error)> RecommendCitiesAsync(string genre, string region, BandPopularity popularity = BandPopularity.Medium, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!RegionData.Map.TryGetValue(region, out var targetStates))
            {
                logger.LogWarning("Invalid region code: {Region}", region);
                return ([], "Invalid region code");
            }

            int artistLimit = popularity switch
            {
                BandPopularity.Small => 25,
                BandPopularity.Medium => 50,
                BandPopularity.Large => 100,
                _ => 50
            };

            var (artists, error) = await mbClient.FindArtistsByGenreAsync(genre, artistLimit, cancellationToken);
            if (!string.IsNullOrEmpty(error))
            {
                return ([], error);
            }

            var topArtists = artists
                .OrderByDescending(a => a.Rating?.Value ?? 0)
                .Take(artistLimit)
                .ToList();

            var cityCounts = new Dictionary<string, int>();
            var cityReasons = new Dictionary<string, List<string>>();

            var artistShows = await setlistClient.GetShowsForMultipleArtistsAsync(
                topArtists.Select(a => a.Id.ToString()),
                cancellationToken);

            foreach (var (artistId, shows) in artistShows)
            {
                var artist = topArtists.FirstOrDefault(a => a.Id.ToString() == artistId);
                if (artist is null || shows is null or { Count: 0 }) continue;

                foreach (var show in shows)
                {
                    if (show.Venue?.City?.StateCode is not null && targetStates.Contains(show.Venue.City.StateCode))
                    {
                        string cityKey = $"{show.Venue.City.Name}, {show.Venue.City.StateCode}";

                        if (!cityCounts.TryGetValue(cityKey, out _))
                        {
                            cityCounts[cityKey] = 0;
                            cityReasons[cityKey] = [];
                        }

                        cityCounts[cityKey]++;
                        string reasonDetail = $"{artist.Name} @ {show.Venue.Name}";
                        if (!cityReasons[cityKey].Contains(reasonDetail))
                        {
                            cityReasons[cityKey].Add(reasonDetail);
                        }
                    }
                }
            }

            if (cityCounts.Count == 0)
            {
                return ([], string.Empty);
            }

            int maxShows = cityCounts.Values.Max();

            var fits = cityCounts
                .Select(kvp => new CityFit
                {
                    City = kvp.Key,
                    FitScore = (int)((double)kvp.Value / maxShows * 100),
                    Reason = $"Hosted {kvp.Value} shows: {string.Join(", ", cityReasons[kvp.Key].Take(3))}..."
                })
                .OrderByDescending(r => r.FitScore)
                .Take(20)
                .ToList();

            return (fits, string.Empty);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in RecommendCitiesAsync");
            return ([], ex.Message);
        }
    }

    public async Task<(List<EventSummary> Results, string Error)> SearchArtistAsync(string artistName, int artistLimit = 5, CancellationToken cancellationToken = default)
    {
        try
        {
            var (artists, error) = await mbClient.FindArtistsByNameAsync(artistName, artistLimit * 2, cancellationToken);
            if (!string.IsNullOrEmpty(error) || artists is null or { Count: 0 })
            {
                return ([], error ?? "No artists found.");
            }

            var ordered = artists
                .OrderByDescending(a => string.Equals(a.Name, artistName, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(a => a.Rating?.Value ?? 0)
                .Take(artistLimit)
                .ToList();

            var results = new List<EventSummary>();
            var artistDetails = await mbClient.GetArtistDetailsBatchAsync(ordered.Select(a => a.Id), cancellationToken);
            var artistShows = await setlistClient.GetShowsForMultipleArtistsAsync(ordered.Select(a => a.Id.ToString()), cancellationToken);

            foreach (var artist in ordered)
            {
                if (!artistShows.TryGetValue(artist.Id.ToString(), out var shows) || shows is null or { Count: 0 })
                {
                    continue;
                }

                artistDetails.TryGetValue(artist.Id, out var details);

                foreach (var s in shows)
                {
                    var ev = new EventSummary
                    {
                        ArtistName = s.Artist?.Name ?? artist.Name,
                        Venue = s.Venue?.Name ?? "Unknown",
                        City = s.Venue?.City?.Name ?? "Unknown",
                        Date = ParseEventDate(s.EventDate),
                        Url = s.Url?.ToString()
                    };

                    if (details is not null)
                    {
                        ev.Country = details.Country;
                        ev.Genre = FormatGenres(details.Genres);
                        ev.Popularity = FormatPopularity(details.Rating?.Value);
                    }

                    results.Add(ev);
                }
            }

            var deduped = results
                .GroupBy(r => new { r.ArtistName, r.Venue, Date = r.Date?.ToString("yyyy-MM-dd"), r.City })
                .Select(g => g.First())
                .ToList();

            return (deduped, string.Empty);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in SearchArtistAsync for {Artist}", artistName);
            return ([], ex.Message);
        }
    }

    public async Task<(List<string> Genres, string Error)> GetArtistGenresAsync(string artistName, CancellationToken cancellationToken = default)
    {
        return await mbClient.GetArtistGenresAsync(artistName, cancellationToken);
    }

    private static DateTime? ParseEventDate(string? eventDate)
    {
        if (string.IsNullOrEmpty(eventDate))
        {
            return null;
        }

        return DateTime.TryParseExact(eventDate, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    private static string? FormatPopularity(double? ratingValue)
    {
        return ratingValue.HasValue ? $"{(ratingValue.Value / 5.0) * 100:F0}%" : null;
    }

    private static string? FormatGenres(List<Genre>? genres)
    {
        return genres is { Count: > 0 }
            ? string.Join(", ", genres.Select(g => g.Name).Take(3))
            : null;
    }
}
