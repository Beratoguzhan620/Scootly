using Scootly.Application.Abstractions;
using Scootly.Application.Payments.Commands;
using Scootly.Application.Telemetry;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding;
using Xunit;

namespace Scootly.Application.UnitTests;

public sealed class ChargeRideCommandHandlerTests
{
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly InMemoryRideRepository _rides = new();
    private readonly StubPaymentGateway _gateway = new();
    private readonly ChargeRideCommandHandler _handler;

    public ChargeRideCommandHandlerTests()
    {
        _handler = new ChargeRideCommandHandler(_rides, _gateway, _unitOfWork, new FakeClock());
    }

    private Ride StoreCompletedRide()
    {
        var ride = new Ride(RideId.New(), Guid.NewGuid(), Guid.NewGuid(), new GeoPoint(41, 29), TestClock.Now.AddMinutes(-10));
        ride.Complete(new GeoPoint(41.01, 29.01), TestClock.Now, Tariff.Standard);
        return _rides.Store(ride);
    }

    [Fact]
    public async Task Onaylanan_Odeme_Surusu_Odenmis_Yapmali()
    {
        var ride = StoreCompletedRide();

        var result = await _handler.Handle(new ChargeRideCommand(ride.Id));

        Assert.Equal(ChargeOutcome.Approved, result.Value);
        Assert.Equal(PaymentStatus.Paid, ride.PaymentStatus);

        var request = Assert.Single(_gateway.Requests);
        Assert.Equal(ride.Fare, request.Amount);
        Assert.Equal($"ride-{ride.Id:N}-attempt-1", request.IdempotencyKey);
    }

    [Fact]
    public async Task Reddedilen_Odeme_Deneme_Olarak_Kaydedilmeli()
    {
        var ride = StoreCompletedRide();
        _gateway.Outcome = PaymentGatewayOutcome.Declined;

        var result = await _handler.Handle(new ChargeRideCommand(ride.Id));

        Assert.Equal(ChargeOutcome.Declined, result.Value);
        Assert.Equal(PaymentStatus.Pending, ride.PaymentStatus);
        Assert.Equal(1, ride.PaymentAttempts);
    }

    [Fact]
    public async Task Ulasilamayan_Saglayici_Deneme_Sayilmamali_Ve_Ayni_Anahtar_Tekrar_Kullanilmali()
    {
        var ride = StoreCompletedRide();
        _gateway.Outcome = PaymentGatewayOutcome.Unavailable;

        var first = await _handler.Handle(new ChargeRideCommand(ride.Id));
        var second = await _handler.Handle(new ChargeRideCommand(ride.Id));

        Assert.Equal(ChargeOutcome.GatewayUnavailable, first.Value);
        Assert.Equal(ChargeOutcome.GatewayUnavailable, second.Value);
        Assert.Equal(0, ride.PaymentAttempts);
        Assert.Equal(0, _unitOfWork.SaveCount);

        // Yeniden teslimde aynı idempotency anahtarı gider: sağlayıcı ikinci kez tahsil etmez.
        Assert.Equal(_gateway.Requests[0].IdempotencyKey, _gateway.Requests[1].IdempotencyKey);
    }

    [Fact]
    public async Task Odenmis_Surus_Icin_Saglayici_Cagrilmamali()
    {
        var ride = StoreCompletedRide();
        ride.RecordPaymentApproved(TestClock.Now);

        var result = await _handler.Handle(new ChargeRideCommand(ride.Id));

        Assert.Equal(ChargeOutcome.NothingToCharge, result.Value);
        Assert.Empty(_gateway.Requests);
    }

    [Fact]
    public async Task Aktif_Surus_Ucretlendirilmemeli()
    {
        var ride = _rides.Store(new Ride(RideId.New(), Guid.NewGuid(), Guid.NewGuid(), new GeoPoint(41, 29), TestClock.Now));

        var result = await _handler.Handle(new ChargeRideCommand(ride.Id));

        Assert.Equal(ChargeOutcome.NothingToCharge, result.Value);
        Assert.Empty(_gateway.Requests);
    }
}

public sealed class ApplyPaymentWebhookCommandHandlerTests
{
    [Fact]
    public async Task Basarili_Webhook_Bekleyen_Odemeyi_Tamamlamali()
    {
        var unitOfWork = new FakeUnitOfWork();
        var rides = new InMemoryRideRepository();
        var ride = new Ride(RideId.New(), Guid.NewGuid(), Guid.NewGuid(), new GeoPoint(41, 29), TestClock.Now.AddMinutes(-5));
        ride.Complete(new GeoPoint(41.01, 29.01), TestClock.Now, Tariff.Standard);
        rides.Store(ride);

        var handler = new ApplyPaymentWebhookCommandHandler(rides, unitOfWork, new FakeClock());

        var result = await handler.Handle(new ApplyPaymentWebhookCommand(ride.Id, Success: true, "ok"));

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Paid, ride.PaymentStatus);
    }

    [Fact]
    public async Task Basarisiz_Webhook_Deneme_Sayisini_Degistirmemeli()
    {
        var unitOfWork = new FakeUnitOfWork();
        var rides = new InMemoryRideRepository();
        var ride = new Ride(RideId.New(), Guid.NewGuid(), Guid.NewGuid(), new GeoPoint(41, 29), TestClock.Now.AddMinutes(-5));
        ride.Complete(new GeoPoint(41.01, 29.01), TestClock.Now, Tariff.Standard);
        rides.Store(ride);

        var handler = new ApplyPaymentWebhookCommandHandler(rides, unitOfWork, new FakeClock());

        var result = await handler.Handle(new ApplyPaymentWebhookCommand(ride.Id, Success: false, "ret"));

        Assert.True(result.IsSuccess);
        Assert.Equal(0, ride.PaymentAttempts);
        Assert.Equal(0, unitOfWork.SaveCount);
    }
}

public sealed class ProcessTelemetryBatchCommandHandlerTests
{
    [Fact]
    public async Task Bilinen_Araclarin_Okumalari_Saklanmali_Ve_Arac_Durumu_Guncellenmeli()
    {
        var unitOfWork = new FakeUnitOfWork();
        var vehicles = new InMemoryVehicleRepository(unitOfWork);
        var telemetry = new InMemoryTelemetryRepository();
        var vehicle = vehicles.Store(Build.AvailableVehicle(battery: 50));

        var handler = new ProcessTelemetryBatchCommandHandler(vehicles, telemetry, unitOfWork);

        var result = await handler.Handle(new ProcessTelemetryBatchCommand(
        [
            new TelemetryReadingData(vehicle.Id, 41.1, 29.1, 45, TestClock.Now.AddSeconds(5)),
            new TelemetryReadingData(vehicle.Id, 41.2, 29.2, 44, TestClock.Now.AddSeconds(10)),
            new TelemetryReadingData(Guid.NewGuid(), 41.3, 29.3, 90, TestClock.Now.AddSeconds(10))
        ]));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Stored);
        Assert.Equal(1, result.Value.Skipped);
        Assert.Equal(2, telemetry.Readings.Count);
        Assert.Equal(44, vehicle.Battery.Percentage);
        Assert.Equal(new GeoPoint(41.2, 29.2), vehicle.Location);
    }

    [Fact]
    public async Task Gecersiz_Deger_Iceren_Okuma_Atlanmali()
    {
        var unitOfWork = new FakeUnitOfWork();
        var vehicles = new InMemoryVehicleRepository(unitOfWork);
        var telemetry = new InMemoryTelemetryRepository();
        var vehicle = vehicles.Store(Build.AvailableVehicle());

        var handler = new ProcessTelemetryBatchCommandHandler(vehicles, telemetry, unitOfWork);

        var result = await handler.Handle(new ProcessTelemetryBatchCommand(
            [new TelemetryReadingData(vehicle.Id, 95, 29, 50, TestClock.Now)]));

        Assert.Equal(0, result.Value!.Stored);
        Assert.Equal(1, result.Value.Skipped);
    }
}

public sealed class TelemetryChannelTests
{
    [Fact]
    public void Kapasiteyi_Asan_Parti_Hic_Kuyruga_Alinmamali()
    {
        var channel = new TelemetryChannel();
        var reading = new TelemetryReadingData(Guid.NewGuid(), 41, 29, 50, TestClock.Now);

        Assert.True(channel.TryWriteBatch(Enumerable.Repeat(reading, TelemetryChannel.Capacity - 1).ToList()));
        Assert.False(channel.TryWriteBatch([reading, reading]));
        Assert.Equal(TelemetryChannel.Capacity - 1, channel.Reader.Count);
    }
}
