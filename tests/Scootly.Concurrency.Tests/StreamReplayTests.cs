using System.Text.Json;
using Scootly.Domain.Geo;
using Scootly.Domain.Telemetry;
using Scootly.Infrastructure.Streaming;
using Xunit;

namespace Scootly.Concurrency.Tests;

public sealed class StreamReplayTests
{
    [Fact]
    public async Task Yayinlanan_Mesaj_Farkli_Tuketici_Gruplariyla_Tekrar_Okunabilir()
    {
        var vehicleId = Guid.NewGuid();

        await using (var producer = new TelemetryStreamProducer())
        {
            var reading = new TelemetryReading(
                Guid.NewGuid(), vehicleId, new GeoPoint(41.0, 29.0), 75, DateTime.UtcNow);

            await producer.PublishAsync(reading);
        }

        await Task.Delay(1000);

        // İlk tüketici grubu — mesajı okur
        string? firstReadPayload;
        using (var consumer1 = new TelemetryStreamConsumer($"test-group-1-{Guid.NewGuid()}"))
        {
            var result = consumer1.ConsumeOnce(TimeSpan.FromSeconds(10));
            firstReadPayload = result?.Message.Value;
        }

        // İkinci (farklı) tüketici grubu — AYNI mesajı, sanki hiç okunmamış gibi TEKRAR okuyabiliyor
        string? secondReadPayload;
        using (var consumer2 = new TelemetryStreamConsumer($"test-group-2-{Guid.NewGuid()}"))
        {
            var result = consumer2.ConsumeOnce(TimeSpan.FromSeconds(10));
            secondReadPayload = result?.Message.Value;
        }

        throw new Xunit.Sdk.XunitException(
            $"1. tüketici grubu okudu mu: {firstReadPayload is not null} | " +
            $"2. (farklı) tüketici grubu AYNI mesajı okuyabildi mi: {secondReadPayload is not null} | " +
            $"İkisi aynı veri mi: {firstReadPayload == secondReadPayload}");
    }
}