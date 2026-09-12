using Scootly.Infrastructure.Identity;

namespace Scootly.Api.Middleware;

public sealed class DeviceAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public DeviceAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, DeviceTokenService deviceTokenService)
    {
        if (context.Request.Path.StartsWithSegments("/api/telemetry"))
        {
            var authHeader = context.Request.Headers.Authorization.ToString();

            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }

        await _next(context);
    }
}