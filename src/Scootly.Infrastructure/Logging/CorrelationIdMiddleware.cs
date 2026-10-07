using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Scootly.Infrastructure.Logging;

/// <summary>
/// Gelen X-Correlation-Id başlığını kullanır ya da üretir, tüm log olaylarına ekler,
/// yanıt başlığına geri yazar — istemci tarafı günlükleriyle eşleştirme sağlar.
/// İstemcinin değeri yalnızca kısa ve güvenli karakterlerden oluşuyorsa kabul edilir; aksi halde
/// (log enjeksiyonu, aşırı uzun başlık) yenisi üretilir.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const int MaxLength = 64;

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var provided = context.Request.Headers[HeaderName].ToString();
        var correlationId = IsAcceptable(provided) ? provided : Guid.NewGuid().ToString("N");

        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    public static bool IsAcceptable(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength)
            return false;

        foreach (var character in value)
        {
            if (!(char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or ':'))
                return false;
        }

        return true;
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseScootlyCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}
