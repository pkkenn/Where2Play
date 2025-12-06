using Microsoft.AspNetCore.Mvc;
using Where2Play.Models;
using Where2Play.Services;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Where2Play.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        private readonly CitySearchService _searchService;
        private readonly EventParkingService _parkingService;

        public ValuesController(CitySearchService searchService, EventParkingService parkingService)
        {
            _searchService = searchService;
            _parkingService = parkingService;
        }

        /// <summary>
        /// Get all pre-seeded demo concert events
        /// </summary>
        /// <returns>A list of all demo events across multiple cities (Cincinnati, Nashville, Cleveland, Columbus, Chicago)</returns>
        /// <response code="200">Returns the complete list of demo events</response>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<EventSummary>), StatusCodes.Status200OK)]
        public IEnumerable<EventSummary> Get()
        {
            // Return all demo events from CitySearchService
            return CitySearchService.GetAllDemoEvents();
        }

        /// <summary>
        /// Get a specific event by ID
        /// </summary>
        /// <param name="id">The unique identifier (hash code) of the event</param>
        /// <returns>The event matching the specified ID</returns>
        /// <response code="200">Returns the requested event</response>
        /// <response code="404">If no event with the specified ID is found</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(EventSummary), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public EventSummary Get(int id)
        {
            return EventRoster.AllEvents.FirstOrDefault(e => e.GetHashCode() == id);
        }

        /// <summary>
        /// Search for concert events in a specific city
        /// </summary>
        /// <param name="city">The name of the city to search for events (e.g., Cincinnati, Nashville, Cleveland, Columbus, Chicago)</param>
        /// <returns>A list of concert events in the specified city, including both demo data and live data from Setlist.fm API</returns>
        /// <remarks>
        /// This endpoint combines pre-seeded demo events with live concert data from Setlist.fm.
        /// Artist details (genre, country, popularity) are enriched using MusicBrainz API with intelligent caching.
        /// 
        /// Sample request:
        /// 
        ///     GET /api/Values/search?city=Nashville
        ///     
        /// Available demo cities: Cincinnati, Nashville, Cleveland, Columbus, Chicago
        /// </remarks>
        /// <response code="200">Returns the list of events found in the city</response>
        /// <response code="400">If the city parameter is missing or empty</response>
        [HttpGet("search")]
        [ProducesResponseType(typeof(List<EventSummary>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<EventSummary>>> Search([FromQuery] string city)
        {
            if (string.IsNullOrWhiteSpace(city))
            {
                return BadRequest("City parameter is required");
            }

            var results = await _searchService.SearchCityEventsAsync(city);
            return Ok(results);
        }

        /// <summary>
        /// Search for concert events by artist name from MusicBrainz
        /// </summary>
        /// <param name="artist">The name of the artist or band to search for (e.g., Metallica, Taylor Swift, The Killers)</param>
        /// <returns>A list of concert events for the specified artist from Setlist.fm, enriched with MusicBrainz data</returns>
        /// <remarks>
        /// This endpoint searches MusicBrainz for the artist and then finds all concert setlists for that artist on Setlist.fm.
        /// Results include venue, city, date, and artist details (genres, popularity, country).
        /// 
        /// Sample request:
        /// 
        ///     GET /api/Values/artist?artist=Metallica
        /// </remarks>
        /// <response code="200">Returns the list of events found for the artist</response>
        /// <response code="400">If the artist parameter is missing or empty</response>
        [HttpGet("artist")]
        [ProducesResponseType(typeof(List<EventSummary>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<List<EventSummary>>> SearchByArtist([FromQuery] string artist)
        {
            if (string.IsNullOrWhiteSpace(artist))
            {
                return BadRequest("Artist parameter is required");
            }

            var results = await _searchService.SearchArtistEventsAsync(artist);
            return Ok(results);
        }

        /// <summary>
        /// Get parking information for a specific event
        /// </summary>
        /// <param name="eventId">The event ID</param>
        /// <returns>Parking information from EventParking API</returns>
        /// <response code="200">Returns parking information if available</response>
        /// <response code="404">If parking information cannot be found</response>
        [HttpGet("parking/event/{eventId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetParkingForEvent(string eventId)
        {
            if (string.IsNullOrEmpty(eventId))
            {
                return BadRequest("Event ID is required");
            }

            var parkingData = await _parkingService.GetParkingForEventAsync(eventId);
            if (parkingData == null)
            {
                return NotFound($"No parking information found for event {eventId}");
            }

            return Ok(parkingData);
        }

        /// <summary>
        /// Get parking options near a specific location
        /// </summary>
        /// <param name="latitude">Venue latitude</param>
        /// <param name="longitude">Venue longitude</param>
        /// <param name="radius">Search radius in miles (default: 1.0)</param>
        /// <returns>List of nearby parking options</returns>
        /// <response code="200">Returns nearby parking options</response>
        [HttpGet("parking/nearby")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetNearbyParking([FromQuery] double latitude, [FromQuery] double longitude, [FromQuery] double radius = 1.0)
        {
            if (latitude == 0 || longitude == 0)
            {
                return BadRequest("Latitude and longitude are required");
            }

            var parkingData = await _parkingService.GetParkingByLocationAsync(latitude, longitude, radius);
            return Ok(parkingData ?? new { message = "No parking information available for this location" });
        }

        /// <summary>
        /// Get all available parking options
        /// </summary>
        /// <returns>List of all parking options</returns>
        /// <response code="200">Returns all parking options</response>
        [HttpGet("parking")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllParking()
        {
            var parkingData = await _parkingService.GetAllParkingAsync();

            // Check if the list is null or empty
            if (parkingData == null || !parkingData.Any())
            {
                // Return the message object if no data found
                return Ok(new { message = "No parking information available" });
            }

            // Otherwise, return the list of parking
            return Ok(parkingData);
        }
    }
}
