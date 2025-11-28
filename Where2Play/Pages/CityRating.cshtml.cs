using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Where2Play.Models;

namespace Where2Play.Pages
{
    public class TourPlannerModel : PageModel
    {
        // 1. INPUT PROPERTIES
        [BindProperty]
        [Required(ErrorMessage = "Please enter a genre.")]
        public string BandGenre { get; set; }

        [BindProperty]
        [Required]
        public BandPopularity Popularity { get; set; }

        [BindProperty]
        [Required]
        public string TargetRegion { get; set; }

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TourPlannerModel> _logger;

        // Constants matching your CitySearch User-Agent
        private const string UserAgentAppName = "Where2Play/1.0 (dickendd@mail.uc.edu)";

        public TourPlannerModel(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<TourPlannerModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        // 2. VIEW DATA
        public List<SelectListItem> RegionOptions { get; set; }
        public List<CityFit> Results { get; set; } = new List<CityFit>();

        // 3. INITIALIZATION
        public void OnGet()
        {
            PopulateDropdowns();
        }

        // 4. FORM SUBMISSION
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                PopulateDropdowns();
                return Page();
            }

            try
            {
                // Ensure this key name matches your appsettings.json
                string setlistKey = _configuration["ApiKeys:SetlistFm"];
                if (string.IsNullOrEmpty(setlistKey))
                {
                    ModelState.AddModelError("", "API Configuration Missing: SetlistFm key not found.");
                    return Page();
                }

                Results = await AnalyzeTourRoute(BandGenre, Popularity, TargetRegion, setlistKey);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tour data");
                ModelState.AddModelError(string.Empty, "There was an error communicating with the music services.");
            }

            PopulateDropdowns();
            return Page();
        }

        private void PopulateDropdowns()
        {
            RegionOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "NE", Text = "Northeast" },
                new SelectListItem { Value = "SE", Text = "Southeast" },
                new SelectListItem { Value = "MW", Text = "Midwest" },
                new SelectListItem { Value = "SW", Text = "Southwest" },
                new SelectListItem { Value = "W", Text = "West Coast" }
            };
        }

        // --- CORE LOGIC ---

        private async Task<List<CityFit>> AnalyzeTourRoute(string genre, BandPopularity pop, string region, string apiKey)
        {
            // 1. Get State Codes for the selected Region
            if (!RegionMap.TryGetValue(region, out var targetStates))
                return new List<CityFit>();

            // 2. Find Artists from MusicBrainz based on Genre
            // This queries for artists tagged with the genre
            var artists = await GetArtistsByGenreAsync(genre);

            var cityCounts = new Dictionary<string, int>();
            var cityReasons = new Dictionary<string, List<string>>();

            // 3. For each artist, check their recent shows
            foreach (var artist in artists)
            {
                // Rate Limiting
                await Task.Delay(1100);

                var shows = await GetShowsForArtistAsync(artist.Id.ToString(), apiKey);

                if (shows == null) continue;

                foreach (var show in shows)
                {
                    // Check if Venue, City, and StateCode exist, and if the state is in our target region
                    if (show.Venue?.City?.StateCode != null &&
                        targetStates.Contains(show.Venue.City.StateCode))
                    {
                        string cityKey = $"{show.Venue.City.Name}, {show.Venue.City.StateCode}";

                        // Count occurrences
                        if (!cityCounts.ContainsKey(cityKey))
                        {
                            cityCounts[cityKey] = 0;
                            cityReasons[cityKey] = new List<string>();
                        }

                        cityCounts[cityKey]++;

                        // Add reason string (Artist @ Venue)
                        string reasonDetail = $"{artist.Name} @ {show.Venue.Name}";
                        if (!cityReasons[cityKey].Contains(reasonDetail))
                        {
                            cityReasons[cityKey].Add(reasonDetail);
                        }
                    }
                }
            }

            // 4. Calculate Scores and return
            if (cityCounts.Count == 0) return new List<CityFit>();

            int maxShows = cityCounts.Values.Max();

            return cityCounts.Select(kvp => new CityFit
            {
                City = kvp.Key,
                // Simple score calculation based on volume relative to the max found
                FitScore = (int)((double)kvp.Value / maxShows * 100),
                Reason = $"Hosted {kvp.Value} similar shows, including: {string.Join(", ", cityReasons[kvp.Key].Take(2))}."
            })
            .OrderByDescending(r => r.FitScore)
            .Take(20)
            .ToList();
        }

        // --- API HELPERS ---

        private async Task<List<MusicBrainzArtist>> GetArtistsByGenreAsync(string genre)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("User-Agent", UserAgentAppName);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Search query for artists by tag
            string url = $"https://musicbrainz.org/ws/2/artist?query=tag:{genre}&fmt=json&limit=5";

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return new List<MusicBrainzArtist>();

            var json = await response.Content.ReadAsStringAsync();

            // We use a custom wrapper here because MusicBrainz.cs is for a Single Artist,
            // but the Search returns a list wrapper.
            var searchResult = JsonConvert.DeserializeObject<MusicBrainzSearchResponse>(json);

            return searchResult?.Artists ?? new List<MusicBrainzArtist>();
        }

        private async Task<List<Setlist>> GetShowsForArtistAsync(string mbid, string apiKey)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            // Get setlists for this specific artist
            string url = $"https://api.setlist.fm/rest/1.0/artist/{mbid}/setlists?p=1";

            var response = await client.GetAsync(url);
            if (!response.IsSuccessStatusCode) return new List<Setlist>();

            string jsonResponse = await response.Content.ReadAsStringAsync();

            try
            {
                // Use the static helper from your generated 'Setlist.cs'
                var welcomeData = Welcome.FromJson(jsonResponse);

                // Return the array as a List, or empty if null
                return welcomeData?.Setlist?.ToList() ?? new List<Setlist>();
            }
            catch
            {
                return new List<Setlist>();
            }
        }

        private static readonly Dictionary<string, HashSet<string>> RegionMap = new()
        {
            { "NE", new HashSet<string> { "ME", "NH", "VT", "MA", "RI", "CT", "NY", "PA", "NJ" } },
            { "SE", new HashSet<string> { "DE", "MD", "VA", "WV", "NC", "SC", "GA", "FL", "AL", "TN", "MS", "KY" } },
            { "MW", new HashSet<string> { "OH", "IN", "MI", "IL", "WI", "MO", "IA", "MN", "ND", "SD", "NE", "KS" } },
            { "SW", new HashSet<string> { "TX", "OK", "NM", "AZ" } },
            { "W",  new HashSet<string> { "CO", "WY", "MT", "ID", "WA", "OR", "UT", "NV", "CA", "AK", "HI" } }
        };
    }
}