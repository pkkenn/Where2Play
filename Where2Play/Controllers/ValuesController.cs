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

        public ValuesController(CitySearchService searchService)
        {
            _searchService = searchService;
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
    }
}
