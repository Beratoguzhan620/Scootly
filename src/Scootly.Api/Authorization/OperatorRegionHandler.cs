using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Scootly.Domain.Common;
using Scootly.Infrastructure.Identity;

namespace Scootly.Api.Authorization;

/// <summary>
/// Operatörün bölgesi ile kaynağın bölgesini karşılaştırır.
/// </summary>
public sealed class OperatorRegionHandler
    : AuthorizationHandler<OperatorRegionRequirement, IRegionScoped>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperatorRegionRequirement requirement,
        IRegionScoped resource)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);

        var kullaniciBolgesi = context.User.FindFirstValue(ScootlyClaimTypes.HomeRegion);

        if (string.IsNullOrWhiteSpace(kullaniciBolgesi) || string.IsNullOrWhiteSpace(resource.Region))
        {
            return Task.CompletedTask;
        }

        // OrdinalIgnoreCase — kültüre duyarlı karşılaştırma DEĞİL.
        //
        // Türkçe yerel ayarında büyük "I" harfinin küçüğü noktasız "ı"dır.
        // CurrentCultureIgnoreCase kullanılsaydı, "ISTANBUL" ile "istanbul"
        // Türkçe bir makinede eşleşmez, İngilizce bir makinede eşleşirdi —
        // yani yetkilendirme kararı, kodun çalıştığı makinenin dil ayarına
        // göre değişirdi. Yetkilendirmede makineye göre değişen bir karar,
        // tanımı gereği bir güvenlik açığıdır.
        if (string.Equals(kullaniciBolgesi, resource.Region, StringComparison.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
