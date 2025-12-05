using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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

        /// <summary>
        /// Get all available parking options
        /// </summary>
        /// <returns>List of all parking options</returns>
        public async Task<dynamic?> GetAllParkingAsync()
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync($"{_eventParkingApiUrl}/parking");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var parkingData = JsonConvert.DeserializeObject<dynamic>(content);
                    _logger.LogInformation("Retrieved all parking options");
                    return parkingData;
                }
                else
                {
                    _logger.LogWarning($"Failed to retrieve all parking options: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error retrieving all parking options: {ex.Message}");
                return null;
            }
        }
    }
}
