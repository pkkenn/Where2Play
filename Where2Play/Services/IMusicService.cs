using System.Threading.Tasks;
using System.Collections.Generic;
using Where2Play.Models;

namespace Where2Play.Services
{
    public interface IMusicService
    {
        Task<(List<EventSummary> Results, string Error)> SearchCityAsync(string city, CancellationToken cancellationToken = default);

        Task<(List<MusicBrainzArtist> Results, string Error)> FindArtistsByGenreAsync(string genre, int limit = 25, CancellationToken cancellationToken = default);

        Task<(List<CityFit> Results, string Error)> RecommendCitiesAsync(string genre, string region, BandPopularity popularity = BandPopularity.Medium, CancellationToken cancellationToken = default);

        // Search shows by an artist name (tries to find artist(s) then fetch setlists)
        Task<(List<EventSummary> Results, string Error)> SearchArtistAsync(string artistName, int artistLimit = 5, CancellationToken cancellationToken = default);

        // New: get top genres (tags) for an artist name using MusicBrainz
        Task<(List<string> Genres, string Error)> GetArtistGenresAsync(string artistName, CancellationToken cancellationToken = default);
    }
}
