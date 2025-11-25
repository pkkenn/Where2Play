using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Diagnostics;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using Where2Play.Models;

namespace Where2Play.Pages
{
    public class SearchModel : PageModel
    {
        [BindProperty]
        public string SearchText { get; set; }

        [BindProperty]
        public string SearchType { get; set; } = "City"; // default

        public List<EventSummary> FinalResults { get; set; } = new List<EventSummary>();

        public bool HasSearched { get; private set; }

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<SearchModel> _logger;

        public SearchModel(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<SearchModel> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        public void OnGet()
        {
            HasSearched = false;
        }

        public async Task OnPostAsync()
        {
            HasSearched = true;
            FinalResults.Clear();

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                _logger.LogWarning("Search attempted with empty search text.");
                return;
            }

            if (SearchType != "City")
            {
                _logger.LogInformation("Artist search not implemented yet.");
                return;
            }

            string normalizedCity = System.Globalization.CultureInfo.CurrentCulture.TextInfo
                                    .ToTitleCase(SearchText.Trim().ToLower());

            try
            {
                var apiKey = _configuration["ApiKeys:SetlistFm"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogError("Setlist.fm API key not configured.");
                    return;
                }

                var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Where2Play/1.0 (dickendd@mail.uc.edu)");
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var setlistUrl = $"https://api.setlist.fm/rest/1.0/search/setlists?cityName={normalizedCity}";
                var response = await httpClient.GetAsync(setlistUrl);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Setlist.fm API failed with status {StatusCode}: {ErrorContent}", response.StatusCode, errorContent);
                    return;
                }

                string jsonResponse = await response.Content.ReadAsStringAsync();
                var searchResult = Welcome.FromJson(jsonResponse);

                if (searchResult?.Setlist == null || searchResult.Setlist.Length == 0)
                {
                    _logger.LogInformation("No setlists found for city: {City}", normalizedCity);
                    return;
                }

                foreach (var setlist in searchResult.Setlist)
                {
                    string artistName = setlist.Artist?.Name ?? "Unknown Artist";
                    string country = "N/A";
                    string popularity = "N/A";

                    if (setlist.Artist?.Mbid != Guid.Empty)
                    {
                        var mbClient = _httpClientFactory.CreateClient();
                        mbClient.DefaultRequestHeaders.Add("User-Agent", "Where2Play/1.0 (dickendd@mail.uc.edu)");
                        mbClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                        var mbUrl = $"https://musicbrainz.org/ws/2/artist/{setlist.Artist.Mbid}?inc=ratings&fmt=json";
                        var mbResponse = await mbClient.GetAsync(mbUrl);

                        if (mbResponse.IsSuccessStatusCode)
                        {
                            var mbJson = await mbResponse.Content.ReadAsStringAsync();
                            var artistDetails = MusicBrainzArtist.FromJson(mbJson);

                            if (!string.IsNullOrWhiteSpace(artistDetails?.Country))
                                country = artistDetails.Country;

                            if (artistDetails?.Rating?.Value != null)
                                popularity = $"{(artistDetails.Rating.Value / 5.0) * 100:F0}%";
                        }

                        await Task.Delay(1000);
                    }

                    DateTime? eventDate = null;
                    if (!string.IsNullOrWhiteSpace(setlist.EventDate))
                    {
                        // Setlist.fm uses formats: "DD-MM-YYYY", "MM-YYYY", or "YYYY"
                        string[] formats = { "dd-MM-yyyy", "MM-yyyy", "yyyy" };
                        if (DateTime.TryParseExact(setlist.EventDate, formats,
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out var parsedDate))
                        {
                            eventDate = parsedDate;
                        }
                        else
                        {
                            // Fallback: try standard parsing
                            if (DateTime.TryParse(setlist.EventDate, out parsedDate))
                            {
                                eventDate = parsedDate;
                            }
                            else
                            {
                                // Log for debugging if date still can't be parsed
                                Console.WriteLine($"Could not parse date: '{setlist.EventDate}' for artist: {artistName}");
                            }
                        }
                    }

                    FinalResults.Add(new EventSummary
                    {
                        ArtistName = artistName,
                        Genre = "N/A",
                        Country = country,
                        Venue = setlist.Venue?.Name ?? "Unknown Venue",
                        City = setlist.Venue?.City?.Name ?? normalizedCity,
                        Date = eventDate,
                        Url = setlist.Url?.ToString() ?? string.Empty,
                        Popularity = popularity
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during API calls for city: {City}", SearchText);
            }
        }
    }
}
