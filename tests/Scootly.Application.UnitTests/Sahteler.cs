using Scootly.Application.Abstractions;
using Scootly.Application.IntegrationEvents;
using Scootly.Domain.Billing;
using Scootly.Domain.Fleet;
using Scootly.Domain.Riding;

namespace Scootly.Application.UnitTests;

/// <summary>Hafta 14 testlerinin ortak sahteleri.</summary>
internal sealed class SahteSurusDeposu : IRideRepository
{
    private readonly Ride? _ride;
    public SahteSurusDeposu(Ride? ride) => _ride = ride;
    public Task<Ride?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_ride is not null && _ride.Id == id ? _ride : null);
    public Task AddAsync(Ride ride, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

internal sealed class SahteAracDeposu : IVehicleRepository
{
    private readonly Vehicle? _vehicle;
    public SahteAracDeposu(Vehicle? vehicle) => _vehicle = vehicle;
    public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_vehicle is not null && _vehicle.Id == id ? _vehicle : null);
    public Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>
/// Outbox + iş birimi birlikte: yazılan olaylar ancak <see cref="SaveChangesAsync"/>
/// çağrılınca "kaydedilmiş" sayılıyor — gerçekteki transaction davranışının aynısı.
/// </summary>
internal sealed class SahteIsBirimi : IUnitOfWork, IOutboxWriter, IOutstandingDebtRepository, IProcessedMessageStore
{
    private readonly List<IIntegrationEvent> _bekleyenOlaylar = new();
    private readonly List<OutstandingDebt> _bekleyenBorclar = new();
    private readonly List<(Guid, string)> _bekleyenIslenenler = new();

    public List<IIntegrationEvent> KaydedilenOlaylar { get; } = new();
    public List<OutstandingDebt> KaydedilenBorclar { get; } = new();
    public HashSet<(Guid, string)> KaydedilenIslenenler { get; } = new();
    public int KayitSayisi { get; private set; }

    public Task WriteAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent, IHasEventName
    {
        _bekleyenOlaylar.Add(integrationEvent);
        return Task.CompletedTask;
    }

    public Task AddAsync(OutstandingDebt debt, CancellationToken cancellationToken = default)
    {
        _bekleyenBorclar.Add(debt);
        return Task.CompletedTask;
    }

    public Task<bool> HasBeenProcessedAsync(Guid messageId, string consumer, CancellationToken cancellationToken = default)
        => Task.FromResult(KaydedilenIslenenler.Contains((messageId, consumer)));

    public Task MarkAsProcessedAsync(Guid messageId, string consumer, CancellationToken cancellationToken = default)
    {
        _bekleyenIslenenler.Add((messageId, consumer));
        return Task.CompletedTask;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        KayitSayisi++;
        KaydedilenOlaylar.AddRange(_bekleyenOlaylar);
        KaydedilenBorclar.AddRange(_bekleyenBorclar);
        foreach (var x in _bekleyenIslenenler) KaydedilenIslenenler.Add(x);
        var n = _bekleyenOlaylar.Count + _bekleyenBorclar.Count + _bekleyenIslenenler.Count;
        _bekleyenOlaylar.Clear(); _bekleyenBorclar.Clear(); _bekleyenIslenenler.Clear();
        return Task.FromResult(n);
    }
}

internal sealed class SabitSaat : IClock
{
    public DateTime UtcNow { get; init; } = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
}

internal sealed class SahteOdeme : IPaymentGateway
{
    private readonly bool _onayla;
    public SahteOdeme(bool onayla) => _onayla = onayla;
    public List<PaymentAuthorizationRequest> Istekler { get; } = new();
    public Task<PaymentAuthorizationResult> AuthorizeAsync(PaymentAuthorizationRequest request, CancellationToken cancellationToken = default)
    {
        Istekler.Add(request);
        return Task.FromResult(_onayla
            ? new PaymentAuthorizationResult(true, null)
            : new PaymentAuthorizationResult(false, "Kart reddedildi (test)."));
    }
}

internal static class Kurgu
{
    public static readonly DateTime Baslangic = new(2026, 9, 27, 10, 0, 0, DateTimeKind.Utc);

    public static Vehicle SurusteArac()
    {
        var v = new Vehicle(VehicleId.New(), new VehicleModel("Xiaomi", 25), new Scootly.Domain.Geo.GeoPoint(41.0, 29.0), new BatteryLevel(80));
        v.Reserve();
        v.StartRide();
        return v;
    }

    public static Ride AktifSurus(Guid aracId)
        => new(RideId.New(), Guid.NewGuid(), aracId, new Scootly.Domain.Geo.GeoPoint(41.0, 29.0), Baslangic);

    public static Ride UcretliSurus(Guid aracId, decimal ucret = 35m)
    {
        var r = AktifSurus(aracId);
        r.Complete(new Scootly.Domain.Geo.GeoPoint(41.0, 29.01), Baslangic.AddMinutes(10));
        r.ApplyFare(ucret);
        r.ClearDomainEvents();
        return r;
    }
}
