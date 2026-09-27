using Scootly.Application.Abstractions;
using Scootly.Application.Pricing.Commands;
using Scootly.Domain.Geo;
using Scootly.Domain.Pricing;
using Scootly.Domain.Riding;
using Xunit;

namespace Scootly.Application.UnitTests;

public sealed class ApplyRideFareCommandHandlerTests
{
    [Fact]
    public async Task Tamamlanmis_Surushe_Standart_Tarifeyle_Ucret_Yazilir()
    {
        var baslangic = new DateTime(2026, 9, 27, 10, 0, 0, DateTimeKind.Utc);
        var ride = new Ride(RideId.New(), Guid.NewGuid(), Guid.NewGuid(), new GeoPoint(41.0, 29.0), baslangic);
        ride.Complete(new GeoPoint(41.0, 29.01), baslangic.AddMinutes(10));
        var uow = new SayanUnitOfWork();

        var result = await new ApplyRideFareCommandHandler(new TekSurusDeposu(ride), uow)
            .Handle(new ApplyRideFareCommand(ride.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(Tariff.Standard.Calculate(TimeSpan.FromMinutes(10)), ride.Fare);
        Assert.Equal(1, uow.Kayit);
    }

    [Fact]
    public async Task Ayni_Mesaj_Ikinci_Kez_Gelirse_Ucret_Degismez_Ve_Kayit_Yapilmaz()
    {
        var ride = new Ride(RideId.New(), Guid.NewGuid(), Guid.NewGuid(), new GeoPoint(41.0, 29.0), DateTime.UtcNow.AddMinutes(-3));
        ride.Complete(new GeoPoint(41.0, 29.01), DateTime.UtcNow);
        var uow = new SayanUnitOfWork();
        var handler = new ApplyRideFareCommandHandler(new TekSurusDeposu(ride), uow);

        await handler.Handle(new ApplyRideFareCommand(ride.Id));
        var ilkUcret = ride.Fare;
        var ikinci = await handler.Handle(new ApplyRideFareCommand(ride.Id));

        Assert.True(ikinci.IsSuccess);
        Assert.Equal(ilkUcret, ride.Fare);
        Assert.Equal(1, uow.Kayit);
    }

    [Fact]
    public async Task Olmayan_Surus_Reddedilir()
    {
        var result = await new ApplyRideFareCommandHandler(new TekSurusDeposu(null), new SayanUnitOfWork())
            .Handle(new ApplyRideFareCommand(Guid.NewGuid()));

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Aktif_Surus_Reddedilir()
    {
        var ride = new Ride(RideId.New(), Guid.NewGuid(), Guid.NewGuid(), new GeoPoint(41.0, 29.0), DateTime.UtcNow);

        var result = await new ApplyRideFareCommandHandler(new TekSurusDeposu(ride), new SayanUnitOfWork())
            .Handle(new ApplyRideFareCommand(ride.Id));

        Assert.False(result.IsSuccess);
        Assert.Null(ride.Fare);
    }

    private sealed class TekSurusDeposu : IRideRepository
    {
        private readonly Ride? _ride;

        public TekSurusDeposu(Ride? ride) => _ride = ride;

        public Task<Ride?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(_ride is not null && _ride.Id == id ? _ride : null);

        public Task AddAsync(Ride ride, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class SayanUnitOfWork : IUnitOfWork
    {
        public int Kayit { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Kayit++;
            return Task.FromResult(1);
        }
    }
}
