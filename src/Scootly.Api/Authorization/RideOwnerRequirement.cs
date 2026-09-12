using Microsoft.AspNetCore.Authorization;

namespace Scootly.Api.Authorization;

/// <summary>
/// "Bu sürüş, isteği yapan sürücünün kendi sürüşü olmalı" kuralı.
/// </summary>
/// <remarks>
/// İçi boş, çünkü bir gereksinim (requirement) yalnızca bir işarettir — kuralın
/// ADI'dır, mantığı değil. Mantık <see cref="RideOwnerHandler"/> içinde.
/// Bu ayrım, aynı gereksinimi karşılayabilecek birden fazla kural yazılabilmesini
/// sağlar: ileride "denetçi rolündeki kullanıcı her sürüşe bakabilir" diye ikinci
/// bir handler eklenirse, politika tanımına dokunmadan çalışır.
/// </remarks>
public sealed class RideOwnerRequirement : IAuthorizationRequirement
{
}
