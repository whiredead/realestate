using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Reservations.Entities;
using ValidationException = ProjectAPI.Api.Application.Common.Exceptions.ValidationException;

namespace ProjectAPI.Api.Filters;

/// <summary>
/// Represents an attribute implementing the IExceptionFilter interface to globally handle exceptions within an API.
/// </summary>
public class ApiExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ApiExceptionFilter> _logger;

    /// <summary>
    /// Dictionary containing registered exception types and their respective handlers.
    /// </summary>
    private readonly Dictionary<Type, Action<ExceptionContext>> _exceptionHandlers;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiExceptionFilter"/> class.
    /// Registers known exception types and their associated handlers.
    /// </summary>
    /// <remarks>
    /// The known exception types and their respective handlers are added to the internal dictionary of the <see cref="ApiExceptionFilter"/> class
    /// to manage and handle exceptions uniformly within the API.
    /// </remarks>
    public ApiExceptionFilter(ILogger<ApiExceptionFilter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Register known exception types and handlers.
        _exceptionHandlers = new Dictionary<Type, Action<ExceptionContext>>
        {
            { typeof(ValidationException), HandleValidationException },
            { typeof(NotFoundException), HandleNotFoundException },
            { typeof(UnauthorizedAccessException), HandleUnauthorizedAccessException },
            { typeof(BusinessRuleException), HandleBusinessRuleException },
            { typeof(InvalidReservationTransitionException), HandleInvalidTransitionException },
            { typeof(Exception), HandleGlobalException }
        };
    }

    /// <summary>
    /// Applies the members every error response must carry per RFC 9457 and
    /// spec §31.2: a stable <c>code</c>, the request path as <c>instance</c>,
    /// and a correlatable <c>requestId</c>.
    /// </summary>
    private static void Enrich(ProblemDetails details, ExceptionContext context, string code, int statusCode)
    {
        details.Status = statusCode;
        details.Instance = context.HttpContext.Request.Path;
        details.Extensions["code"] = code;
        details.Extensions["requestId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
    }

    /// <summary>
    /// Handles exceptions caught in the application.
    /// </summary>
    /// <param name="context">The ExceptionContext containing information about the exception.</param>
    public void OnException(ExceptionContext context)
    {
        HandleException(context);
    }

    /// <summary>
    /// Handles the incoming exception based on its type by invoking the corresponding registered handler.
    /// </summary>
    /// <param name="context">The ExceptionContext containing information about the exception.</param>
    /// <summary>
    /// SQL Server error numbers raised by a unique/PK constraint violation.
    /// </summary>
    private static readonly int[] UniqueViolationNumbers = { 2601, 2627 };

    /// <summary>
    /// Detects a unique-index violation anywhere in the exception chain.
    /// The filtered index IX_Reservations_ActivePerUnit is the concurrency
    /// backstop for §7.7, so losing a race must surface as 409 UNIT_NOT_AVAILABLE
    /// rather than a generic 500 (ADR-0002).
    /// </summary>
    private static bool TryGetUniqueViolation(Exception? exception, out string? indexName)
    {
        indexName = null;
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql && UniqueViolationNumbers.Contains(sql.Number))
            {
                indexName = sql.Message;
                return true;
            }
        }
        return false;
    }

    private void HandleException(ExceptionContext context)
    {
        // Translate database-level uniqueness failures before generic handling.
        if (TryGetUniqueViolation(context.Exception, out var message))
        {
            var isActiveReservation = message?.Contains("IX_Reservations_ActivePerUnit", StringComparison.OrdinalIgnoreCase) == true;

            var conflict = new ProblemDetails
            {
                Title = "Règle métier non satisfaite.",
                Detail = isActiveReservation
                    ? "Le bien n'est plus disponible : une réservation active existe déjà."
                    : "Cette opération viole une contrainte d'unicité.",
                Type = "https://docs.gpia.example/problems/unit-not-available"
            };

            var code = isActiveReservation
                ? BusinessErrorCodes.UnitNotAvailable
                : BusinessErrorCodes.ResourceVersionConflict;

            Enrich(conflict, context, code, StatusCodes.Status409Conflict);

            context.Result = new ObjectResult(conflict) { StatusCode = StatusCodes.Status409Conflict };
            _logger.LogInformation("[Conflict] {Code} on {Path}", code, context.HttpContext.Request.Path);
            context.ExceptionHandled = true;
            return;
        }

        // Walk the hierarchy so subclasses reach their base handler instead of
        // silently falling through to the generic 500.
        for (Type? type = context.Exception.GetType(); type is not null; type = type.BaseType)
        {
            if (_exceptionHandlers.TryGetValue(type, out Action<ExceptionContext>? value))
            {
                value.Invoke(context);
                return;
            }
        }

        HandleGlobalException(context);
    }

    #region Exception Handlers

    /// <summary>
    /// Handles the ValidationException and returns a BadRequestObjectResult with validation details.
    /// </summary>
    /// <param name="context">The ExceptionContext containing information about the exception.</param>
    private void HandleValidationException(ExceptionContext context)
    {
        var exception = (ValidationException)context.Exception;

        var details = new ValidationProblemDetails(exception.Errors)
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "Données invalides."
        };

        // §31.3: syntactically valid but failing a field/business validation → 422.
        Enrich(details, context, BusinessErrorCodes.ValidationFailed, StatusCodes.Status422UnprocessableEntity);

        context.Result = new ObjectResult(details)
        {
            StatusCode = StatusCodes.Status422UnprocessableEntity
        };

        context.ExceptionHandled = true;
    }

    /// <summary>
    /// Maps a refused reservation transition to 409 INVALID_STATUS_TRANSITION (§12.4).
    /// </summary>
    private void HandleInvalidTransitionException(ExceptionContext context)
    {
        var exception = (InvalidReservationTransitionException)context.Exception;

        var details = new ProblemDetails
        {
            Title = "Transition de statut non autorisée.",
            Detail = exception.Message,
            Type = "https://docs.gpia.example/problems/invalid-status-transition"
        };

        Enrich(details, context, BusinessErrorCodes.InvalidStatusTransition, StatusCodes.Status409Conflict);

        context.Result = new ObjectResult(details) { StatusCode = StatusCodes.Status409Conflict };

        _logger.LogInformation(
            "[Transition] refused {From} -> {To} on {Path}",
            exception.From, exception.To, context.HttpContext.Request.Path);

        context.ExceptionHandled = true;
    }

    /// <summary>
    /// Handles a domain rule violation, mapping it to the status and stable code
    /// declared by the exception.
    /// </summary>
    private void HandleBusinessRuleException(ExceptionContext context)
    {
        var exception = (BusinessRuleException)context.Exception;

        var details = new ProblemDetails
        {
            Title = "Règle métier non satisfaite.",
            Detail = exception.Message,
            Type = $"https://docs.gpia.example/problems/{exception.Code.ToLowerInvariant().Replace('_', '-')}"
        };

        Enrich(details, context, exception.Code, exception.StatusCode);

        context.Result = new ObjectResult(details) { StatusCode = exception.StatusCode };

        _logger.LogInformation(
            "[BusinessRule] {Code} on {Path}: {Message}",
            exception.Code, context.HttpContext.Request.Path, exception.Message);

        context.ExceptionHandled = true;
    }

    /// <summary>
    /// Handles the NotFoundException and returns a NotFoundObjectResult with problem details.
    /// </summary>
    /// <param name="context">The ExceptionContext containing information about the exception.</param>
    private void HandleNotFoundException(ExceptionContext context)
    {
        var exception = (NotFoundException)context.Exception;

        var details = new ProblemDetails()
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            Title = "Ressource introuvable.",
            Detail = exception.Message
        };

        Enrich(details, context, BusinessErrorCodes.NotFound, StatusCodes.Status404NotFound);

        context.Result = new NotFoundObjectResult(details);

        context.ExceptionHandled = true;
    }

    /// <summary>
    /// Handles the UnauthorizedAccessException and returns an ObjectResult with unauthorized details.
    /// </summary>
    /// <param name="context">The ExceptionContext containing information about the exception.</param>
    private void HandleUnauthorizedAccessException(ExceptionContext context)
    {
        var exception = (UnauthorizedAccessException)context.Exception;

        var details = new ProblemDetails
        {
            Title = "Non authentifié.",
            Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
            Detail = exception.Message
        };

        Enrich(details, context, BusinessErrorCodes.Unauthorized, StatusCodes.Status401Unauthorized);

        context.Result = new ObjectResult(details)
        {
            StatusCode = StatusCodes.Status401Unauthorized
        };

        context.ExceptionHandled = true;
    }

    /// <summary>
    /// Handles an unhandled exception by creating a generic error response for internal server errors.
    /// Logs the exception details and sets the appropriate HTTP status code.
    /// </summary>
    /// <param name="context">The ExceptionContext containing information about the exception.</param>
    private void HandleGlobalException(ExceptionContext context)
    {
        // §31.2: never leak stack traces, SQL or sensitive data to the client.
        // The requestId ties this response to the server-side log entry.
        var details = new ProblemDetails
        {
            Title = "Une erreur interne est survenue.",
            Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1"
        };

        Enrich(details, context, BusinessErrorCodes.InternalError, StatusCodes.Status500InternalServerError);

        context.Result = new ObjectResult(details)
        {
            StatusCode = StatusCodes.Status500InternalServerError
        };

        _logger.LogError(
            context.Exception,
            "[Unhandled] {Path} requestId={RequestId}",
            context.HttpContext.Request.Path,
            details.Extensions["requestId"]);

        context.ExceptionHandled = true;
    }

    #endregion
}
