// =============================================================================
// IExceptionHandler — global exception handler returning ProblemDetails
// =============================================================================

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace YourProject.Api.Handlers;

/// <summary>
/// Global exception handler — fallback for anything not handled earlier.
/// Map exception types to ProblemDetails.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

        var problemDetails = CreateProblemDetails(exception);

        httpContext.Response.StatusCode = problemDetails.Status ?? (int)HttpStatusCode.InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await httpContext.Response.WriteAsync(json, cancellationToken);

        return true;
    }

    /// <summary>Build a ProblemDetails based on the exception type.</summary>
    private static ProblemDetails CreateProblemDetails(Exception exception) => exception switch
    {
        ArgumentException => new ProblemDetails
        {
            Type = "https://httpstatuses.com/400",
            Title = "Bad request",
            Status = (int)HttpStatusCode.BadRequest,
            Detail = exception.Message
        },
        KeyNotFoundException => new ProblemDetails
        {
            Type = "https://httpstatuses.com/404",
            Title = "Resource not found",
            Status = (int)HttpStatusCode.NotFound,
            Detail = exception.Message
        },
        UnauthorizedAccessException => new ProblemDetails
        {
            Type = "https://httpstatuses.com/401",
            Title = "Unauthorized",
            Status = (int)HttpStatusCode.Unauthorized,
            Detail = "You do not have permission to perform this operation."
        },
        TimeoutException => new ProblemDetails
        {
            Type = "https://httpstatuses.com/408",
            Title = "Request timeout",
            Status = (int)HttpStatusCode.RequestTimeout,
            Detail = "Operation timed out, please try again later."
        },
        InvalidOperationException => new ProblemDetails
        {
            Type = "https://httpstatuses.com/422",
            Title = "Invalid operation",
            Status = (int)HttpStatusCode.UnprocessableEntity,
            Detail = exception.Message
        },
        _ => new ProblemDetails
        {
            Type = "https://httpstatuses.com/500",
            Title = "Internal server error",
            Status = (int)HttpStatusCode.InternalServerError,
            Detail = "An unexpected error occurred, please contact your administrator."
        }
    };
}
