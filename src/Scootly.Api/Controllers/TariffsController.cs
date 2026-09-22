using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Scootly.Api.Contracts.Responses;
using Scootly.Api.RateLimiting;
using Scootly.Application.Pricing.Queries;

namespace Scootly.Api.Controllers;

/// <summary>Tarife uçları (46. gün).</summary>
[ApiController]
[Route("api/v1/tariffs")]
public sealed class TariffsController : ControllerBase
{
    /// <summary>Çıktı önbelleği politikasının adı.</summary>
    public const string CiktiOnbellegi = "tarife-cikti";

    private readonly GetActiveTariffQueryHandler _handler;

    public TariffsController(GetActiveTariffQueryHandler handler)
    {
        _handler = handler;
    }

    /// <summary>Şu an geçerli tarife. Ziyaretçiye açık.</summary>
    /// <remarks>
    /// <para>
    /// <b>İki katmanlı önbellek var ve ikisi farklı şeyi önbelleğe alıyor</b>
    /// (50. gün):
    /// </para>
    /// <list type="bullet">
    ///   <item><b>Çıktı önbelleği (output cache):</b> HTTP yanıtının tamamını
    ///   saklıyor — serileştirme dahil. İsabet halinde controller'a hiç
    ///   girilmiyor.</item>
    ///   <item><b>Veri önbelleği:</b> handler'ın içinde, sorgu sonucunu
    ///   saklıyor. Çıktı önbelleği ıska verdiğinde bu devreye giriyor.</item>
    /// </list>
    /// <para>
    /// Çıktı önbelleğinin KİMLİK DOĞRULAMASI OLMAYAN uçlarda kullanılması
    /// şart. Kimliğe göre değişen bir yanıt böyle önbelleklenseydi, bir
    /// kullanıcının yanıtı başka bir kullanıcıya servis edilirdi — önbelleğin
    /// en pahalı hatası. Bu uç herkese aynı cevabı veriyor.
    /// </para>
    /// </remarks>
    [HttpGet("active")]
    [AllowAnonymous]
    [OutputCache(PolicyName = CiktiOnbellegi)]
    [EnableRateLimiting(RateLimitPolicies.AnonimHarita)]
    [ProducesResponseType(typeof(TariffResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Active(CancellationToken cancellationToken)
    {
        var tarife = await _handler.Handle(cancellationToken);

        if (tarife is null)
        {
            return NotFound();
        }

        return Ok(new TariffResponse(
            tarife.Id,
            tarife.Name,
            tarife.UnlockAmount,
            tarife.PerMinuteAmount,
            tarife.Currency));
    }
}
