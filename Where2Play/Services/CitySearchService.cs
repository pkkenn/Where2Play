using System.Net.Http.Headers;
using Where2Play.Models;

namespace Where2Play.Services
{
    public class CitySearchService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<CitySearchService> _logger;
        private static MusicBrainzCache? _artistCache;

        // Pre-seeded demo data
        public static readonly List<EventSummary> _demoEvents = new()
        {
            new EventSummary
            {
                ArtistName = "The Killers",
                Genre = "Rock, Alternative Rock",
                Country = "United States",
                Popularity = "High",
                Venue = "Riverbend Music Center",
                City = "Cincinnati",
                Date = DateTime.Parse("2025-06-15"),
                Url = "https://www.setlist.fm/setlist/the-killers/2025/riverbend-music-center-cincinnati-oh.html"
            },
            new EventSummary
            {
                ArtistName = "Tyler Childers",
                Genre = "Country, Folk",
                Country = "United States",
                Popularity = "High",
                Venue = "Heritage Bank Center",
                City = "Cincinnati",
                Date = DateTime.Parse("2025-07-20"),
                Url = "https://www.setlist.fm/setlist/tyler-childers/2025/heritage-bank-center-cincinnati-oh.html"
            },
            new EventSummary
            {
                ArtistName = "Jack Harlow",
                Genre = "Hip Hop, Rap",
                Country = "United States",
                Popularity = "High",
                Venue = "Bridgestone Arena",
                City = "Nashville",
                Date = DateTime.Parse("2025-08-10"),
                Url = "https://www.setlist.fm/setlist/jack-harlow/2025/bridgestone-arena-nashville-tn.html"
            },
            new EventSummary
            {
                ArtistName = "Foo Fighters",
                Genre = "Rock, Alternative Rock",
                Country = "United States",
                Popularity = "High",
                Venue = "Nissan Stadium",
                City = "Nashville",
                Date = DateTime.Parse("2025-09-05"),
                Url = "https://www.setlist.fm/setlist/foo-fighters/2025/nissan-stadium-nashville-tn.html"
            },
            new EventSummary
            {
                ArtistName = "Machine Gun Kelly",
                Genre = "Rock, Pop Punk",
                Country = "United States",
                Popularity = "High",
                Venue = "Rocket Mortgage FieldHouse",
                City = "Cleveland",
                Date = DateTime.Parse("2025-12-20"),
                Url = "https://www.setlist.fm/setlist/machine-gun-kelly/2025/rocket-mortgage-fieldhouse-cleveland-oh.html"
            },
            new EventSummary
            {
                ArtistName = "The Black Keys",
                Genre = "Rock, Blues Rock",
                Country = "United States",
                Popularity = "High",
                Venue = "FirstEnergy Stadium",
                City = "Cleveland",
                Date = DateTime.Parse("2026-01-15"),
                Url = "https://www.setlist.fm/setlist/the-black-keys/2026/firstenergy-stadium-cleveland-oh.html"
            },
            new EventSummary
            {
                ArtistName = "Twenty One Pilots",
                Genre = "Alternative Rock, Pop",
                Country = "United States",
                Popularity = "High",
                Venue = "Nationwide Arena",
                City = "Columbus",
                Date = DateTime.Parse("2025-12-30"),
                Url = "https://www.setlist.fm/setlist/twenty-one-pilots/2025/nationwide-arena-columbus-oh.html"
            },
            new EventSummary
            {
                ArtistName = "Metallica",
                Genre = "Heavy Metal, Thrash Metal",
                Country = "United States",
                Popularity = "High",
                Venue = "Soldier Field",
                City = "Chicago",
                Date = DateTime.Parse("2026-02-10"),
                Url = "https://www.setlist.fm/setlist/metallica/2026/soldier-field-chicago-il.html"
            }
        };

        public CitySearchService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<CitySearchService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;

            // Load MusicBrainz cache on first instantiation
            if (_artistCache == null)
            {
                var cachePath = Path.Combine(AppContext.BaseDirectory, "Data", "musicbrainz-cache.json");
                _artistCache = CachedArtist.LoadFromFile(cachePath);
                if (_artistCache != null)
                {
                    _logger.LogInformation("Loaded {Count} artists from MusicBrainz cache", _artistCache.Artists.Count);
                }
            }
        }

        public static List<EventSummary> GetAllDemoEvents()
        {
            return new List<EventSummary>(_demoEvents);
        }

        public async Task<List<EventSummary>> SearchCityEventsAsync(string cityName)
        {
            var results = new List<EventSummary>();
            string normalizedCity = cityName?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedCity))
            {
                return results;
            }

            // First, try to get live data from Setlist.fm
            try
            {
                var apiKey = _configuration["ApiKeys:SetlistFm"];
                if (!string.IsNullOrEmpty(apiKey))
                {
                    var httpClient = _httpClientFactory.CreateClient();
                    httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
                    httpClient.DefaultRequestHeaders.Add("User-Agent", "Where2Play/1.0 (dickendd@mail.uc.edu)");
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var setlistUrl = $"https://api.setlist.fm/rest/1.0/search/setlists?cityName={normalizedCity}";
                    var response = await httpClient.GetAsync(setlistUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        var searchResult = Welcome.FromJson(jsonResponse);

                        if (searchResult?.Setlist != null && searchResult.Setlist.Length > 0)
                        {
                            foreach (var setlist in searchResult.Setlist)
                            {
                                string artistName = setlist.Artist?.Name ?? "Unknown Artist";
                                string country = "N/A";
                                string popularity = "N/A";
                                string genre = "N/A";

                                // First, try to get data from cache
                                if (setlist.Artist?.Mbid != null && setlist.Artist.Mbid != Guid.Empty)
                                {
                                    var cachedArtist = _artistCache?.Artists.FirstOrDefault(a => 
                                        a.Mbid.Equals(setlist.Artist.Mbid.ToString(), StringComparison.OrdinalIgnoreCase));

                                    if (cachedArtist != null)
                                    {
                                        country = cachedArtist.Country;
                                        popularity = cachedArtist.Popularity;
                                        genre = string.Join(", ", cachedArtist.Genres);
                                    }
                                    else
                                    {
                                        // Fall back to live API call
                                        var mbClient = _httpClientFactory.CreateClient();
                                        mbClient.DefaultRequestHeaders.Add("User-Agent", "Where2Play/1.0 (dickendd@mail.uc.edu)");
                                        mbClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                                        var mbUrl = $"https://musicbrainz.org/ws/2/artist/{setlist.Artist.Mbid}?inc=ratings+genres&fmt=json";
                                        var mbResponse = await mbClient.GetAsync(mbUrl);

                                        if (mbResponse.IsSuccessStatusCode)
                                        {
                                            var mbJson = await mbResponse.Content.ReadAsStringAsync();
                                            var artistDetails = MusicBrainzArtist.FromJson(mbJson);

                                            if (!string.IsNullOrWhiteSpace(artistDetails?.Country))
                                                country = artistDetails.Country;

                                            if (artistDetails?.Rating?.Value != null)
                                                popularity = $"{(artistDetails.Rating.Value / 5.0) * 100:F0}%";

                                            if (artistDetails?.Genres != null && artistDetails.Genres.Count > 0)
                                            {
                                                var genreNames = artistDetails.Genres.Select(g => g.Name).Take(3);
                                                genre = string.Join(", ", genreNames);
                                            }
                                        }

                                        await Task.Delay(1000);
                                    }
                                }

                                DateTime? eventDate = null;
                                if (DateTime.TryParseExact(setlist.EventDate,
                                       "dd-MM-yyyy",
                                       System.Globalization.CultureInfo.InvariantCulture,
                                       System.Globalization.DateTimeStyles.None,
                                       out var parsedDate))
                                {
                                    eventDate = parsedDate;
                                }

                                results.Add(new EventSummary
                                {
                                    ArtistName = artistName,
                                    Genre = genre,
                                    Country = country,
                                    Venue = setlist.Venue?.Name ?? "Unknown Venue",
                                    City = setlist.Venue?.City?.Name ?? normalizedCity,
                                    Date = eventDate,
                                    Url = setlist.Url?.ToString() ?? string.Empty,
                                    Popularity = popularity
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during Setlist.fm API call for city: {City}", cityName);
            }

            // Always supplement with demo events (either matching city or all if no live results)
            if (results.Count == 0)
            {
                // If no live results, return all demo events as samples
                results.AddRange(_demoEvents);
                _logger.LogInformation("No live events found for {City}, returning demo events", normalizedCity);
            }
            else
            {
                // If we have live results, also add demo events from matching city
                results.AddRange(_demoEvents.Where(e => 
                    e.City?.Equals(normalizedCity, StringComparison.OrdinalIgnoreCase) == true));
            }

            return results;
        }

        public async Task<List<EventSummary>> SearchArtistEventsAsync(string artistName)
        {
            var results = new List<EventSummary>();
            string normalizedArtist = artistName?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(normalizedArtist))
            {
                return results;
            }

            try
            {
                var apiKey = _configuration["ApiKeys:SetlistFm"];
                if (string.IsNullOrEmpty(apiKey))
                {
                    _logger.LogError("Setlist.fm API key not configured.");
                    return results;
                }

                var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Where2Play/1.0 (dickendd@mail.uc.edu)");
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Search for artist on Setlist.fm
                var setlistUrl = $"https://api.setlist.fm/rest/1.0/search/setlists?artistName={System.Web.HttpUtility.UrlEncode(normalizedArtist)}";
                var response = await httpClient.GetAsync(setlistUrl);

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    var searchResult = Welcome.FromJson(jsonResponse);

                    if (searchResult?.Setlist != null && searchResult.Setlist.Length > 0)
                    {
                        foreach (var setlist in searchResult.Setlist)
                        {
                            string artistNameResult = setlist.Artist?.Name ?? "Unknown Artist";
                            string country = "N/A";
                            string popularity = "N/A";
                            string genre = "N/A";

                            // Try to get data from cache
                            if (setlist.Artist?.Mbid != null && setlist.Artist.Mbid != Guid.Empty)
                            {
                                var cachedArtist = _artistCache?.Artists.FirstOrDefault(a => 
                                    a.Mbid.Equals(setlist.Artist.Mbid.ToString(), StringComparison.OrdinalIgnoreCase));

                                if (cachedArtist != null)
                                {
                                    country = cachedArtist.Country;
                                    popularity = cachedArtist.Popularity;
                                    genre = string.Join(", ", cachedArtist.Genres);
                                }
                                else
                                {
                                    // Fall back to live API call
                                    var mbClient = _httpClientFactory.CreateClient();
                                    mbClient.DefaultRequestHeaders.Add("User-Agent", "Where2Play/1.0 (dickendd@mail.uc.edu)");
                                    mbClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                                    var mbUrl = $"https://musicbrainz.org/ws/2/artist/{setlist.Artist.Mbid}?inc=ratings+genres&fmt=json";
                                    var mbResponse = await mbClient.GetAsync(mbUrl);

                                    if (mbResponse.IsSuccessStatusCode)
                                    {
                                        var mbJson = await mbResponse.Content.ReadAsStringAsync();
                                        var artistDetails = MusicBrainzArtist.FromJson(mbJson);

                                        if (!string.IsNullOrWhiteSpace(artistDetails?.Country))
                                            country = artistDetails.Country;

                                        if (artistDetails?.Rating?.Value != null)
                                            popularity = $"{(artistDetails.Rating.Value / 5.0) * 100:F0}%";

                                        if (artistDetails?.Genres != null && artistDetails.Genres.Count > 0)
                                        {
                                            var genreNames = artistDetails.Genres.Select(g => g.Name).Take(3);
                                            genre = string.Join(", ", genreNames);
                                        }
                                    }

                                    await Task.Delay(1000);
                                }
                            }

                            DateTime? eventDate = null;
                            if (DateTime.TryParseExact(setlist.EventDate,
                                   "dd-MM-yyyy",
                                   System.Globalization.CultureInfo.InvariantCulture,
                                   System.Globalization.DateTimeStyles.None,
                                   out var parsedDate))
                            {
                                eventDate = parsedDate;
                            }

                            results.Add(new EventSummary
                            {
                                ArtistName = artistNameResult,
                                Genre = genre,
                                Country = country,
                                Venue = setlist.Venue?.Name ?? "Unknown Venue",
                                City = setlist.Venue?.City?.Name ?? "Unknown City",
                                Date = eventDate,
                                Url = setlist.Url?.ToString() ?? string.Empty,
                                Popularity = popularity
                            });
                        }

                        _logger.LogInformation("Found {Count} events for artist: {Artist}", results.Count, normalizedArtist);
                    }
                    else
                    {
                        _logger.LogInformation("No setlists found for artist: {Artist}", normalizedArtist);
                    }
                }
                else
                {
                    _logger.LogError("Setlist.fm API failed with status {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during artist search: {Artist}", artistName);
            }

            return results;
        }
    }
}
