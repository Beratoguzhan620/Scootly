using Scootly.Domain.Common;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding;
using Scootly.Domain.Telemetry;
using Xunit;

namespace Scootly.Domain.UnitTests;

public class TelemetryReadingTests
{
    private static readonly DateTime RecordedAt = new(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);

    private static TelemetryReading NewReading(Guid? id = null)
        => new(id ?? Guid.NewGuid(), Guid.NewGuid(), new GeoPoint(41.0, 29.0), new BatteryLevel(55), RecordedAt);

    [Fact]
    public void Gecerli_Okuma_Alanlari_Tasimali()
    {
        var id = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var location = new GeoPoint(41.0, 29.0);

        var reading = new TelemetryReading(id, vehicleId, location, new BatteryLevel(55), RecordedAt);

        Assert.Equal(id, reading.Id);
        Assert.Equal(vehicleId, reading.VehicleId);
        Assert.Equal(location, reading.Location);
        Assert.Equal(55, reading.BatteryPercentage);
        Assert.Equal(RecordedAt, reading.RecordedAt);
    }

    [Fact]
    public void Bos_Arac_Kimligi_Reddedilmeli()
    {
        Assert.Throws<DomainException>(
            () => new TelemetryReading(Guid.NewGuid(), Guid.Empty, new GeoPoint(41.0, 29.0), new BatteryLevel(55), RecordedAt));
    }

    [Fact]
    public void Bos_Okuma_Kimligi_Reddedilmeli()
    {
        Assert.Throws<DomainException>(
            () => new TelemetryReading(Guid.Empty, Guid.NewGuid(), new GeoPoint(41.0, 29.0), new BatteryLevel(55), RecordedAt));
    }

    [Fact]
    public void Ayni_Kimlikli_Okumalar_Esit_Sayilmali()
    {
        var id = Guid.NewGuid();
        var first = NewReading(id);
        var second = NewReading(id);
        var other = NewReading();

        Assert.True(first == second);
        Assert.True(first.Equals(second));
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.True(first != other);
        Assert.False(first.Equals("metin"));
    }

    [Fact]
    public void Bos_Referanslarla_Esitlik_Dogru_Calismali()
    {
        var reading = NewReading();
        Entity? none = null;
        Entity? alsoNone = null;

        Assert.False(reading == none);
        Assert.False(none == reading);
        Assert.True(reading != none);
        Assert.True(none == alsoNone);
        Assert.True(reading.Equals(reading));
    }
}

public class ValueObjectEqualityTests
{
    [Fact]
    public void RideId_Ayni_Deger_Esit_Farkli_Deger_Esit_Degil()
    {
        var guid = Guid.NewGuid();
        var first = new RideId(guid);
        var second = new RideId(guid);
        var other = RideId.New();

        Assert.True(first == second);
        Assert.True(first.Equals(second));
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.True(first != other);
    }

    [Fact]
    public void Bos_Referans_Ve_Farkli_Tur_Esit_Sayilmamali()
    {
        var rideId = RideId.New();
        RideId? none = null;
        RideId? alsoNone = null;

        Assert.False(rideId == none);
        Assert.False(none == rideId);
        Assert.True(rideId != none);
        Assert.True(none == alsoNone);
        Assert.False(rideId.Equals(new BatteryLevel(10)));
    }

    [Fact]
    public void Tarife_Ayni_Degerlerle_Esit_Sayilmali()
    {
        var first = new Tariff(1m, 2m);
        var second = new Tariff(1m, 2m);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, new Tariff(1m, 3m));
    }

    [Fact]
    public void Pil_Seviyesi_Ayni_Degerle_Esit_Sayilmali()
    {
        Assert.Equal(new BatteryLevel(40), new BatteryLevel(40));
        Assert.NotEqual(new BatteryLevel(40), new BatteryLevel(41));
    }

    [Fact]
    public void Konum_Kopyasi_Esit_Sayilmali()
    {
        var point = new GeoPoint(41.0, 29.0);

        Assert.Equal(point, point.Copy());
        Assert.NotSame(point, point.Copy());
    }
}

public class ResultErrorTypeTests
{
    [Fact]
    public void Genel_Sonuc_Hata_Turlerini_Tasimali()
    {
        Assert.Equal(ErrorType.NotFound, Result<int>.NotFound("yok").ErrorType);
        Assert.Equal(ErrorType.Validation, Result<int>.Validation("gecersiz").ErrorType);
        Assert.Equal(ErrorType.Forbidden, Result<int>.Forbidden("yasak").ErrorType);
        Assert.Equal(ErrorType.Conflict, Result<int>.Failure("cakisma").ErrorType);
        Assert.Equal(ErrorType.Validation, Result<int>.Failure("x", ErrorType.Validation).ErrorType);
    }

    [Fact]
    public void Basarisiz_Genel_Sonuc_Deger_Tasimamali()
    {
        var result = Result<string>.NotFound("yok");

        Assert.False(result.IsSuccess);
        Assert.Equal("yok", result.Error);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Basarili_Genel_Sonuc_Degeri_Tasimali()
    {
        var result = Result<string>.Success("a");

        Assert.True(result.IsSuccess);
        Assert.Equal("a", result.Value);
        Assert.Equal(ErrorType.None, result.ErrorType);
        Assert.Equal(string.Empty, result.Error);
    }

    [Fact]
    public void Genel_Olmayan_Sonuc_Hata_Turlerini_Tasimali()
    {
        Assert.Equal(ErrorType.NotFound, Result.NotFound("yok").ErrorType);
        Assert.Equal(ErrorType.Validation, Result.Validation("gecersiz").ErrorType);
        Assert.Equal(ErrorType.Forbidden, Result.Forbidden("yasak").ErrorType);
        Assert.Equal(ErrorType.Conflict, Result.Failure("cakisma").ErrorType);
        Assert.True(Result.Success().IsSuccess);
    }
}
