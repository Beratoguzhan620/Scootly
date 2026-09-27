using Scootly.Application.Abstractions;
using Scootly.Application.FieldOps.Commands;
using Scootly.Domain.FieldOps;
using Xunit;

namespace Scootly.Application.UnitTests;

public sealed class OpenBatteryFieldTaskCommandHandlerTests
{
    [Fact]
    public async Task Dusuk_Batarya_Icin_Gorev_Acilir()
    {
        var depo = new BellekDeposu();
        var aracId = Guid.NewGuid();

        var result = await Handler(depo).Handle(new OpenBatteryFieldTaskCommand(aracId, 12));

        Assert.True(result.IsSuccess);
        var gorev = Assert.Single(depo.Gorevler);
        Assert.Equal(aracId, gorev.VehicleId);
        Assert.Equal(FieldTaskStatus.Open, gorev.Status);
    }

    [Fact]
    public async Task Tarayici_Her_Turda_Ayni_Araci_Bildirse_De_Tek_Gorev_Acilir()
    {
        // Batarya tarayicisi bes dakikada bir, esigin altindaki her arac icin
        // olay yayinliyor. Saha ekibine ayni arac icin her bes dakikada bir
        // yeni gorev dusmemeli.
        var depo = new BellekDeposu();
        var aracId = Guid.NewGuid();
        var handler = Handler(depo);

        for (var tur = 0; tur < 5; tur++)
        {
            var result = await handler.Handle(new OpenBatteryFieldTaskCommand(aracId, 12));
            Assert.True(result.IsSuccess);
        }

        Assert.Single(depo.Gorevler);
    }

    [Fact]
    public async Task Gecersiz_Yuzde_Reddedilir_Ve_Gorev_Acilmaz()
    {
        var depo = new BellekDeposu();

        var result = await Handler(depo).Handle(new OpenBatteryFieldTaskCommand(Guid.NewGuid(), 150));

        Assert.False(result.IsSuccess);
        Assert.Empty(depo.Gorevler);
    }

    private static OpenBatteryFieldTaskCommandHandler Handler(BellekDeposu depo)
        => new(depo, depo, new SabitSaat());

    private sealed class BellekDeposu : IFieldTaskRepository, IUnitOfWork
    {
        private readonly List<FieldTask> _bekleyen = new();

        public List<FieldTask> Gorevler { get; } = new();

        public Task<bool> HasOpenTaskAsync(Guid vehicleId, FieldTaskType type, CancellationToken cancellationToken = default)
            => Task.FromResult(Gorevler.Any(g => g.VehicleId == vehicleId && g.Type == type && g.Status == FieldTaskStatus.Open));

        public Task AddAsync(FieldTask task, CancellationToken cancellationToken = default)
        {
            _bekleyen.Add(task);
            return Task.CompletedTask;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Gorevler.AddRange(_bekleyen);
            var n = _bekleyen.Count;
            _bekleyen.Clear();
            return Task.FromResult(n);
        }
    }

    private sealed class SabitSaat : IClock
    {
        public DateTime UtcNow { get; } = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
    }
}
