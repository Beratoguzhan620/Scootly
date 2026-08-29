using Scootly.Api.Contracts.Responses;
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
        try
        {
            await _next(context);
        }
        catch (DomainException exception)
        {
            _logger.LogWarning(exception, "İş kuralı ihlali: {Message}", exception.Message);

            await WriteErrorAsync(
                context,
                StatusCodes.Status409Conflict,
                "İş kuralı ihlali",
                exception.Message);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Beklenmeyen hata");

            await WriteErrorAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "Sunucu hatası",
                "Beklenmeyen bir hata oluştu.");
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, string title, string detail)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new ApiErrorResponse(title, detail, statusCode));
    }
}