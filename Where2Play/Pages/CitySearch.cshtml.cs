using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using Where2Play.Models; 

namespace Where2Play.Pages
{
    public class CitySearchModel : PageModel
    {
        
        [BindProperty]
        public string SearchCity { get; set; }

        
        public List<ConcertResultViewModel> FinalResults { get; set; }

        
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;


        public CitySearchModel(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }
        
        public void OnGet()
        {
        }

        
        public async Task OnPostAsync()
        {
            // Don't do anything if the search box is empty
            if (string.IsNullOrWhiteSpace(SearchCity))
            {
                return;
            }

            FinalResults = new List<ConcertResultViewModel>();

            try
            {
                // --- Part 1: Call Setlist.FM API ---

                var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Add("x-api-key", "2QokW-SPmnefJFPpIoP0ABeRrFF5Rm-t8XxZ");
                httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                var setlistUrl = $"https://api.setlist.fm/rest/1.0/search/setlists?cityName={SearchCity}";

                var response = await httpClient.GetAsync(setlistUrl);

                if (!response.IsSuccessStatusCode)
                {
                    // If this call fails, stop and show no results
                    return;
                }

                string jsonResponse = await response.Content.ReadAsStringAsync();

                // Deserialize using your 'Welcome' class
                var searchResult = Welcome.FromJson(jsonResponse);

                // --- Part 2: Loop and Call MusicBrainz API ---


                foreach (var setlist in searchResult.Setlist)
                {
                    // Get the MusicBrainz ID (Mbid) from the setlist artist
                    string mbid = setlist.Artist.Mbid.ToString();

                    // Create a *new* client for the MusicBrainz call
                    var mbClient = _httpClientFactory.CreateClient();

                    // !! CRITICAL: MusicBrainz requires a User-Agent header !!
                    mbClient.DefaultRequestHeaders.Add("User-Agent", "Where2Play/1.0 ( dickendd@mail.uc.edu )");
                    mbClient.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));

                    var mbUrl = $"https://musicbrainz.org/ws/2/artist/{mbid}?inc=tags+ratings&fmt=json";

                    var mbResponse = await mbClient.GetAsync(mbUrl);

                    // Set default values in case MusicBrainz fails or has no data
                    string genre = "N/A";
                    string popularity = "N/A";

                    if (mbResponse.IsSuccessStatusCode)
                    {
                        string mbJson = await mbResponse.Content.ReadAsStringAsync();

                        // Deserialize using your 'MusicBrainzArtist' class
                        var artistDetails = MusicBrainzArtist.FromJson(mbJson);

                        // Safely get the rating and convert it to a percentage
                        if (artistDetails.Rating.Value != null && artistDetails.Rating.Value > 0)
                        {
                            // Convert the 5-star rating to a 100% scale
                            popularity = $"{artistDetails.Rating.Value * 20}%";
                            // Convert the 5-star rating to a percentage
                            popularity = $"{(artistDetails.Rating.Value / 5.0) * 100:F0}%";
                        }
                    }
                    else
                    {
                        // It's good practice to log when an optional API call fails,
                        // even if you continue execution.
                        // In a real application, you would use a logging framework.
                        Console.WriteLine($"MusicBrainz API call failed for MBID {mbid} with status code {mbResponse.StatusCode}");
                    }


                    // --- Part 3: Combine Data ---

                    // Add the combined data to our final list
                    FinalResults.Add(new ConcertResultViewModel
                    {
                        ArtistName = setlist.Artist.Name,
                        VenueName = setlist.Venue.Name,
                        EventDate = setlist.EventDate,
                        Genre = genre,
                        Popularity = popularity
                    });

                    // !! CRITICAL: Wait 1 second to respect MusicBrainz API rate limit !!
                    // Wait 1 second to respect MusicBrainz API rate limit
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during API calls: {ex.Message}");
                // Ensure FinalResults is empty so "No results" displays
                FinalResults = new List<ConcertResultViewModel>();
            }
        }
    }
}