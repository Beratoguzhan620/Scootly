using System.ComponentModel.DataAnnotations;

namespace Scootly.Api.Contracts.Requests;

/// <summary>
/// Cihaz token isteği. İnsan girişinden farkı, kullanıcı adı/parola yerine
/// cihaz kimliği ve sırrı taşıması ve tarayıcı yönlendirmesi gerektirmemesi.
/// </summary>
public sealed record DeviceTokenRequest(
    [property: Required, StringLength(64, MinimumLength = 4)] string DeviceId,
    [property: Required, StringLength(256, MinimumLength = 16)] string Secret);
