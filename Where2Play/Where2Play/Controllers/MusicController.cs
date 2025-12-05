using Microsoft.AspNetCore.Mvc;
using Where2Play.Models;
using Where2Play.Services;

namespace Where2Play.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [ApiExplorerSettings(IgnoreApi = true)]
    public class MusicController : ControllerBase
    {
        private readonly IMusicService _musicService;
        private readonly ILogger<MusicController> _logger;

        public MusicController(IMusicService musicService, ILogger<MusicController> logger)
        {
            _musicService = musicService;
            _logger = logger;
        }

        /// <summary>
        /// Default index for the API controller root.
        /// </summary>
        [HttpGet]
        public IActionResult Index()
        {
            var endpoints = new[]
            {
                "/api/music/findsimilarartists?genre={genre}&limit={n}",
                "/api/music/recommendcities?genre={genre}&region={region}&popularity={Small|Medium|Large}",
                "/api/music/searchbycity?q={city}"
            };

            return new JsonResult(new { endpoints });
        }

        /// <summary>
        /// Finds similar artists by genre.
        /// Kept for backward compatibility but routed in ConsolidatedApiController as primary implementation.
        /// </summary>
        [HttpGet("findsimilarartists")]
        public Task<IActionResult> FindSimilarArtists(string genre, int limit = 10)
        {
            // delegate to the consolidated controller via normal routing (kept to avoid breaking callers that use this controller directly)
            // The real implementation lives in `ConsolidatedApiController` which also serves `/api/music/findsimilarartists`.
            return Task.FromResult<IActionResult>(BadRequest("Use the consolidated API endpoints under /api/v1 or /api/music"));
        }
    }
}
