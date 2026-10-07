using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Scootly.Api.Services;
using Scootly.Application.Abstractions;
using Scootly.Application.IntegrationEvents;
using Scootly.Domain.Riding;
using Scootly.Infrastructure.Messaging.Consumers;
using Scootly.Testing;
using Xunit;

namespace Scootly.Api.IntegrationTests;

/// <summary>Tüketicilerin mesaj işleme mantığı, broker olmadan (gerçek veritabanı ve sahte sağlayıcıyla).</summary>
[Collection(ApiCollection.Name)]
public sealed class RideChargeConsumerTests
{
    private readonly ScootlyApiFactory _factory;

    public RideChargeConsumerTests(ScootlyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Tamamlanan_Surus_Mesaji_Odemeyi_Almali_Ve_Onaylanmali()
    {
        var rideId = await SeedPendingRideAsync();

        var result = await ProcessAsync(RideMessage(rideId));

        Assert.Equal(ConsumeResult.Ack, result);
        var ride = await _factory.WithDbContextAsync(db => db.Rides.AsNoTracking().SingleAsync(r => r.Id == rideId));
        Assert.Equal(PaymentStatus.Paid, ride.PaymentStatus);
    }

    [Fact]
    public async Task Saglayiciya_Ulasilamazsa_Mesaj_Yeniden_Denenmeli()
    {
        var rideId = await SeedPendingRideAsync();
        _factory.PaymentGateway.NextOutcome = PaymentGatewayOutcome.Unavailable;

        try
        {
            var result = await ProcessAsync(RideMessage(rideId));

            Assert.Equal(ConsumeResult.Retry, result);
        }
        finally
        {
            _factory.PaymentGateway.NextOutcome = PaymentGatewayOutcome.Approved;
        }
    }

    [Theory]
    [InlineData("{ bozuk")]
    [InlineData("{\"RideId\":\"00000000-0000-0000-0000-000000000000\"}")]
    public async Task Bozuk_Mesaj_DLQya_Gitmeli(string body)
    {
        var result = await ProcessAsync(new ReceivedMessage(IntegrationEventNames.RideCompleted, Guid.NewGuid().ToString(), body));

        Assert.Equal(ConsumeResult.DeadLetter, result);
    }

    [Fact]
    public async Task Bilinmeyen_Surus_DLQya_Gitmeli()
    {
        var result = await ProcessAsync(RideMessage(Guid.NewGuid()));

        Assert.Equal(ConsumeResult.DeadLetter, result);
    }

    private async Task<ConsumeResult> ProcessAsync(ReceivedMessage message)
    {
        using var scope = _factory.Services.CreateScope();
        return await RideChargeConsumer.ProcessAsync(message, scope.ServiceProvider, NullLogger.Instance, CancellationToken.None);
    }

    private static ReceivedMessage RideMessage(Guid rideId) => new(
        IntegrationEventNames.RideCompleted,
        Guid.NewGuid().ToString(),
        JsonSerializer.Serialize(new RideCompletedIntegrationEvent(rideId, Guid.NewGuid(), Guid.NewGuid(), 10, 500, 25m)));

    private async Task<Guid> SeedPendingRideAsync()
    {
        var vehicle = await _factory.SeedVehicleAsync();
        var ride = TestData.NewActiveRide(Guid.NewGuid(), vehicle.Id, DateTime.UtcNow.AddMinutes(-10));
        ride.Complete(new Domain.Geo.GeoPoint(41.0, 29.0), DateTime.UtcNow, Tariff.Standard);

        await _factory.WithDbContextAsync(async db =>
        {
            db.Rides.Add(ride);
            await db.SaveChangesAsync();
        });

        return ride.Id;
    }
}

public sealed class VehicleStatusNotificationConsumerTests
{
    private sealed class FixedRegionResolver : IRegionResolver
    {
        public Task<string> ResolveRegionAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
            => Task.FromResult(latitude > 40 ? "Kuzey" : IRegionResolver.DefaultRegion);

        public Task<bool> IsKnownRegionAsync(string regionName, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class RecordingNotifier : IFleetNotifier
    {
        public List<(Guid VehicleId, string Region, string Status)> Sent { get; } = new();

        public Task NotifyVehicleStatusChangedAsync(Guid vehicleId, string regionName, string newStatus, CancellationToken cancellationToken = default)
        {
            Sent.Add((vehicleId, regionName, newStatus));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Durum_Degisikligi_Aracin_Bolgesine_Bildirilmeli()
    {
        var notifier = new RecordingNotifier();
        var vehicleId = Guid.NewGuid();
        var body = JsonSerializer.Serialize(new VehicleStatusChangedIntegrationEvent(vehicleId, "Available", "Reserved", 41.0, 29.0));

        var result = await VehicleStatusNotificationConsumer.ProcessAsync(
            new ReceivedMessage(IntegrationEventNames.VehicleStatusChanged, null, body), Services(notifier), NullLogger.Instance, CancellationToken.None);

        Assert.Equal(ConsumeResult.Ack, result);
        Assert.Equal([(vehicleId, "Kuzey", "Reserved")], notifier.Sent);
    }

    [Fact]
    public async Task Bozuk_Bildirim_Mesaji_Atlanmali()
    {
        var notifier = new RecordingNotifier();

        var result = await VehicleStatusNotificationConsumer.ProcessAsync(
            new ReceivedMessage(IntegrationEventNames.VehicleStatusChanged, null, "bozuk"), Services(notifier), NullLogger.Instance, CancellationToken.None);

        Assert.Equal(ConsumeResult.DeadLetter, result);
        Assert.Empty(notifier.Sent);
    }

    private static ServiceProvider Services(RecordingNotifier notifier) => new ServiceCollection()
        .AddSingleton<IRegionResolver, FixedRegionResolver>()
        .AddSingleton<IFleetNotifier>(notifier)
        .BuildServiceProvider();
}
