using System.ComponentModel.DataAnnotations;
using Scootly.Application.Fleet.Queries;

namespace Scootly.Api.Contracts.Requests;

/// <summary>
/// Harita sorgusunun parametreleri (sorgu dizesinden gelir).
/// </summary>
/// <remarks>
/// <see cref="PageRequest"/>'i miras almıyor, alanlarını tekrarlıyor:
/// model bağlama (model binding) kalıtımla çalışmıyor değil, ama iki farklı
/// uç sözleşmesini kalıtımla bağlamak, birinin değişmesinin diğerini sessizce
/// değiştirmesi demek. Sözleşmeler tekrar etmeyi hak eder.
/// </remarks>
public sealed record NearbyQueryRequest
{
    [Range(-90, 90)]
    public double Latitude { get; init; }

    [Range(-180, 180)]
    public double Longitude { get; init; }

    /// <summary>Metre cinsinden arama yarıçapı.</summary>
    [Range(1, FindNearbyVehiclesQuery.MaxRadiusMeters)]
    public double RadiusMeters { get; init; } = FindNearbyVehiclesQuery.DefaultRadiusMeters;

    [Range(1, int.MaxValue)]
    public int PageNumber { get; init; } = 1;

    [Range(1, PageRequest.MaxPageSize)]
    public int PageSize { get; init; } = PageRequest.DefaultPageSize;

    public FindNearbyVehiclesQuery ToQuery() =>
        new(Latitude, Longitude, RadiusMeters, PageNumber, PageSize);
}
