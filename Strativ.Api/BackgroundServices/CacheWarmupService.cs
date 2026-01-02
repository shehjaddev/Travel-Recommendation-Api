using Strativ.Api.Services;

namespace Strativ.Api.BackgroundServices;

public class CacheWarmupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CacheWarmupService> _logger;

    public CacheWarmupService(IServiceProvider serviceProvider, ILogger<CacheWarmupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Warm up immediately on startup
        await WarmupCacheAsync();

        // Then every 30 minutes
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));

        while (!stoppingToken.IsCancellationRequested &&
               await timer.WaitForNextTickAsync(stoppingToken))
        {
            await WarmupCacheAsync();
        }
    }

    private async Task WarmupCacheAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var weatherService = scope.ServiceProvider.GetRequiredService<IWeatherService>();

        try
        {
            var (top10, fromCache) = await weatherService.GetTop10DistrictsAsync();
            _logger.LogInformation("Cache warmup completed. Top10 count: {Count}, FromCache: {FromCache}", top10.Count, fromCache);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cache warmup failed");
        }
    }
}