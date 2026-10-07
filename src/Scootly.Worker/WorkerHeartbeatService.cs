namespace Scootly.Worker;

/// <summary>
/// Worker'ın HTTP ucu olmadığı için sağlık sinyali dosya tabanlıdır: sürecin ayakta olduğunu ve
/// arka plan iş parçacıklarının çalışabildiğini gösteren zaman damgası periyodik olarak yazılır.
/// Dış kontrol (örn. Compose healthcheck) dosyanın son değiştirilme zamanına bakar.
/// Not: bu yalnızca "süreç canlı" sinyalidir, tek tek işlerin (ödeme yeniden deneme vb.) doğru çalıştığını kanıtlamaz.
/// </summary>
public sealed class WorkerHeartbeatService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly string _path;
    private readonly ILogger<WorkerHeartbeatService> _logger;

    public WorkerHeartbeatService(IConfiguration configuration, ILogger<WorkerHeartbeatService> logger)
    {
        var configured = configuration["Worker:HeartbeatPath"];

        _path = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Path.GetTempPath(), "scootly-worker-heartbeat")
            : configured;

        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker heartbeat dosyası: {Path}", _path);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await File.WriteAllTextAsync(_path, DateTime.UtcNow.ToString("O"), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Worker heartbeat dosyası yazılamadı: {Path}", _path);
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
