namespace EmojiWaterfall.Api;

public sealed class StartupFetchService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<StartupFetchService> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
#pragma warning disable CA1873
        logger.LogInformation("{Service} is running", nameof(StartupFetchService));
#pragma warning restore CA1873

        try
        {
            await using AsyncServiceScope scope = serviceScopeFactory.CreateAsyncScope();

            EmojiService scopedEmojiService =
                scope.ServiceProvider.GetRequiredService<EmojiService>();

            await scopedEmojiService.PrimeCacheAsync(stoppingToken);

            logger.LogInformation("Emoji cache primed.");
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Unable to prime cache");
        }
    }
}
