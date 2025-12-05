using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Where2Play.Models;
using Where2Play.Services;
using System.Globalization;

namespace Where2Play.Pages;

public class SearchModel(IMusicService musicService, ILogger<SearchModel> logger) : PageModel
{
    private const int DefaultPageSize = 5;

    [BindProperty]
    public string SearchText { get; set; } = string.Empty;

    public List<EventSummary> FinalResults { get; private set; } = [];
    public List<EventSummary> PagedResults { get; private set; } = [];
    public new int Page { get; private set; } = 1;
    public int PageSize { get; private set; } = DefaultPageSize;
    public bool HasMore { get; private set; }

    public async Task OnGetAsync(string? q, string? query, int page = 1, CancellationToken cancellationToken = default)
    {
        FinalResults = [];
        PagedResults = [];

        var searchInput = !string.IsNullOrWhiteSpace(q) ? q : query;

        if (string.IsNullOrWhiteSpace(searchInput))
        {
            return;
        }

        SearchText = searchInput;
        Page = Math.Max(1, page);

        await PerformSearchAsync(SearchText, cancellationToken);
    }

    public async Task OnPostAsync(CancellationToken cancellationToken)
    {
        FinalResults = [];
        PagedResults = [];
        Page = 1;

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            logger.LogWarning("Search attempted with empty search text.");
            return;
        }

        await PerformSearchAsync(SearchText, cancellationToken);
    }

    private async Task PerformSearchAsync(string input, CancellationToken cancellationToken)
    {
        try
        {
            // Normalize input for city queries (title case)
            var normalized = CultureInfo.CurrentCulture.TextInfo
                .ToTitleCase(input.Trim().ToLower());

            // Try city search first
            var (cityResults, cityError) = await musicService.SearchCityAsync(normalized, cancellationToken);

            if (!string.IsNullOrEmpty(cityError))
            {
                logger.LogError("SearchCityAsync returned error: {Error}", cityError);
            }

            if (cityResults is { Count: > 0 })
            {
                FinalResults = cityResults;
                ApplyPaging();
                return;
            }

            // Fallback: treat input as an artist name
            var (artistResults, artistError) = await musicService.SearchArtistAsync(input, 5, cancellationToken);

            if (!string.IsNullOrEmpty(artistError))
            {
                logger.LogError("SearchArtistAsync returned error: {Error}", artistError);
            }

            if (artistResults is { Count: > 0 })
            {
                FinalResults = artistResults;
                ApplyPaging();
                return;
            }

            logger.LogInformation("No setlists found for query: {Query}", input);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred during search for: {Query}", input);
        }
    }

    private void ApplyPaging()
    {
        var take = Math.Max(PageSize * Page, PageSize);
        PagedResults = FinalResults.Take(take).ToList();
        HasMore = FinalResults.Count > take;
    }
}
