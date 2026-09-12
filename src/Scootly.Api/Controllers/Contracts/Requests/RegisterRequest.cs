using System.ComponentModel.DataAnnotations;

namespace Scootly.Api.Contracts.Requests;

/// <summary>
/// Kayıt isteği. İçinde ROL ALANI YOK ve olmamalı.
/// </summary>
/// <remarks>
/// İstek gövdesinden rol kabul etmek, herkesin kendini filo yöneticisi olarak
/// kaydedebilmesi demektir — yetki yükseltme (privilege escalation) açığının
/// en doğrudan biçimi. Kendi kendine kayıt olan herkes <c>Driver</c> rolüyle
/// başlar; daha yetkili roller yalnızca yetkili bir kullanıcı tarafından atanır.
/// </remarks>
public sealed record RegisterRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password);
