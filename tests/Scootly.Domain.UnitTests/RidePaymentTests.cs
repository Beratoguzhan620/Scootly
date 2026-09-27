using Scootly.Domain.Billing;
using Scootly.Domain.Common;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding;
using Xunit;

namespace Scootly.Domain.UnitTests;

public class RidePaymentTests
{
    private static Ride Ucretli()
    {
        var r = new Ride(RideId.New(), Guid.NewGuid(), Guid.NewGuid(), new GeoPoint(41.0, 29.0), DateTime.UtcNow.AddMinutes(-5));
        r.Complete(new GeoPoint(41.0, 29.01), DateTime.UtcNow);
        r.ApplyFare(22.5m);
        return r;
    }

    [Fact]
    public void Yeni_Surusun_Odemesi_Sonuclanmamistir()
    {
        Assert.Null(Ucretli().PaymentStatus);
    }

    [Fact]
    public void Odeme_Bir_Kez_Sonuclanir()
    {
        var r = Ucretli();
        r.MarkPaid();

        Assert.Equal(RidePaymentStatus.Paid, r.PaymentStatus);
        Assert.Throws<DomainException>(r.MarkPaymentFailed);
        Assert.Throws<DomainException>(r.MarkPaid);
    }

    [Fact]
    public void Ucretsiz_Surusun_Odemesi_Sonuclanamaz()
    {
        var r = new Ride(RideId.New(), Guid.NewGuid(), Guid.NewGuid(), new GeoPoint(41.0, 29.0), DateTime.UtcNow);
        r.Complete(new GeoPoint(41.0, 29.01), DateTime.UtcNow);

        Assert.Throws<DomainException>(r.MarkPaid);
    }

    [Fact]
    public void Borc_Surusun_Ucreti_Ve_Surucusuyle_Acilir()
    {
        var r = Ucretli();
        var borc = OutstandingDebt.ForFailedRidePayment(r, "  Kart reddedildi.  ", DateTime.UtcNow);

        Assert.Equal(r.DriverId, borc.DriverId);
        Assert.Equal(r.Id, borc.RideId);
        Assert.Equal(22.5m, borc.Amount);
        Assert.Equal("Kart reddedildi.", borc.Reason);
        Assert.Null(borc.SettledAt);
    }

    [Fact]
    public void Sebep_Yoksa_Varsayilan_Yazilir_Ve_Uzunsa_Kesilir()
    {
        var r = Ucretli();

        Assert.False(string.IsNullOrWhiteSpace(OutstandingDebt.ForFailedRidePayment(r, null, DateTime.UtcNow).Reason));
        Assert.Equal(OutstandingDebt.MaxReasonLength,
            OutstandingDebt.ForFailedRidePayment(r, new string('x', 1000), DateTime.UtcNow).Reason.Length);
    }

    [Fact]
    public void Kapatilan_Borc_Tekrar_Kapatilamaz()
    {
        var borc = OutstandingDebt.ForFailedRidePayment(Ucretli(), "x", DateTime.UtcNow);
        borc.Settle(DateTime.UtcNow);

        Assert.Throws<DomainException>(() => borc.Settle(DateTime.UtcNow));
    }
}
