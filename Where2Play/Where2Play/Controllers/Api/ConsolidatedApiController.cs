using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Where2Play.Application.Queries;
using Where2Play.Contracts.V1;
using Where2Play.Models;
using Where2Play.Services;

namespace Where2Play.Controllers.Api
{
    /// <summary>
    /// Consolidated API controller containing read-only endpoints for recommendations, search and music helpers.
    /// Keeps both versioned routes (`/api/v1/...`) and legacy routes (`/api/music/...`) for compatibility.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Produces("application/json")]
    [Route("api/v{version:apiVersion}")]
    public class ConsolidatedApiController : ControllerBase
    {
        private readonly RecommendationQueries _queries;
        private readonly IMusicService _musicService;
        private readonly ILogger<ConsolidatedApiController> _logger;

        public ConsolidatedApiController(RecommendationQueries queries, IMusicService musicService, ILogger<ConsolidatedApiController> logger)
        {
            _queries = queries;
            _musicService = musicService;
            _logger = logger;
        }

        // Versioned: GET /api/v1/recommendations/cities
        [HttpGet("recommendations/cities")]
        // Legacy: GET /api/music/recommendcities?genre=...&region=...&popularity=...
        [HttpGet("/api/music/recommendcities")]
        public async Task<IActionResult> RecommendCities([FromQuery] string genre, [FromQuery] string region, [FromQuery] BandPopularity popularity = BandPopularity.Medium)
        {
            if (string.IsNullOrWhiteSpace(genre) || string.IsNullOrWhiteSpace(region))
                return BadRequest("genre and region required");

            var (results, error) = await _queries.RecommendCitiesAsync(genre, region, popularity);
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogError("RecommendCities failed: {Error}", error);
                return StatusCode(500, error);
            }

            if (results == null || !results.Any())
                return NotFound();

            return Ok(results);
        }

        // Versioned: GET /api/v1/search/by-city?q=...
        [HttpGet("search/by-city")]
        // Legacy: GET /api/music/searchbycity?q=...
        [HttpGet("/api/music/searchbycity")]
        public async Task<IActionResult> SearchByCity([FromQuery(Name = "q")] string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return BadRequest("query required");

            var (results, error) = await _queries.SearchByCityAsync(query);
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogError("SearchByCity failed: {Error}", error);
                return StatusCode(500, error);
            }

            if (results == null || !results.Any()) return NotFound();
            return Ok(results);
        }

        // Versioned: GET /api/v1/recommendations/events-by-artist?artist=...
        [HttpGet("recommendations/events-by-artist")]
        // Legacy compatibility not required for this endpoint, but keep a music-prefixed route too
        [HttpGet("/api/music/searchbyartist")]
        public async Task<IActionResult> EventsByArtist([FromQuery] string artist, [FromQuery] int artistLimit = 5)
        {
            if (string.IsNullOrWhiteSpace(artist)) return BadRequest("artist required");

            var (results, error) = await _queries.SearchEventsByArtistAsync(artist, artistLimit);
            if (!string.IsNullOrEmpty(error))
            {
                _logger.LogError("EventsByArtist failed: {Error}", error);
                return StatusCode(500, error);
            }

            if (results == null || !results.Any()) return NotFound();
            return Ok(results);
        }

        // Versioned: GET /api/v1/music/findsimilarartists?genre=...&limit=...
        [HttpGet("music/findsimilarartists")]
        // Legacy: GET /api/music/findsimilarartists?genre=...&limit=...
        [HttpGet("/api/music/findsimilarartists")]
        public async Task<IActionResult> FindSimilarArtists([FromQuery] string genre, [FromQuery] int limit = 10)
        {
            if (string.IsNullOrWhiteSpace(genre)) return BadRequest("genre required");

            try
            {
                var (artists, error) = await _musicService.FindArtistsByGenreAsync(genre, limit);
                if (!string.IsNullOrEmpty(error))
                {
                    _logger.LogError("FindArtistsByGenreAsync failed: {Error}", error);
                    return StatusCode(500, error);
                }

                var results = artists.Select(a => new
                {
                    a.Id,
                    a.Name,
                    Country = a.Country,
                    Genres = a.Genres?.Select(g => g.Name).ToArray(),
                    Popularity = a.Rating?.Value
                }).ToList();

                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching similar artists");
                return StatusCode(500, "Error fetching similar artists");
            }
        }
    }
}
