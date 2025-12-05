using Where2Play.Contracts.V1;
using Where2Play.Models;
using Where2Play.Services;

namespace Where2Play.Application.Queries
{
    /// <summary>
    /// Query services that shape data for public API consumption.
    /// </summary>
    public class RecommendationQueries
    {
        private readonly IMusicService _music;

        public RecommendationQueries(IMusicService music)
        {
            _music = music;
        }

        /// <summary>
        /// Recommends cities based on genre, region and popularity.
        /// </summary>
        public async Task<(IReadOnlyList<CityFitDto> Results, string Error)> RecommendCitiesAsync(string genre, string region, BandPopularity popularity)
        {
            var (results, error) = await _music.RecommendCitiesAsync(genre, region, popularity);
            if (!string.IsNullOrEmpty(error)) return (Array.Empty<CityFitDto>(), error);

            var dto = results.Select(r => new CityFitDto
            {
                City = r.City,
                FitScore = r.FitScore,
                Reason = r.Reason
            }).ToList();

            return (dto, string.Empty);
        }

        /// <summary>
        /// Searches events by artist name and returns summaries.
        /// </summary>
        public async Task<(IReadOnlyList<EventSummaryDto> Results, string Error)> SearchEventsByArtistAsync(string artistName, int artistLimit = 5)
        {
            var (events, error) = await _music.SearchArtistAsync(artistName, artistLimit);
            if (!string.IsNullOrEmpty(error)) return (Array.Empty<EventSummaryDto>(), error);

            var dto = events.Select(e => new EventSummaryDto
            {
                ArtistName = e.ArtistName,
                Genre = e.Genre,
                Country = e.Country,
                Popularity = e.Popularity,
                Venue = e.Venue,
                City = e.City,
                Date = e.Date,
                Url = e.Url
            }).ToList();

            return (dto, string.Empty);
        }

        /// <summary>
        /// City search by free text.
        /// </summary>
        public async Task<(IReadOnlyList<EventSummaryDto> Results, string Error)> SearchByCityAsync(string query)
        {
            var (events, error) = await _music.SearchCityAsync(query);
            if (!string.IsNullOrEmpty(error)) return (Array.Empty<EventSummaryDto>(), error);

            var dto = events.Select(e => new EventSummaryDto
            {
                ArtistName = e.ArtistName,
                Genre = e.Genre,
                Country = e.Country,
                Popularity = e.Popularity,
                Venue = e.Venue,
                City = e.City,
                Date = e.Date,
                Url = e.Url
            }).ToList();

            return (dto, string.Empty);
        }
    }
}
