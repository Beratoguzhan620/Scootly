namespace Scootly.Api.Contracts.Responses;

/// <summary>Tek bir sürüşün okunabilir hali.</summary>
/// <remarks>
/// Alan modeli doğrudan döndürülmüyor. Ride bir toplu kök (aggregate root);
/// serileştirilirse hem alan olaylarını hem de ileride eklenecek her iç ayrıntıyı
/// sözleşmeye sızdırır. Sözleşme ayrı bir tip olduğunda alan modeli, uçları
/// kırmadan değişebilir.
/// </remarks>
public sealed record RideResponse(
    Guid Id,
    Guid VehicleId,
    DateTime StartedAt,
    DateTime? EndedAt,
    string Status,
    decimal? Fare);
