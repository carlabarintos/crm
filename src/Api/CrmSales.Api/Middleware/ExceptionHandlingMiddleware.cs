using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Text.Json;

namespace CrmSales.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
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
            await HandleExceptionAsync(context, real);
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
