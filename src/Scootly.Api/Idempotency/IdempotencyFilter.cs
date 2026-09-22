using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Scootly.Application.Abstractions;

namespace Scootly.Api.Idempotency;

/// <summary>
/// <see cref="IdempotentAttribute"/> taşıyan uçları tekrara dayanıklı kılar
/// (60. gün deneyi — öznitelik yaklaşımının çalışan hali).
/// </summary>
/// <remarks>
/// <para>
/// Özniteliği <b>yansımayla (reflection)</b> buluyor. Deneyin asıl öğretici
/// tarafı burası: yansıma çalışma zamanında iş yapıyor, yani bir ucun
/// işaretlenip işaretlenmediği derleme zamanında bilinmiyor. Özniteliği
/// yazmayı unutan bir uç, hiçbir uyarı üretmeden korumasız kalıyor.
/// </para>
/// <para>
/// ASP.NET Core öznitelik keşfini uç oluşturulurken bir kez yapıp
/// önbelleklediği için buradaki yansımanın ölçülebilir bir maliyeti yok —
/// yani "yansıma yavaştır" itirazı bu senaryoda geçerli değil. Reddedilme
/// sebebi performans değil (bkz. ADR 0020).
/// </para>
/// </remarks>
public sealed class IdempotencyFilter : IAsyncActionFilter
{
    private readonly IIdempotencyStore _store;

    public IdempotencyFilter(IIdempotencyStore store)
    {
        _store = store;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var isaret = context.ActionDescriptor.EndpointMetadata
            .OfType<IdempotentAttribute>()
            .FirstOrDefault();

        if (isaret is null)
        {
            // İşaretlenmemiş uç: koruma yok. Sessiz olan kısım tam olarak bu.
            await next();
            return;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(IdempotentAttribute.BaslikAdi, out var ham)
            || string.IsNullOrWhiteSpace(ham))
        {
            context.Result = new BadRequestObjectResult(
                $"{IdempotentAttribute.BaslikAdi} başlığı zorunlu.");
            return;
        }

        var anahtar = ham.ToString();

        // Anahtar YOL ile birlikte saklanıyor: aynı anahtarın iki farklı uca
        // gönderilmesi iki farklı işlem. Yalnızca anahtar saklansaydı, bir
        // istemcinin tesadüfen aynı anahtarı yeniden kullanması alakasız bir
        // ucu sessizce engellerdi.
        var kapsamliAnahtar = $"idem:{context.HttpContext.Request.Path}:{anahtar}";

        var ilkKez = await _store.IlkKezMiAsync(
            kapsamliAnahtar,
            TimeSpan.FromMinutes(isaret.HatirlamaSuresiDakika),
            context.HttpContext.RequestAborted);

        if (!ilkKez)
        {
            // 409 değil 200: istemcinin bakış açısından işlem BAŞARILI oldu,
            // yalnızca ilk denemede. Çakışma dönmek, istemciyi olmayan bir
            // hatayı ele almaya zorlardı.
            context.Result = new OkObjectResult(new { tekrar = true });
            return;
        }

        await next();
    }
}
