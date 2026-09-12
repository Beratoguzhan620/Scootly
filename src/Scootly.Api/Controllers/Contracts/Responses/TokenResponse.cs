namespace Scootly.Api.Contracts.Responses;

/// <summary>
/// Başarılı girişin yanıtı. Yenileme (refresh) token'ı henüz yok —
/// oturum süresi dolunca yeniden giriş gerekir. Bu bilinçli bir sadeleştirme,
/// teknik borç listesinde kayıtlı.
/// </summary>
public sealed record TokenResponse(string AccessToken, DateTime ExpiresAtUtc);
