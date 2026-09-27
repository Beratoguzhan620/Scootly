using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scootly.Application.Abstractions.Exceptions;
using Scootly.Application.Common;
using Scootly.Application.IntegrationEvents;
using Scootly.Infrastructure.Persistence;
using Scootly.Testing;
using Xunit;

namespace Scootly.Infrastructure.Tests;

[Collection(InfrastructureCollection.Name)]
public sealed class PersistenceTests
{
    private readonly InfrastructureFixture _fixture;

    public PersistenceTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<Guid> SeedVehicleAsync()
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

        var vehicle = TestData.NewVehicle();
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();

        return vehicle.Id;
    }

    [Fact]
    public async Task Domain_Olaylari_Ayni_Transactionda_Outboxa_Yazilmali_Ve_Temizlenmeli()
    {
        var vehicleId = await SeedVehicleAsync();

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

        var vehicle = await db.Vehicles.SingleAsync(v => v.Id == vehicleId);
        vehicle.Reserve(Guid.NewGuid(), DateTime.UtcNow);
        await db.SaveChangesAsync();

        Assert.Empty(vehicle.DomainEvents);

        var outbox = await db.OutboxMessages.AsNoTracking()
            .Where(m => m.EventType == IntegrationEventNames.VehicleStatusChanged && m.Payload.Contains(vehicleId.ToString()))
            .ToListAsync();

        var message = Assert.Single(outbox);
        Assert.Contains("\"NewStatus\":\"Reserved\"", message.Payload);
    }

    [Fact]
    public async Task Ayni_Surucunun_Ikinci_Rezervasyonu_Veritabaninda_Engellenmeli()
    {
        var firstId = await SeedVehicleAsync();
        var secondId = await SeedVehicleAsync();
        var driverId = Guid.NewGuid();

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

        (await db.Vehicles.SingleAsync(v => v.Id == firstId)).Reserve(driverId, DateTime.UtcNow);
        (await db.Vehicles.SingleAsync(v => v.Id == secondId)).Reserve(driverId, DateTime.UtcNow);

        var ex = await Assert.ThrowsAsync<UniqueConstraintViolationException>(() => db.SaveChangesAsync());

        Assert.Equal(ConstraintNames.OneActiveReservationPerDriver, ex.ConstraintName);
    }

    [Fact]
    public async Task Eszamanli_Guncelleme_ConcurrencyConflictException_Firlatmali()
    {
        var vehicleId = await SeedVehicleAsync();

        using var firstScope = _fixture.Services.CreateScope();
        using var secondScope = _fixture.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<ScootlyDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

        var firstCopy = await first.Vehicles.SingleAsync(v => v.Id == vehicleId);
        var secondCopy = await second.Vehicles.SingleAsync(v => v.Id == vehicleId);

        firstCopy.Reserve(Guid.NewGuid(), DateTime.UtcNow);
        await first.SaveChangesAsync();

        secondCopy.Reserve(Guid.NewGuid(), DateTime.UtcNow);

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Surucu_Basina_Tek_Aktif_Surus_Veritabaninda_Engellenmeli()
    {
        var firstVehicleId = await SeedVehicleAsync();
        var secondVehicleId = await SeedVehicleAsync();
        var driverId = Guid.NewGuid();

        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

        db.Rides.Add(TestData.NewActiveRide(driverId, firstVehicleId));
        db.Rides.Add(TestData.NewActiveRide(driverId, secondVehicleId));

        var ex = await Assert.ThrowsAsync<UniqueConstraintViolationException>(() => db.SaveChangesAsync());

        Assert.Equal(ConstraintNames.OneActiveRidePerDriver, ex.ConstraintName);
    }
}
