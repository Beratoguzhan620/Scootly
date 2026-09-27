using Scootly.Domain.Common;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding;
using Xunit;

namespace Scootly.Domain.UnitTests;

public class RidePaymentTests
{
    private static readonly DateTime StartedAt = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

    private static Ride CreateCompletedRide()
    {
        var ride = new Ride(RideId.New(), Guid.NewGuid(), Guid.NewGuid(), new GeoPoint(41.0, 29.0), StartedAt);
        ride.Complete(new GeoPoint(41.01, 29.01), StartedAt.AddMinutes(4), Tariff.Standard);
        return ride;
    }

    [Fact]
    public void Aktif_Suruste_Odeme_Kaydedilemez()
    {
        var ride = new Ride(RideId.New(), Guid.NewGuid(), Guid.NewGuid(), new GeoPoint(41.0, 29.0), StartedAt);

        Assert.Throws<DomainException>(() => ride.RecordPaymentApproved(StartedAt));
        Assert.Throws<DomainException>(() => ride.RecordPaymentDeclined("ret", StartedAt));
    }

    [Fact]
    public void Onay_Odemeyi_Tamamlamali_Ve_Tekrari_Etkisiz_Olmali()
    {
        var ride = CreateCompletedRide();

        ride.RecordPaymentApproved(StartedAt.AddMinutes(5));
        ride.RecordPaymentApproved(StartedAt.AddMinutes(6));

        Assert.Equal(PaymentStatus.Paid, ride.PaymentStatus);
        Assert.Equal(StartedAt.AddMinutes(5), ride.PaidAt);
        Assert.Equal(1, ride.PaymentAttempts);
    }

    [Fact]
    public void Her_Ret_Bir_Deneme_Sayilmali_Ve_Idempotency_Anahtari_Degismeli()
    {
        var ride = CreateCompletedRide();
        var firstKey = ride.NextPaymentIdempotencyKey;

        ride.RecordPaymentDeclined("Yetersiz bakiye", StartedAt.AddMinutes(5));

        Assert.Equal(PaymentStatus.Pending, ride.PaymentStatus);
        Assert.Equal(1, ride.PaymentAttempts);
        Assert.Equal("Yetersiz bakiye", ride.LastPaymentError);
        Assert.NotEqual(firstKey, ride.NextPaymentIdempotencyKey);
    }

    [Fact]
    public void Azami_Denemeden_Sonra_Odeme_Basarisiz_Sayilmali()
    {
        var ride = CreateCompletedRide();

        for (var i = 0; i < Ride.MaxPaymentAttempts; i++)
            ride.RecordPaymentDeclined("ret", StartedAt.AddMinutes(5 + i));

        Assert.Equal(PaymentStatus.Failed, ride.PaymentStatus);
        Assert.Throws<DomainException>(() => ride.RecordPaymentDeclined("ret", StartedAt.AddHours(1)));
    }

    [Fact]
    public void Gec_Gelen_Onay_Basarisiz_Odemeyi_Tamamlayabilmeli()
    {
        var ride = CreateCompletedRide();

        for (var i = 0; i < Ride.MaxPaymentAttempts; i++)
            ride.RecordPaymentDeclined("ret", StartedAt.AddMinutes(5 + i));

        ride.RecordPaymentApproved(StartedAt.AddHours(1));

        Assert.Equal(PaymentStatus.Paid, ride.PaymentStatus);
    }

    [Fact]
    public void Cok_Uzun_Hata_Mesaji_Kirpilmali()
    {
        var ride = CreateCompletedRide();

        ride.RecordPaymentDeclined(new string('x', 2_000), StartedAt.AddMinutes(5));

        Assert.Equal(Ride.PaymentErrorMaxLength, ride.LastPaymentError!.Length);
    }
}
