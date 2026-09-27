using Scootly.Concurrency.Tests.Experiments.Streaming;
using Xunit;

namespace Scootly.Concurrency.Tests;

/// <summary>
/// ADR 0016 deneyi. Çalışan bir Redpanda/Kafka gerektirir; yalnızca
/// <c>SCOOTLY_REDPANDA_BOOTSTRAP</c> ortam değişkeni ayarlıysa çalışır, aksi halde atlanır.
/// </summary>
[Trait(TestCategories.Key, TestCategories.Experiment)]
public sealed class StreamReplayTests
{
    public const string BootstrapVariable = "SCOOTLY_REDPANDA_BOOTSTRAP";

    private readonly ITestOutputHelper _output;

    public StreamReplayTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Yayinlanan_Mesaj_Farkli_Tuketici_Gruplariyla_Tekrar_Okunabilir()
    {
        var bootstrapServers = Environment.GetEnvironmentVariable(BootstrapVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(bootstrapServers), $"{BootstrapVariable} ayarlı değil; stream deneyi atlandı.");

        var vehicleId = Guid.NewGuid();

        using (var producer = new TelemetryStreamProducer(bootstrapServers!))
        {
            await producer.PublishAsync(new StreamedTelemetry(vehicleId, 41.0, 29.0, 75, DateTime.UtcNow));
        }

        string? firstReadPayload;
        using (var consumer1 = new TelemetryStreamConsumer(bootstrapServers!, $"test-group-1-{Guid.NewGuid()}"))
        {
            firstReadPayload = ReadUntil(consumer1, vehicleId);
        }

        // İkinci (farklı) tüketici grubu aynı mesajı, sanki hiç okunmamış gibi tekrar okuyabilmeli.
        string? secondReadPayload;
        using (var consumer2 = new TelemetryStreamConsumer(bootstrapServers!, $"test-group-2-{Guid.NewGuid()}"))
        {
            secondReadPayload = ReadUntil(consumer2, vehicleId);
        }

        _output.WriteLine($"1. grup: {firstReadPayload is not null}, 2. grup: {secondReadPayload is not null}");

        Assert.NotNull(firstReadPayload);
        Assert.Equal(firstReadPayload, secondReadPayload);
    }

    private static string? ReadUntil(TelemetryStreamConsumer consumer, Guid vehicleId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);

        while (DateTime.UtcNow < deadline)
        {
            var result = consumer.ConsumeOnce(TimeSpan.FromSeconds(1));

            if (result?.Message.Key == vehicleId.ToString())
                return result.Message.Value;
        }

        return null;
    }
}
