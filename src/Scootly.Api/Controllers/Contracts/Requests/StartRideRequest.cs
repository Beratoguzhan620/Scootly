using System.ComponentModel.DataAnnotations;

namespace Scootly.Api.Contracts.Requests;

/// <summary>
/// Sürüş başlatma isteği.
/// </summary>
/// <remarks>
/// <c>DriverId</c> 26. günkü OWASP taramasında KALDIRILDI. Sürücünün kimliği
/// artık token'dan okunuyor. Gövdeden alınırken, sürücü A gövdeye sürücü B'nin
/// kimliğini yazarak B adına sürüş başlatabiliyordu — kimlik doğrulamasından
/// geçmiş, rol kontrolünden geçmiş, ama başkası adına işlem yapan bir istek.
/// (OWASP A01: Broken Access Control / Insecure Direct Object Reference.)
/// </remarks>
public sealed record StartRideRequest(
    [property: Required] Guid VehicleId);
