using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using Where2Play.Models;

namespace Where2Play.Pages
{
    public class CityRatingModel : PageModel
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
        private readonly ILogger<CityRatingModel> _logger;

        private const string UserAgentAppName = "Where2Play/1.0 (dickendd@mail.uc.edu)";

        public CityRatingModel(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<CityRatingModel> logger)
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
                string setlistKey = _configuration["ApiKeys:SetlistFm"];
                if (string.IsNullOrEmpty(setlistKey))
                {
                    _logger.LogError("API Key Missing: SetlistFm key not found in configuration.");
                    ModelState.AddModelError("", "Configuration Error: API key missing.");
                    PopulateDropdowns();
                    return Page();
                }

                Results = await AnalyzeTourRoute(BandGenre, Popularity, TargetRegion, setlistKey);

                if (Results.Count == 0)
                {
                    _logger.LogWarning($"Analysis complete but no cities matched criteria for Genre: {BandGenre}, Region: {TargetRegion}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating city ratings.");
                ModelState.AddModelError(string.Empty, "An error occurred while analyzing data.");
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
            if (!RegionData.Map.TryGetValue(region, out var targetStates))
            {
                _logger.LogWarning($"Invalid region code received: {region}");
                return new List<CityFit>();
            }

            // 1. Find Artists from MusicBrainz
            var artists = await GetArtistsByGenreAsync(genre);
            _logger.LogInformation($"MusicBrainz returned {artists.Count} artists for genre '{genre}'.");

            var cityCounts = new Dictionary<string, int>();
            var cityReasons = new Dictionary<string, List<string>>();

            // 2. Loop through artists to find their recent shows
            foreach (var artist in artists)
            {
                // Rate Limits
                await Task.Delay(1100);

                var shows = await GetShowsForArtistAsync(artist.Id.ToString(), apiKey);

                if (shows == null || !shows.Any()) continue;

                foreach (var show in shows)
                {
                    // Filter by Region (State Code)
                    if (show.Venue?.City?.StateCode != null &&
                        targetStates.Contains(show.Venue.City.StateCode))
                    {
                        string cityKey = $"{show.Venue.City.Name}, {show.Venue.City.StateCode}";

                        if (!cityCounts.ContainsKey(cityKey))
                        {
                            cityCounts[cityKey] = 0;
                            cityReasons[cityKey] = new List<string>();
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

            _logger.LogInformation($"Found {cityCounts.Count} unique cities matching criteria.");

            if (cityCounts.Count == 0) return new List<CityFit>();

            int maxShows = cityCounts.Values.Max();

            // 3. Construct Results
            return cityCounts.Select(kvp => new CityFit
            {
                City = kvp.Key,
                FitScore = (int)((double)kvp.Value / maxShows * 100),
                Reason = $"Hosted {kvp.Value} shows: {string.Join(", ", cityReasons[kvp.Key].Take(3))}..."
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

            string cleanGenre = genre.Trim().ToLower();

            // Search by tag OR by artist keyword directly.
            // AND increase limit to 25 to improve chances of getting results
            string query = Uri.EscapeDataString($"tag:{cleanGenre} OR artist:{cleanGenre}");
            string url = $"https://musicbrainz.org/ws/2/artist?query={query}&fmt=json&limit=25";

            try
            {
                var response = await client.GetAsync(url);
                string json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"MusicBrainz failed: {response.StatusCode} | Response: {json}");
                    return new List<MusicBrainzArtist>();
                }

                var searchResult = JsonConvert.DeserializeObject<MusicBrainzSearchResponse>(json);

                if (searchResult?.Artists == null || searchResult.Artists.Count == 0)
                {
                    // Log raw JSON to see why parsing failed or if API returned nothing
                    _logger.LogWarning($"MusicBrainz returned 0 results. Query: {url}");
                    _logger.LogWarning($"Raw JSON: {json}");
                }

                return searchResult?.Artists ?? new List<MusicBrainzArtist>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during MusicBrainz request.");
                return new List<MusicBrainzArtist>();
            }
        }

        private async Task<List<Setlist>> GetShowsForArtistAsync(string mbid, string apiKey)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            client.DefaultRequestHeaders.Add("Accept", "application/json");

            string url = $"https://api.setlist.fm/rest/1.0/artist/{mbid}/setlists?p=1";

            try
            {
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode) return new List<Setlist>();

                string jsonResponse = await response.Content.ReadAsStringAsync();

                var welcomeData = Welcome.FromJson(jsonResponse);
                return welcomeData?.Setlist?.ToList() ?? new List<Setlist>();
            }
            catch
            {
                return new List<Setlist>();
            }
        }
    }
}