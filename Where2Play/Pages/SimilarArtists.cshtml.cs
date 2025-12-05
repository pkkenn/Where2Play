using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Where2Play.Services;

namespace Where2Play.Pages;

public class SimilarArtistsModel(IMusicService musicService, ILogger<SimilarArtistsModel> logger) : PageModel
{
    [BindProperty]
    public string ArtistName { get; set; } = string.Empty;

    public List<string> Genres { get; private set; } = [];

    public List<dynamic> SimilarArtists { get; private set; } = [];

    public string? Error { get; private set; }

    public async Task OnGetAsync(string? q, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(q))
        {
            ArtistName = q;
            await SearchAsync(cancellationToken);
        }
    }

    public async Task OnPostAsync(CancellationToken cancellationToken)
    {
        Genres = [];
        SimilarArtists = [];
        Error = null;

        if (string.IsNullOrWhiteSpace(ArtistName))
        {
            Error = "Enter an artist name.";
            return;
        }

        await SearchAsync(cancellationToken);
    }

    private async Task SearchAsync(CancellationToken cancellationToken)
    {
        try
        {
            var (genres, gErr) = await musicService.GetArtistGenresAsync(ArtistName, cancellationToken);
            if (!string.IsNullOrEmpty(gErr))
            {
                logger.LogWarning("GetArtistGenresAsync error: {Err}", gErr);
            }

            Genres = genres ?? [];

            if (Genres.Count > 0)
            {
                // Use top genre to find similar artists
                var top = Genres[0];
                var (artists, aErr) = await musicService.FindArtistsByGenreAsync(top, 12, cancellationToken);
                if (!string.IsNullOrEmpty(aErr))
                {
                    logger.LogWarning("FindArtistsByGenreAsync error: {Err}", aErr);
                }

                SimilarArtists = artists
                    .Select(a => new
                    {
                        a.Name,
                        a.Country,
                        Genres = a.Genres?.Select(g => g.Name).ToArray(),
                        a.Id
                    })
                    .Cast<dynamic>()
                    .ToList();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in SimilarArtists search for {ArtistName}", ArtistName);
            Error = ex.Message;
        }
    }
}
