namespace Scootly.Domain.Riding;

public enum PaymentStatus
{
    /// <summary>Henüz ücretlendirilecek bir tutar yok (sürüş devam ediyor).</summary>
    None,

    /// <summary>Ücret belirlendi, ödeme sağlayıcısından onay bekleniyor veya yeniden denenecek.</summary>
    Pending,

    Paid,

    /// <summary>Azami deneme sayısına ulaşıldı; tahsilat operasyon ekibine devredilir.</summary>
    Failed
}
