using CrmSales.Api.Auditing;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Text.Json;

namespace CrmSales.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IServiceProvider services)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            // Unwrap TargetInvocationException so the real cause surfaces
            var real = ex is TargetInvocationException { InnerException: not null } tie
                ? tie.InnerException : ex;

            // Unwrap DbUpdateException to expose the database-level message
            if (real is DbUpdateException { InnerException: not null } due)
                real = due.InnerException!;

            logger.LogError(real, "Unhandled exception: {Message}", real.Message);
            await TryLogToAuditAsync(context, real);
            await HandleExceptionAsync(context, real);
        }
    }

    private async Task TryLogToAuditAsync(HttpContext context, Exception ex)
    {
        try
        {
            // Use a new scope so a faulted DbContext from the failed request doesn't affect audit logging
            using var scope = services.CreateScope();
            var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();
            var actor = context.User.FindFirst("preferred_username")?.Value ?? "anonymous";
            await audit.LogAsync(
                tenantId: "system",
                eventType: "system.error",
                entityType: context.Request.Path,
                entityId: context.TraceIdentifier,
                description: $"{ex.GetType().Name}: {ex.Message}",
                actor: actor);
        }
        catch (Exception logEx)
        {
            logger.LogWarning(logEx, "Failed to write error to audit log");
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var (statusCode, title) = ex switch
        {
            InvalidOperationException => (StatusCodes.Status409Conflict, "Conflict"),
            ArgumentException        => (StatusCodes.Status400BadRequest, "Bad Request"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error")
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        var problem = new
        {
            type    = $"https://httpstatuses.io/{statusCode}",
            title,
            status  = statusCode,
            detail  = ex.Message,
            traceId = context.TraceIdentifier
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}
