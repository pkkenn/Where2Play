using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Caching.Memory;
using System.ComponentModel.DataAnnotations;
using Where2Play.Models;
using Where2Play.Services;

namespace Where2Play.Pages;

public class CityRatingModel(
    IMusicService musicService,
    ILogger<CityRatingModel> logger,
    IMemoryCache cache) : PageModel
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);
    private const int MaxResults = 10;
    private const int MinGenreLength = 2;

    // INPUT PROPERTIES
    [BindProperty(SupportsGet = true)]
    [Required(ErrorMessage = "Please enter a genre.")]
    [StringLength(100, MinimumLength = MinGenreLength, ErrorMessage = "Genre must be between 2 and 100 characters.")]
    public string BandGenre { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    [Required(ErrorMessage = "Please select a venue size.")]
    public BandPopularity Popularity { get; set; }

    [BindProperty(SupportsGet = true)]
    [Required(ErrorMessage = "Please select a region.")]
    public string TargetRegion { get; set; } = string.Empty;

    // VIEW DATA
    public List<SelectListItem> RegionOptions { get; private set; } = [];
    public List<CityFit> Results { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (!Request.QueryString.HasValue)
        {
            ModelState.Clear();
        }

        PopulateDropdowns();

        if (!IsValidSearchInput())
        {
            return;
        }

        await PerformRecommendationAsync(BandGenre, TargetRegion, Popularity, cancellationToken);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        PopulateDropdowns();

        if (!ModelState.IsValid)
        {
            return Page();
        }

        await PerformRecommendationAsync(BandGenre, TargetRegion, Popularity, cancellationToken);

        return Page();
    }

    private bool IsValidSearchInput()
    {
        return !string.IsNullOrWhiteSpace(BandGenre)
            && BandGenre.Trim().Length >= MinGenreLength
            && !string.IsNullOrWhiteSpace(TargetRegion);
    }

    private async Task PerformRecommendationAsync(string genre, string region, BandPopularity popularity, CancellationToken cancellationToken)
    {
        try
        {
            var normalizedGenre = genre.Trim();
            var normalizedRegion = region.Trim().ToUpperInvariant();
            var cacheKey = $"cityrating:{normalizedGenre}:{normalizedRegion}:{(int)popularity}";

            if (cache.TryGetValue(cacheKey, out List<CityFit>? cached) && cached is not null)
            {
                Results = cached;
                return;
            }

            var (results, error) = await musicService.RecommendCitiesAsync(
                normalizedGenre,
                normalizedRegion,
                popularity,
                cancellationToken);

            if (!string.IsNullOrEmpty(error))
            {
                logger.LogError("Error calculating city recommendations: {Error}", error);
                ModelState.AddModelError(string.Empty, $"Configuration Error: {error}");
                Results = [];
                return;
            }

            Results = (results ?? [])
                .OrderByDescending(c => c.FitScore)
                .Take(MaxResults)
                .ToList();

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration,
                Size = Math.Max(Results.Count, 1)
            };
            cache.Set(cacheKey, Results, cacheOptions);
        }
        catch (OperationCanceledException)
        {
            // Request was cancelled, don't log as error
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calculating city ratings for genre {Genre} in region {Region}", genre, region);
            ModelState.AddModelError(string.Empty, "An error occurred while analyzing data.");
        }
    }

    private void PopulateDropdowns()
    {
        RegionOptions =
        [
            new SelectListItem { Value = "NE", Text = "Northeast" },
            new SelectListItem { Value = "SE", Text = "Southeast" },
            new SelectListItem { Value = "MW", Text = "Midwest" },
            new SelectListItem { Value = "SW", Text = "Southwest" },
            new SelectListItem { Value = "W", Text = "West Coast" }
        ];
    }
}