using System.ComponentModel.DataAnnotations;

namespace Scootly.Api.Contracts.Requests;

/// <summary>
/// Sayfalama parametreleri (sorgu dizesinden gelir).
/// </summary>
/// <remarks>
/// <see cref="MaxPageSize"/> yalnızca bir performans ayarı değil, bir güvenlik
/// sınırı: üst sınır olmasaydı bir istemci <c>?pageSize=1000000</c> yazarak
/// sunucuyu tek istekte tüm tabloyu belleğe almaya zorlayabilirdi. Kimlik
/// doğrulaması gerektirmeyen bir uçta (bu uç ziyaretçiye açık) bunun maliyeti
/// saldırgan için sıfırdır.
/// </remarks>
public sealed record PageRequest
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 20;

    [Range(1, int.MaxValue)]
    public int PageNumber { get; init; } = 1;

    [Range(1, MaxPageSize)]
    public int PageSize { get; init; } = DefaultPageSize;
}
