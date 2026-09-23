// =============================================================================
// IExceptionHandler — FluentValidation -> ValidationProblemDetails
// =============================================================================

using System.Net;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace YourProject.Api.Handlers;

/// <summary>
/// Dedicated handler for FluentValidation's ValidationException.
/// Register before the generic GlobalExceptionHandler so it gets first chance.
/// </summary>
public class FluentValidationExceptionHandler : IExceptionHandler
{
    private readonly ILogger<FluentValidationExceptionHandler> _logger;

    public FluentValidationExceptionHandler(ILogger<FluentValidationExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Only handle FluentValidation's ValidationException.
        if (exception is not ValidationException validationException)
        {
            return false; // Let the next handler take over.
        }

        _logger.LogWarning(validationException, "Validation failed: {Message}", validationException.Message);

        var problemDetails = new ValidationProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Title = "One or more validation errors occurred.",
            Status = (int)HttpStatusCode.BadRequest,
            Detail = "The submitted data has validation errors, please review and retry.",
            Instance = httpContext.Request.Path
        };

        // Map validation errors into the ValidationProblemDetails shape.
        foreach (var error in validationException.Errors)
        {
            var propertyName = error.PropertyName;
            var errorMessage = error.ErrorMessage;

            if (problemDetails.Errors.ContainsKey(propertyName))
            {
                var existingErrors = problemDetails.Errors[propertyName].ToList();
                existingErrors.Add(errorMessage);
                problemDetails.Errors[propertyName] = existingErrors.ToArray();
            }
            else
            {
                problemDetails.Errors.Add(propertyName, new[] { errorMessage });
            }
        }

        httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
        httpContext.Response.ContentType = "application/problem+json";

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await httpContext.Response.WriteAsync(json, cancellationToken);

        return true;
    }
}
