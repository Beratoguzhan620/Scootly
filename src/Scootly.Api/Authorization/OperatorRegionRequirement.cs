using Microsoft.AspNetCore.Authorization;

namespace Scootly.Api.Authorization;

/// <summary>
/// "Operatör yalnızca kendi bölgesindeki kaynağa erişebilir" kuralı.
/// Mantığı <see cref="OperatorRegionHandler"/> içinde.
/// </summary>
public sealed class OperatorRegionRequirement : IAuthorizationRequirement
{
}
