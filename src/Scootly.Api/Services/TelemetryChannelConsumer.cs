using Scootly.Application.Telemetry;

namespace Scootly.Api.Services;

/// <summary>
/// Telemetri kuyruğunu partiler halinde boşaltır: en fazla <see cref="MaxBatchSize"/> okuma ya da
/// <see cref="MaxBatchDelay"/> süresi dolduğunda tek transaction'da yazılır (okuma başına ayrı SaveChanges yok).
/// </summary>
public sealed class TelemetryChannelConsumer : BackgroundService
{
    public const int MaxBatchSize = 500;
    public static readonly TimeSpan MaxBatchDelay = TimeSpan.FromSeconds(1);

    private readonly TelemetryChannel _telemetryChannel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelemetryChannelConsumer> _logger;

    public TelemetryChannelConsumer(
        TelemetryChannel telemetryChannel,
        IServiceScopeFactory scopeFactory,
        ILogger<TelemetryChannelConsumer> logger)
    {
        _telemetryChannel = telemetryChannel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _telemetryChannel.Reader;
        var batch = new List<TelemetryReadingData>(MaxBatchSize);

        try
        {
            while (await reader.WaitToReadAsync(stoppingToken))
            {
                using var batchWindow = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                batchWindow.CancelAfter(MaxBatchDelay);

                try
                {
                    while (batch.Count < MaxBatchSize)
                    {
                        if (reader.TryRead(out var reading))
                        {
                            batch.Add(reading);
                            continue;
                        }

                        if (!await reader.WaitToReadAsync(batchWindow.Token))
                            break;
                    }
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                {
                    // Parti penceresi doldu; eldekiler yazılır.
                }

                await WriteBatchAsync(batch, stoppingToken);
                batch.Clear();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Kapanış: kuyrukta kalanları mümkün olduğunca kaydet.
            while (reader.TryRead(out var reading))
                batch.Add(reading);

            if (batch.Count > 0)
                await WriteBatchAsync(batch, CancellationToken.None);
        }
    }

    private async Task WriteBatchAsync(List<TelemetryReadingData> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
            return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<ProcessTelemetryBatchCommandHandler>();

            var result = await handler.Handle(new ProcessTelemetryBatchCommand(batch.ToList()), cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogError("Telemetri partisi kaydedilemedi ({Count} okuma): {Error}", batch.Count, result.Error);
                return;
            }

            if (result.Value!.Skipped > 0)
                _logger.LogWarning("{Skipped} telemetri okuması atlandı (araç bulunamadı veya değer geçersiz).", result.Value.Skipped);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Telemetri partisi yazılırken hata oluştu ({Count} okuma).", batch.Count);
        }
    }
}
