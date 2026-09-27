namespace Scootly.Domain.Riding;

/// <summary>Bir sürüşün ödeme sonucu (69. gün).</summary>
/// <remarks>
/// <c>Ride.PaymentStatus</c> NULL ise ödeme henüz sonuçlanmadı. Ayrı bir
/// "Bekliyor" değeri yerine NULL: sütun migration'la mevcut satırlara
/// eklendiğinde eski sürüşler için anlamlı tek değer "bilinmiyor" ve NULL tam
/// olarak bu. Varsayılan bir enum değeri koymak, geçmişteki her sürüşü
/// yalan yere "ödenmedi" diye işaretlerdi.
/// </remarks>
public enum RidePaymentStatus
{
    /// <summary>Ödeme yetkilendirildi.</summary>
    Paid,

    /// <summary>Ödeme alınamadı; sürücü adına borç kaydı açıldı.</summary>
    PaymentFailed
}
