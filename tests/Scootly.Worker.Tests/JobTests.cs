using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Scootly.Application.IntegrationEvents;
using Scootly.Domain.FieldOps;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding;
using Scootly.Domain.Telemetry;
using Scootly.Infrastructure.Messaging.Consumers;
using Scootly.Infrastructure.Messaging.Idempotency;
using Scootly.Infrastructure.Messaging.Outbox;
using Scootly.Testing;
using Scootly.Worker.Jobs;
using Xunit;

namespace Scootly.Worker.Tests;

[Collection(WorkerCollection.Name)]
public sealed class JobTests
{
    private static readonly IOptions<WorkerOptions> Options = Microsoft.Extensions.Options.Options.Create(new WorkerOptions());

    private readonly WorkerFixture _fixture;

    public JobTests(WorkerFixture fixture)
    {
        _fixture = fixture;
    }

    private DateTime Now => _fixture.Clock.UtcNow;

    private IServiceScopeFactory Scopes => _fixture.Services.GetRequiredService<IServiceScopeFactory>();

    [Fact]
    public async Task Suresi_Dolan_Rezervasyon_Kaldirilmali_Tazesi_Kalmali()
    {
        var expired = TestData.NewVehicle(registeredAt: Now.AddHours(-1));
        var fresh = TestData.NewVehicle(registeredAt: Now.AddHours(-1));
        expired.Reserve(Guid.NewGuid(), Now - ReservationPolicy.Duration - TimeSpan.FromMinutes(1));
        fresh.Reserve(Guid.NewGuid(), Now.AddMinutes(-1));
        await _fixture.SeedAsync(expired, fresh);

        await _fixture.RunAsync(new ReservationTimeoutService(Scopes, NullLogger<ReservationTimeoutService>.Instance));

        Assert.Equal(VehicleStatus.Available, await StatusOfAsync(expired.Id));
        Assert.Equal(VehicleStatus.Reserved, await StatusOfAsync(fresh.Id));
    }

    [Fact]
    public async Task Esigi_Asan_Surus_Terk_Edilmis_Sayilmali_Arac_Bakima_Alinmali_Ve_Denetim_Gorevi_Acilmali()
    {
        var (staleVehicle, staleRide) = ActiveRide(startedAt: Now - Options.Value.AbandonedRideThreshold - TimeSpan.FromMinutes(5));
        var (freshVehicle, freshRide) = ActiveRide(startedAt: Now.AddMinutes(-10));
        await _fixture.SeedAsync(staleVehicle, staleRide, freshVehicle, freshRide);

        await _fixture.RunAsync(new AbandonedRideDetector(Scopes, Options, NullLogger<AbandonedRideDetector>.Instance));

        var stale = await _fixture.QueryAsync(db => db.Rides.AsNoTracking().SingleAsync(r => r.Id == staleRide.Id));
        Assert.Equal(RideStatus.Abandoned, stale.Status);
        Assert.Equal(PaymentStatus.Pending, stale.PaymentStatus);
        Assert.Equal(VehicleStatus.Maintenance, await StatusOfAsync(staleVehicle.Id));
        Assert.True(await _fixture.QueryAsync(db => db.FieldTasks.AnyAsync(
            t => t.VehicleId == staleVehicle.Id && t.Type == FieldTaskType.Inspection && t.Status == FieldTaskStatus.Open)));

        var fresh = await _fixture.QueryAsync(db => db.Rides.AsNoTracking().SingleAsync(r => r.Id == freshRide.Id));
        Assert.Equal(RideStatus.Active, fresh.Status);
        Assert.Equal(VehicleStatus.InRide, await StatusOfAsync(freshVehicle.Id));
    }

    [Fact]
    public async Task Hic_Denenmemis_Bekleyen_Odeme_Tolerans_Sonrasi_Tahsil_Edilmeli()
    {
        var due = CompletedRide(endedAt: Now - Options.Value.UnchargedRideGracePeriod - TimeSpan.FromMinutes(1));
        var notYetDue = CompletedRide(endedAt: Now.AddSeconds(-30));
        await _fixture.SeedAsync(due, notYetDue);

        await _fixture.RunAsync(new PendingPaymentRetryService(Scopes, Options, NullLogger<PendingPaymentRetryService>.Instance));

        Assert.Equal(PaymentStatus.Paid, await PaymentStatusOfAsync(due.Id));
        Assert.Equal(PaymentStatus.Pending, await PaymentStatusOfAsync(notYetDue.Id));
    }

    [Fact]
    public async Task Saklama_Politikasi_Eski_Verileri_Silmeli_Ve_Anonimlestirmeli()
    {
        var vehicleId = Guid.NewGuid();
        var oldReading = new TelemetryReading(Guid.NewGuid(), vehicleId, new GeoPoint(41, 29), new BatteryLevel(50), Now.AddDays(-(Options.Value.TelemetryRetentionDays + 1)));
        var newReading = new TelemetryReading(Guid.NewGuid(), vehicleId, new GeoPoint(41, 29), new BatteryLevel(50), Now.AddDays(-1));
        var oldRide = CompletedRide(endedAt: Now.AddDays(-(Options.Value.RideLocationRetentionDays + 1)));
        var newRide = CompletedRide(endedAt: Now.AddDays(-1));

        var oldOutbox = new OutboxMessage(Guid.NewGuid(), "Test", "{}", Now.AddDays(-30));
        oldOutbox.MarkAsProcessed(Now.AddDays(-(Options.Value.OutboxRetentionDays + 1)));
        var pendingOutbox = new OutboxMessage(Guid.NewGuid(), "Test", "{}", Now.AddDays(-30));
        var oldProcessed = new ProcessedMessage(Guid.NewGuid(), "test", Now.AddDays(-(Options.Value.ProcessedMessageRetentionDays + 1)));

        await _fixture.SeedAsync(oldReading, newReading, oldRide, newRide, oldOutbox, pendingOutbox, oldProcessed);

        await _fixture.RunAsync(new DataRetentionService(Scopes, Options, NullLogger<DataRetentionService>.Instance));

        var readings = await _fixture.QueryAsync(db => db.TelemetryReadings.Where(t => t.VehicleId == vehicleId).Select(t => t.Id).ToListAsync());
        Assert.Equal([newReading.Id], readings);

        var anonymized = await _fixture.QueryAsync(db => db.Rides.AsNoTracking().SingleAsync(r => r.Id == oldRide.Id));
        var kept = await _fixture.QueryAsync(db => db.Rides.AsNoTracking().SingleAsync(r => r.Id == newRide.Id));
        Assert.Null(anonymized.StartLocation);
        Assert.Null(anonymized.EndLocation);
        Assert.NotNull(kept.EndLocation);

        Assert.False(await _fixture.QueryAsync(db => db.OutboxMessages.AnyAsync(m => m.Id == oldOutbox.Id)));
        Assert.True(await _fixture.QueryAsync(db => db.OutboxMessages.AnyAsync(m => m.Id == pendingOutbox.Id)), "Yayınlanmamış mesaj silinmemeli.");
        Assert.False(await _fixture.QueryAsync(db => db.ProcessedMessages.AnyAsync(m => m.MessageId == oldProcessed.MessageId)));
    }

    [Fact]
    public async Task Gorevi_Olmayan_Dusuk_Bataryali_Araca_Bir_Kez_Gorev_Acilmali()
    {
        var vehicle = TestData.NewVehicle(batteryPercentage: 12);
        await _fixture.SeedAsync(vehicle);
        var scanner = new BatteryThresholdScanner(Scopes, Options, NullLogger<BatteryThresholdScanner>.Instance);

        await _fixture.RunAsync(scanner);
        await _fixture.RunAsync(scanner);

        Assert.Equal(1, await BatteryTaskCountAsync(vehicle.Id));
    }

    [Fact]
    public async Task Yakin_Zamanda_Tamamlanan_Batarya_Gorevi_Varsa_Yeni_Gorev_Acilmamali()
    {
        var recentlyServed = TestData.NewVehicle(batteryPercentage: 12);
        var servedLongAgo = TestData.NewVehicle(batteryPercentage: 12);
        var cooldown = Options.Value.LowBatteryTaskCooldown;

        await _fixture.SeedAsync(
            recentlyServed, servedLongAgo,
            CompletedBatteryTask(recentlyServed.Id, completedAt: Now - cooldown + TimeSpan.FromHours(1)),
            CompletedBatteryTask(servedLongAgo.Id, completedAt: Now - cooldown - TimeSpan.FromHours(1)));

        await _fixture.RunAsync(new BatteryThresholdScanner(Scopes, Options, NullLogger<BatteryThresholdScanner>.Instance));

        Assert.Equal(1, await BatteryTaskCountAsync(recentlyServed.Id));
        Assert.Equal(2, await BatteryTaskCountAsync(servedLongAgo.Id));
    }

    [Fact]
    public async Task Batarya_Dusuk_Mesaji_Gorev_Acmali_Bozuk_Mesaj_DLQya_Gitmeli()
    {
        var vehicle = TestData.NewVehicle(batteryPercentage: 15);
        await _fixture.SeedAsync(vehicle);

        using var scope = _fixture.Services.CreateScope();
        var valid = new ReceivedMessage(IntegrationEventNames.VehicleBatteryLow, Guid.NewGuid().ToString(),
            JsonSerializer.Serialize(new VehicleBatteryLowIntegrationEvent(vehicle.Id, 15)));

        var first = await BatteryLowConsumer.ProcessAsync(valid, scope.ServiceProvider, NullLogger.Instance, CancellationToken.None);
        var redelivered = await BatteryLowConsumer.ProcessAsync(valid, scope.ServiceProvider, NullLogger.Instance, CancellationToken.None);
        var malformed = await BatteryLowConsumer.ProcessAsync(
            new ReceivedMessage(IntegrationEventNames.VehicleBatteryLow, null, "{ bozuk"), scope.ServiceProvider, NullLogger.Instance, CancellationToken.None);

        Assert.Equal(ConsumeResult.Ack, first);
        Assert.Equal(ConsumeResult.Ack, redelivered);
        Assert.Equal(ConsumeResult.DeadLetter, malformed);
        Assert.Equal(1, await BatteryTaskCountAsync(vehicle.Id));
    }

    private (Vehicle Vehicle, Ride Ride) ActiveRide(DateTime startedAt)
    {
        var driverId = Guid.NewGuid();
        var vehicle = TestData.NewVehicle(registeredAt: startedAt.AddMinutes(-5));
        vehicle.Reserve(driverId, startedAt.AddMinutes(-1));
        vehicle.StartRide(driverId, startedAt);

        return (vehicle, TestData.NewActiveRide(driverId, vehicle.Id, startedAt));
    }

    private static Ride CompletedRide(DateTime endedAt)
    {
        var ride = TestData.NewActiveRide(Guid.NewGuid(), Guid.NewGuid(), endedAt.AddMinutes(-10));
        ride.Complete(new GeoPoint(41.01, 29.01), endedAt, Tariff.Standard);
        return ride;
    }

    private static FieldTask CompletedBatteryTask(Guid vehicleId, DateTime completedAt)
    {
        var operatorId = Guid.NewGuid();
        var task = new FieldTask(Guid.NewGuid(), vehicleId, FieldTaskType.BatteryReplacement, completedAt.AddHours(-1));
        task.Assign(operatorId, completedAt.AddMinutes(-30));
        task.Complete(operatorId, completedAt, "Batarya değişti.");
        return task;
    }

    private Task<VehicleStatus> StatusOfAsync(Guid vehicleId)
        => _fixture.QueryAsync(db => db.Vehicles.AsNoTracking().Where(v => v.Id == vehicleId).Select(v => v.Status).SingleAsync());

    private Task<PaymentStatus> PaymentStatusOfAsync(Guid rideId)
        => _fixture.QueryAsync(db => db.Rides.AsNoTracking().Where(r => r.Id == rideId).Select(r => r.PaymentStatus).SingleAsync());

    private Task<int> BatteryTaskCountAsync(Guid vehicleId)
        => _fixture.QueryAsync(db => db.FieldTasks.CountAsync(t => t.VehicleId == vehicleId && t.Type == FieldTaskType.BatteryReplacement));
}
