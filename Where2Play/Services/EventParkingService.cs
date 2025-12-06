using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using Where2Play.Models;

namespace Where2Play.Services
{
    /// <summary>
    /// Service for integrating with the EventParking API to retrieve parking information
    /// </summary>
    public class EventParkingService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<EventParkingService> _logger;
        private readonly string _eventParkingApiUrl = "https://eventparking-g2h3grd0e4cdgvag.eastus2-01.azurewebsites.net/api";

        public EventParkingService(IHttpClientFactory httpClientFactory, ILogger<EventParkingService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Get parking information for a specific event or venue
        /// </summary>
        /// <param name="eventId">The event ID or venue name</param>
        /// <returns>Parking information from EventParking API</returns>
        public async Task<dynamic?> GetParkingForEventAsync(string eventId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync($"{_eventParkingApiUrl}/events/{eventId}/parking");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var parkingData = JsonConvert.DeserializeObject<dynamic>(content);
                    _logger.LogInformation($"Retrieved parking data for event: {eventId}");
                    return parkingData;
                }
                else
                {
                    _logger.LogWarning($"Failed to retrieve parking data for event {eventId}: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving parking data for event {eventId}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get parking information by venue/location coordinates
        /// </summary>
        /// <param name="latitude">Venue latitude</param>
        /// <param name="longitude">Venue longitude</param>
        /// <param name="radius">Search radius in miles (default: 1)</param>
        /// <returns>Nearby parking options</returns>
        public async Task<dynamic?> GetParkingByLocationAsync(double latitude, double longitude, double radius = 1.0)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"{_eventParkingApiUrl}/parking/nearby?lat={latitude}&lng={longitude}&radius={radius}";
                var response = await client.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var parkingData = JsonConvert.DeserializeObject<dynamic>(content);
                    _logger.LogInformation($"Retrieved parking data for location: {latitude}, {longitude}");
                    return parkingData;
                }
                else
                {
                    _logger.LogWarning($"Failed to retrieve parking data for location: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving parking data: {ex.Message}");
                return null;
            }
        }


        public async Task<List<Parking>> SearchEventsAsync(string query)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();

                // Use the user's provided URL structure: /api/geteventsearch?q=TERM
                // We use Uri.EscapeDataString to handle spaces or special characters safely
                string safeQuery = Uri.EscapeDataString(query);
                var response = await client.GetAsync($"{_eventParkingApiUrl}/geteventsearch?q={safeQuery}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var parkingData = JsonConvert.DeserializeObject<List<Parking>>(content);

                    // Return the list or an empty list if null
                    return parkingData ?? new List<Parking>();
                }
                else
                {
                    _logger.LogWarning($"Search failed for '{query}'. Status: {response.StatusCode}");
                    return new List<Parking>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching for '{query}'.");
                return new List<Parking>();
            }
        }

        /// <summary>
        /// Get all available parking options
        /// </summary>
        /// <returns>List of all parking options</returns>
        public async Task<List<Parking>> GetAllParkingAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                //
                var response = await client.GetAsync($"{_eventParkingApiUrl}/parking");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    // Deserialize into your new List<Parking> model instead of dynamic
                    var parkingData = JsonConvert.DeserializeObject<List<Parking>>(content);
                    _logger.LogInformation("Retrieved all parking options");
                    return parkingData ?? new List<Parking>();
                }
                else
                {
                    _logger.LogWarning($"Failed to retrieve all parking options: {response.StatusCode}");
                    return new List<Parking>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving all parking options: {ex.Message}");
                return new List<Parking>();
            }
        }
    }
}