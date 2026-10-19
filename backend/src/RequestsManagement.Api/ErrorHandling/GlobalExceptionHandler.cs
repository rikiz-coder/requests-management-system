using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RequestsManagement.Application.Requests.Exceptions;

namespace RequestsManagement.Api.ErrorHandling;

/// <summary>
/// One place that turns exceptions into Problem Details (RFC 9457) with the right status code.
/// Unexpected errors are logged with the exception and returned as 500 without internal details.
/// </summary>
internal sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            // The client went away (for example, a newer search replaced this one). Not an error.
            logger.LogInformation("Request {Method} {Path} was cancelled by the client",
                httpContext.Request.Method, httpContext.Request.Path);
            return true;
        }

        ProblemDetails problem;
        switch (exception)
        {
            case ValidationException validation:
                var errors = validation.Errors
                    .GroupBy(e => ToCamelCase(e.PropertyName))
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
                logger.LogWarning("Validation failed for {Method} {Path}: {Fields}",
                    httpContext.Request.Method, httpContext.Request.Path, string.Join(", ", errors.Keys));
                problem = new HttpValidationProblemDetails(errors)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "One or more validation errors occurred.",
                };
                break;

            case NotFoundException notFound:
                logger.LogWarning("Request {RequestId} was not found", notFound.RequestId);
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Request not found",
                    Detail = notFound.Message,
                };
                break;

            case ConcurrencyConflictException conflict:
                logger.LogWarning("Concurrency conflict on request {RequestId}", conflict.RequestId);
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Concurrency conflict",
                    Detail = conflict.Message,
                    Extensions =
                    {
                        ["currentState"] = conflict.CurrentState,
                        ["lastChange"] = conflict.LastChange,
                    },
                };
                break;

            case InvalidStatusTransitionException transition:
                logger.LogWarning("Rejected status change of request {RequestId} from {From} to {To}",
                    transition.RequestId, transition.CurrentStatus, transition.RequestedStatus);
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status422UnprocessableEntity,
                    Title = "Status change not allowed",
                    Detail = transition.Message,
                    Extensions =
                    {
                        ["currentStatus"] = transition.CurrentStatus,
                        ["requestedStatus"] = transition.RequestedStatus,
                        ["allowedStatuses"] = transition.AllowedStatuses,
                    },
                };
                break;

            default:
                logger.LogError(exception, "Unhandled error on {Method} {Path}",
                    httpContext.Request.Method, httpContext.Request.Path);
                problem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred.",
                };
                break;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) || char.IsLower(name[0]) || name.StartsWith("X-", StringComparison.Ordinal)
            ? name
            : char.ToLowerInvariant(name[0]) + name[1..];
}
