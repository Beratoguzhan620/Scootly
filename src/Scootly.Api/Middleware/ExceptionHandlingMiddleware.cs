using System.Net;
using System.Text.Json;
using Scootly.Api.Contracts.Responses;
using Scootly.Domain.Common;

namespace Scootly.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            await WriteErrorResponse(context, HttpStatusCode.Conflict, "Domain Kuralı İhlali", ex.Message);
        }
        catch (Exception ex)
        {
            await WriteErrorResponse(context, HttpStatusCode.InternalServerError, "Beklenmeyen Hata", ex.Message);
        }
    }

    private static async Task WriteErrorResponse(HttpContext context, HttpStatusCode statusCode, string title, string detail)
    {
        var response = new ApiErrorResponse(title, detail, (int)statusCode);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }
}