namespace AspNetRequestPipeline;

public sealed class CatalogWorker(IServiceScopeFactory scopes) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await using var scope = scopes.CreateAsyncScope();
            var job = scope.ServiceProvider.GetRequiredService<CatalogJob>();
            await job.RunAsync(stoppingToken);
        }
    }
}

public sealed class CatalogJob(ILogger<CatalogJob> logger)
{
    public Task RunAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("The demonstration catalog job ran in its own scope.");
        return Task.CompletedTask;
    }
}
