using System.Security.Claims;
using Echo.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Echo.Api;

public static class HttpContextExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier)
                 ?? user.FindFirstValue("sub");
        if (id is null || !Guid.TryParse(id, out var guid))
        {
            throw new AppException(ErrorCodes.NotFound, "Unauthorized.", 401);
        }

        return guid;
    }

    public static string? GetClientIp(this HttpContext http)
    {
        var forwarded = http.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }

        return http.Connection.RemoteIpAddress?.ToString();
    }
}

public sealed class ErrorResponse
{
    public required string Code { get; init; }
    public required string Message { get; init; }
}

public sealed class ExceptionMappingMiddleware(RequestDelegate next, ILogger<ExceptionMappingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (AppException ex)
        {
            context.Response.StatusCode = ex.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ErrorResponse { Code = ex.Code, Message = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error");
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ErrorResponse
            {
                Code = "INTERNAL_ERROR",
                Message = "Unexpected server error."
            });
        }
    }
}
