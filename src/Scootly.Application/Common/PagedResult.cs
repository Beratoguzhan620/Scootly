namespace Scootly.Application.Common;

/// <summary>
/// Sayfalanmış sonuç.
/// </summary>
/// <remarks>
/// Toplam sayı da dönüyor, çünkü istemcinin "daha var mı" sorusunu ancak
/// böyle cevaplayabiliyor. Bedeli, her sayfa isteğinde ikinci bir COUNT
/// sorgusu. Faz 3'te bu ölçülecek; tablo büyüdüğünde COUNT'un maliyeti
/// sayfanın kendisinden yüksek olabilir ve o noktada imleç (cursor) tabanlı
/// sayfalamaya geçmek gerekebilir.
/// </remarks>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize <= 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasNextPage => PageNumber < TotalPages;
}
