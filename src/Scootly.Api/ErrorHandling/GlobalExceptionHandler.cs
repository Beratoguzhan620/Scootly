using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Scootly.Application.Abstractions.Exceptions;
using Scootly.Application.Common;
using Scootly.Domain.Common;

namespace Scootly.Api.ErrorHandling;

/// <summary>
/// Yakalanmamış istisnaları RFC 7807 (ProblemDetails) yanıtına çevirir.
/// Beklenmeyen hatalarda iç ayrıntı istemciye verilmez; tamamı loglanır ve yanıtta yalnızca izleme kimliği döner.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
    {
        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // İstemci bağlantıyı kapattı; yazılacak bir yanıt yok.
            httpContext.Response.StatusCode = 499;
            return true;
        }

        var (statusCode, title, detail) = exception switch
        {
            DomainException ex => (StatusCodes.Status409Conflict, "İş kuralı ihlali", ex.Message),
            ConcurrencyConflictException => (StatusCodes.Status409Conflict, "Eşzamanlılık çakışması", "Kayıt bu sırada değişti. Lütfen tekrar deneyin."),
            UniqueConstraintViolationException ex => (StatusCodes.Status409Conflict, "Çakışma", ConstraintNames.ToUserMessage(ex.ConstraintName)),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Kimlik doğrulanamadı", "Bu işlem için geçerli bir kullanıcı oturumu gerekli."),
            BadHttpRequestException ex => (ex.StatusCode, "Geçersiz istek", "İstek işlenemedi."),
            _ => (StatusCodes.Status500InternalServerError, "Beklenmeyen hata", "Beklenmeyen bir hata oluştu. Destek ekibine izleme kimliğini iletebilirsiniz.")
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "İşlenmeyen hata: {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        else
            _logger.LogWarning(exception, "İstek {StatusCode} ile sonuçlandı: {Method} {Path}", statusCode, httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = statusCode;

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail
            }
        });
    }
}
