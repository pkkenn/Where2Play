using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using Where2Play.Models;

namespace Where2Play.Pages
{
    public class CitySearchModel : PageModel
    {
        [BindProperty]
        public string SearchCity { get; set; }

        public List<EventSummary> FinalResults { get; set; } = new List<EventSummary>();

        private readonly IHttpClientFactory _httpClientFactory;

        public CitySearchModel(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public void OnGet() { }

        public async Task OnPostAsync()
        {
            FinalResults.Clear();

            if (string.IsNullOrWhiteSpace(SearchCity))
            {
                Console.WriteLine("SearchCity is empty.");
                return;
            }

            string normalizedCity = System.Globalization.CultureInfo.CurrentCulture.TextInfo
                                    .ToTitleCase(SearchCity.Trim().ToLower());
            Console.WriteLine($"Normalized city: '{normalizedCity}'");

            try
            {
                // --- Call Setlist.fm API ---
                var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Add("x-api-key", "2QokW-SPmnefJFPpIoP0ABeRrFF5Rm-t8XxZ");
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Where2Play/1.0 (dickendd@mail.uc.edu)");
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var setlistUrl = $"https://api.setlist.fm/rest/1.0/search/setlists?cityName={normalizedCity}";
                Console.WriteLine($"Calling Setlist.fm API: {setlistUrl}");

                var response = await httpClient.GetAsync(setlistUrl);

                if (!response.IsSuccessStatusCode)
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Setlist.fm API failed: {response.StatusCode}, {errorContent}");
                    return;
                }

                string jsonResponse = await response.Content.ReadAsStringAsync();
                var searchResult = Welcome.FromJson(jsonResponse);

                if (searchResult?.Setlist == null || searchResult.Setlist.Length == 0)
                {
                    Console.WriteLine("No setlists found for this city.");
                    return;
                }

                foreach (var setlist in searchResult.Setlist)
                {
                    string artistName = setlist.Artist?.Name ?? "Unknown Artist";
                    string country = "N/A";
                    string popularity = "N/A";

                    // MusicBrainz enrichment if MBID exists
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

                        await Task.Delay(1000); // Respect MusicBrainz rate limit
                    }

                    DateTime? eventDate = null;
                    if (DateTime.TryParse(setlist.EventDate, out var parsedDate))
                        eventDate = parsedDate;

                    FinalResults.Add(new EventSummary
                    {
                        ArtistName = artistName,
                        Genre = "N/A", // Optional placeholder
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
                Console.WriteLine($"Error during API calls: {ex.Message}");
            }
        }
    }
}
