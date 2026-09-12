using System.Net;
using System.Text.Json;
using Scootly.Api.Contracts.Responses;
using Scootly.Application.Common;
using Scootly.Domain.Common;

namespace Scootly.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await _next(context);
        }
        catch (ConcurrencyConflictException)
        {
            // 38. gun. Handler'lar bunu zaten yakalayip Result.Failure donuyor;
            // buradaki yakalama, ileride eklenecek ve yakalamayi unutan bir
            // handler'in istemciye 500 dondurmesini engelleyen ag.
            // Mesaj sabit: istisnanin kendi metni ic detay tasiyabilir.
            await YanitYaz(
                context,
                HttpStatusCode.Conflict,
                "Eszamanlilik Cakismasi",
                "Biri sizden önce davrandı. Lütfen tekrar deneyin.");
        }
        catch (DomainException ex)
        {
            // Alan kuralı ihlalinin mesajı istemciye gösterilebilir: bu mesajlar
            // bizim yazdığımız, kullanıcıya söylenmek üzere kurulmuş cümleler
            // ("Araç müsait değil, rezerve edilemez.").
            await YanitYaz(context, HttpStatusCode.Conflict, "Domain Kuralı İhlali", ex.Message);
        }
        catch (Exception ex)
        {
            // 26. gündeki taramanın bulduğu bilgi ifşası.
            //
            // Önceki hali ex.Message değerini doğrudan istemciye yazıyordu.
            // Beklenmeyen bir istisnanın mesajı bizim yazdığımız bir cümle
            // değildir: içinde bağlantı dizesi parçası, dosya yolu, sunucu adı,
            // SQL parçası veya kütüphane iç detayı olabilir. Bunların her biri
            // saldırgana sistemin haritasını çıkarmakta yardım eder.
            //
            // Artık istemci yalnızca bir referans numarası görüyor; ayrıntı
            // sunucu tarafındaki loga yazılıyor. Destek istendiğinde referans
            // numarasıyla ilgili kayıt bulunabiliyor.
            _logger.LogError(ex, "Beklenmeyen hata. Referans: {TraceId}", context.TraceIdentifier);

            // Yanıt gövdesi yazılmaya başlanmışsa başlık ve durum kodu
            // değiştirilemez; yazmaya çalışmak ikinci bir istisna üretir.
            if (context.Response.HasStarted)
            {
                return;
            }

            context.Response.Clear();

            await YanitYaz(
                context,
                HttpStatusCode.InternalServerError,
                "Beklenmeyen Hata",
                $"Beklenmeyen bir hata oluştu. Referans: {context.TraceIdentifier}");
        }
    }

    private static async Task YanitYaz(
        HttpContext context,
        HttpStatusCode statusCode,
        string title,
        string detail)
    {
        var response = new ApiErrorResponse(title, detail, (int)statusCode);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}
