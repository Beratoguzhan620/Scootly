using Scootly.Domain.Common;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding;
using Xunit;

namespace Scootly.Domain.UnitTests;

public class TariffTests
{
    [Theory]
    [InlineData(0, 2.50)]      // 0 sn → en az 1 dakika
    [InlineData(30, 2.50)]     // 30 sn → 1 dakika
    [InlineData(60, 2.50)]     // tam 1 dakika
    [InlineData(61, 5.00)]     // başlamış 2. dakika
    [InlineData(119, 5.00)]    // 1 dk 59 sn → 2 dakika
    [InlineData(600, 25.00)]   // 10 dakika
    public void Baslamis_Her_Dakika_Ucretlendirilmeli(int seconds, decimal expectedFare)
    {
        Assert.Equal(expectedFare, Tariff.Standard.Calculate(TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void Acilis_Ucreti_Eklenmeli()
    {
        var tariff = new Tariff(unlockFee: 5m, perMinuteRate: 2m);

        Assert.Equal(11m, tariff.Calculate(TimeSpan.FromMinutes(3)));
    }

    [Fact]
    public void Negatif_Sure_Ve_Gecersiz_Tarife_Reddedilmeli()
    {
        Assert.Throws<DomainException>(() => Tariff.Standard.Calculate(TimeSpan.FromSeconds(-1)));
        Assert.Throws<DomainException>(() => new Tariff(-1m, 2m));
        Assert.Throws<DomainException>(() => new Tariff(0m, 0m));
    }
}

public class ReservationPolicyTests
{
    private static readonly DateTime ReservedAt = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Rezervasyon_Suresi_Dolmadan_Gecerli_Sayilmali()
    {
        Assert.False(ReservationPolicy.IsExpired(ReservedAt, ReservedAt.AddMinutes(9)));
    }

    [Fact]
    public void Rezervasyon_Suresi_Dolunca_Gecersiz_Sayilmali()
    {
        Assert.True(ReservationPolicy.IsExpired(ReservedAt, ReservedAt + ReservationPolicy.Duration));
        Assert.Equal(ReservedAt, ReservationPolicy.ExpiryCutoff(ReservedAt + ReservationPolicy.Duration));
    }
}

public class VehicleModelTests
{
    [Theory]
    [InlineData("", 25)]
    [InlineData("   ", 25)]
    [InlineData("Xiaomi", 0)]
    [InlineData("Xiaomi", -5)]
    [InlineData("Xiaomi", VehicleModel.MaxRangeKm + 1)]
    public void Gecersiz_Model_Reddedilmeli(string brand, int rangeKm)
    {
        Assert.Throws<DomainException>(() => new VehicleModel(brand, rangeKm));
    }

    [Fact]
    public void Cok_Uzun_Marka_Reddedilmeli()
    {
        Assert.Throws<DomainException>(() => new VehicleModel(new string('a', VehicleModel.BrandMaxLength + 1), 25));
    }

    [Fact]
    public void Ayni_Degerli_Modeller_Esit_Olmali()
    {
        Assert.Equal(new VehicleModel("Segway", 30), new VehicleModel(" Segway ", 30));
    }
}

public class ServiceAreaTests
{
    [Fact]
    public void Ucten_Az_Noktali_Bolge_Olusturulamaz()
    {
        Assert.Throws<DomainException>(() => new ServiceArea("Kadıköy", [new GeoPoint(0, 0), new GeoPoint(0, 1)]));
    }

    [Fact]
    public void Tekrarlanan_Nokta_Iceren_Bolge_Olusturulamaz()
    {
        Assert.Throws<DomainException>(() => new ServiceArea("Kadıköy",
            [new GeoPoint(0, 0), new GeoPoint(0, 1), new GeoPoint(1, 1), new GeoPoint(0, 0)]));
    }

    [Fact]
    public void Bos_Adli_Bolge_Olusturulamaz()
    {
        Assert.Throws<DomainException>(() => new ServiceArea(" ", [new GeoPoint(0, 0), new GeoPoint(0, 1), new GeoPoint(1, 1)]));
    }
}
