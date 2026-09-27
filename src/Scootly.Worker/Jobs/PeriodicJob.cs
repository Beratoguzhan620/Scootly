namespace Scootly.Worker.Jobs;

/// <summary>
/// Periyodik işlerin ortak iskeleti (ADR 0011): her tur kendi DI scope'unda çalışır, bir turdaki hata
/// loglanır ve bir sonraki turu engellemez; kapanış (iptal) hata sayılmaz.
/// </summary>
public abstract class PeriodicJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    protected PeriodicJob(IServiceScopeFactory scopeFactory, ILogger logger)
    {
        _scopeFactory = scopeFactory;
        Logger = logger;
    }

    protected ILogger Logger { get; }

    protected abstract TimeSpan Interval { get; }

    protected abstract Task RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                await RunOnceAsync(scope.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "{Job} turunda beklenmeyen hata oluştu.", GetType().Name);
            }
        }
        while (await WaitForNextTickAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>Her komutu ayrı bir scope'ta çalıştırır: bir kaydın hatası diğerlerinin değişiklik izleyicisini kirletmez.</summary>
    protected async Task ForEachInOwnScopeAsync<T>(
        IEnumerable<T> items,
        Func<IServiceProvider, T, CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var scope = _scopeFactory.CreateScope();
                await action(scope.ServiceProvider, item, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogError(ex, "{Job} bir kaydı işleyemedi: {Item}", GetType().Name, item);
            }
        }
    }
}
