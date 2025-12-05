using Microsoft.Extensions.Caching.Memory;
using Where2Play.Models;

namespace Where2Play.Services
{
    // Background worker that periodically refreshes recommendations for configured genres/regions
    public class RecommendationBackgroundService : BackgroundService
    {
        private readonly ILogger<RecommendationBackgroundService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _config;

        public RecommendationBackgroundService(ILogger<RecommendationBackgroundService> logger, IServiceScopeFactory scopeFactory, IConfiguration config)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Recommendation background service starting");

            // simple schedule: refresh every X minutes
            var interval = int.TryParse(_config["Recommendations:BackgroundIntervalMinutes"], out var m) ? m : 30;

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await RefreshRecommendations(stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        // Normal shutdown
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during recommendation refresh");
                    }

                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(interval), stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        // Normal shutdown
                        break;
                    }
                }
            }
            finally
            {
                _logger.LogInformation("Recommendation background service stopping");
            }
        }

        private async Task RefreshRecommendations(CancellationToken ct)
        {
            // Read configured genres and regions from config
            var genres = _config["Recommendations:Genres"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>();
            var regions = _config["Recommendations:Regions"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Array.Empty<string>();

            if (!genres.Any() || !regions.Any())
            {
                _logger.LogInformation("No configured genres/regions for background recommendations");
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var api = scope.ServiceProvider.GetRequiredService<IMusicService>();

            foreach (var genre in genres)
            {
                foreach (var region in regions)
                {
                    if (ct.IsCancellationRequested) return;

                    _logger.LogInformation("Refreshing recommendations for {Genre} / {Region}", genre, region);
                    var (results, err) = await api.RecommendCitiesAsync(genre, region);

                    if (!string.IsNullOrEmpty(err))
                    {
                        _logger.LogWarning("Background RecommendCitiesAsync error for {Genre}/{Region}: {Err}", genre, region, err);
                    }
                }
            }
        }
    }
}
