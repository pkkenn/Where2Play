using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Where2Play.Models;
using Where2Play.Services;

namespace Where2Play.Pages
{
    public class ParkingSearchModel : PageModel
    {
        private readonly EventParkingService _parkingService;
        private readonly ILogger<ParkingSearchModel> _logger;

        // 1. DATA PROPERTIES
        public List<Parking> ParkingList { get; set; } = new List<Parking>();

        [BindProperty(SupportsGet = true)]
        public string SearchCity { get; set; }

        public ParkingSearchModel(EventParkingService parkingService, ILogger<ParkingSearchModel> logger)
        {
            _parkingService = parkingService;
            _logger = logger;
        }

        // 2. INITIALIZATION
        public async Task OnGetAsync()
        {
            try
            {
                // If the user hasn't typed anything, default to "Reds" so they see data immediately
                if (string.IsNullOrWhiteSpace(SearchCity))
                {
                    SearchCity = "Reds";
                }

                ParkingList = await _parkingService.SearchEventsAsync(SearchCity);

                if (ParkingList.Any())
                {
                    _logger.LogInformation($"Found {ParkingList.Count} events for search: '{SearchCity}'.");
                }
                else
                {
                    _logger.LogWarning($"No events found for: '{SearchCity}'");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading parking page.");
            }
        }
    }
}