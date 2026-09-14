using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProjectAPI.Api.Application.Common.Exceptions;
using ProjectAPI.Domain.Immeubles.Entities;
using ProjectAPI.Domain.Reservations.Entities;
using ProjectAPI.Domain.Sales.Entities;
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
            { typeof(InvalidUnitTransitionException), HandleInvalidUnitTransitionException },
            { typeof(InvalidClaimTransitionException), HandleInvalidClaimTransitionException },
            { typeof(InvalidSaleTransitionException), HandleInvalidSaleTransitionException },
            { typeof(DbUpdateConcurrencyException), HandleConcurrencyException },
            { typeof(ProjectAPI.Domain.Construction.Entities.ProjectReadOnlyException), HandleProjectReadOnlyException },
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

    /// <summary>
    /// Filtered unique indexes that exist purely as concurrency backstops, and
    /// the typed 409 each one must produce. The SQL message is the only thing
    /// SqlException carries about which constraint fired, so the index NAME is
    /// part of the API contract: renaming one in a migration without updating
    /// this table turns a 409 back into a 500.
    /// </summary>
    private static readonly (string IndexName, string Code, string Detail)[] UniqueIndexConflicts =
    {
        (
            "IX_Reservations_ActivePerUnit",
            BusinessErrorCodes.UnitNotAvailable,
            "Le bien n'est plus disponible : une réservation active existe déjà."
        ),
        (
            "IX_Sales_ActivePerReservation",
            BusinessErrorCodes.SaleAlreadyExists,
            "Une vente active existe déjà pour cette réservation."
        ),
        (
            "IX_Sales_ActivePerUnit",
            BusinessErrorCodes.UnitNotAvailable,
            "Le bien n'est plus disponible : une vente active existe déjà."
        )
    };

    /// <summary>SQL Server error number for a FOREIGN KEY / REFERENCE constraint violation.</summary>
    private const int ForeignKeyViolationNumber = 547;

    private static bool IsForeignKeyViolation(Exception? exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql && sql.Number == ForeignKeyViolationNumber) return true;
        }
        return false;
    }

    /// <summary>
    /// SQL errors that say "the database could not be reached right now", not
    /// "the request is wrong": command timeout (-2), dropped/unusable connection
    /// (-1, 2, 53, 10053, 10054, 10060), Azure SQL throttling and failover
    /// (40197, 40501, 40613, 49918, 49919, 49920, 10928, 10929, 4060, 4221).
    /// </summary>
    private static readonly HashSet<int> TransientSqlNumbers = new()
    {
        -2, -1, 2, 53, 4060, 4221, 10053, 10054, 10060, 10928, 10929, 40197, 40501, 40613, 49918, 49919, 49920
    };

    private static bool IsTransientSqlFailure(Exception? exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql && TransientSqlNumbers.Contains(sql.Number)) return true;
        }
        return false;
    }

    private void HandleException(ExceptionContext context)
    {
        // A timeout or a dropped connection to the database is not a bug in the
        // request: answer 503 with a retryable code instead of a bare 500, so
        // the client can tell the user to try again.
        if (IsTransientSqlFailure(context.Exception))
        {
            var unavailable = new ProblemDetails
            {
                Title = "Service momentanément indisponible.",
                Detail = "La base de données ne répond pas pour le moment. Réessayez dans quelques instants.",
                Type = "https://docs.gpia.example/problems/service-unavailable"
            };
            Enrich(unavailable, context, BusinessErrorCodes.ServiceUnavailable, StatusCodes.Status503ServiceUnavailable);
            context.Result = new ObjectResult(unavailable) { StatusCode = StatusCodes.Status503ServiceUnavailable };
            context.HttpContext.Response.Headers["Retry-After"] = "5";
            _logger.LogWarning(context.Exception, "[Transient] SERVICE_UNAVAILABLE on {Path}", context.HttpContext.Request.Path);
            context.ExceptionHandled = true;
            return;
        }

        // Deleting (or re-pointing) a row other data still references used to
        // surface as a bare 500. It is a conflict the user can act on.
        if (IsForeignKeyViolation(context.Exception))
        {
            var inUse = new ProblemDetails
            {
                Title = "Règle métier non satisfaite.",
                Detail = "Cet élément est encore utilisé par d'autres données et ne peut pas être supprimé ou modifié ainsi.",
                Type = "https://docs.gpia.example/problems/resource-in-use"
            };
            Enrich(inUse, context, BusinessErrorCodes.ResourceInUse, StatusCodes.Status409Conflict);
            context.Result = new ObjectResult(inUse) { StatusCode = StatusCodes.Status409Conflict };
            _logger.LogInformation("[Conflict] RESOURCE_IN_USE on {Path}", context.HttpContext.Request.Path);
            context.ExceptionHandled = true;
            return;
        }

        // Translate database-level uniqueness failures before generic handling.
        if (TryGetUniqueViolation(context.Exception, out var message))
        {
            var match = UniqueIndexConflicts.FirstOrDefault(
                c => message?.Contains(c.IndexName, StringComparison.OrdinalIgnoreCase) == true);

            var code = match.Code ?? BusinessErrorCodes.ResourceVersionConflict;
            var detail = match.Detail ?? "Cette opération viole une contrainte d'unicité.";

            var conflict = new ProblemDetails
            {
                Title = "Règle métier non satisfaite.",
                Detail = detail,
                Type = $"https://docs.gpia.example/problems/{code.ToLowerInvariant().Replace('_', '-')}"
            };

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
    /// Maps a refused unit transition to 409 INVALID_STATUS_TRANSITION (§3, §7).
    /// </summary>
    private void HandleInvalidUnitTransitionException(ExceptionContext context)
    {
        var exception = (InvalidUnitTransitionException)context.Exception;

        var details = new ProblemDetails
        {
            Title = "Transition de statut non autorisée.",
            Detail = exception.Message,
            Type = "https://docs.gpia.example/problems/invalid-status-transition"
        };

        Enrich(details, context, BusinessErrorCodes.InvalidStatusTransition, StatusCodes.Status409Conflict);

        context.Result = new ObjectResult(details) { StatusCode = StatusCodes.Status409Conflict };

        _logger.LogInformation(
            "[UnitTransition] refused {From} -> {To} on {Path}",
            exception.From, exception.To, context.HttpContext.Request.Path);

        context.ExceptionHandled = true;
    }

    /// <summary>
    /// §31.6 — a real EF-level optimistic concurrency conflict (RowVersion
    /// mismatch on Reservation/PaymentSchedule/Snag), distinct from the
    /// SQL-2601 unique-index path above: this fires when the caller's If-Match
    /// / loaded version is genuinely stale, not from a double-submit racing a
    /// uniqueness constraint.
    /// </summary>
    private void HandleConcurrencyException(ExceptionContext context)
    {
        var details = new ProblemDetails
        {
            Title = "Conflit de version.",
            Detail = "La ressource a été modifiée entre-temps. Rechargez puis réessayez.",
            Type = "https://docs.gpia.example/problems/resource-version-conflict"
        };

        Enrich(details, context, BusinessErrorCodes.ResourceVersionConflict, StatusCodes.Status409Conflict);

        context.Result = new ObjectResult(details) { StatusCode = StatusCodes.Status409Conflict };
        _logger.LogInformation("[Concurrency] stale write on {Path}", context.HttpContext.Request.Path);
        context.ExceptionHandled = true;
    }

    /// <summary>Maps a write on a finalised project to 409 PROJECT_READ_ONLY.</summary>
    private void HandleProjectReadOnlyException(ExceptionContext context)
    {
        var details = new ProblemDetails
        {
            Title = "Projet en lecture seule.",
            Detail = context.Exception.Message,
            Type = "https://docs.gpia.example/problems/project-read-only"
        };
        Enrich(details, context, BusinessErrorCodes.ProjectReadOnly, StatusCodes.Status409Conflict);
        context.Result = new ObjectResult(details) { StatusCode = StatusCodes.Status409Conflict };
        _logger.LogInformation("[Conflict] PROJECT_READ_ONLY on {Path}", context.HttpContext.Request.Path);
        context.ExceptionHandled = true;
    }

    /// <summary>
    /// Maps a refused SAV claim transition to 409 INVALID_STATUS_TRANSITION (§20, §47.5).
    /// </summary>
    private void HandleInvalidClaimTransitionException(ExceptionContext context)
    {
        var exception = (InvalidClaimTransitionException)context.Exception;

        var details = new ProblemDetails
        {
            Title = "Transition de statut non autorisée.",
            Detail = exception.Message,
            Type = "https://docs.gpia.example/problems/invalid-status-transition"
        };

        Enrich(details, context, BusinessErrorCodes.InvalidStatusTransition, StatusCodes.Status409Conflict);

        context.Result = new ObjectResult(details) { StatusCode = StatusCodes.Status409Conflict };

        _logger.LogInformation(
            "[ClaimTransition] refused {From} -> {To} on {Path}",
            exception.From, exception.To, context.HttpContext.Request.Path);

        context.ExceptionHandled = true;
    }

    /// <summary>
    /// Maps a refused sale transition to 409 INVALID_STATUS_TRANSITION (§5.7, §6).
    /// </summary>
    private void HandleInvalidSaleTransitionException(ExceptionContext context)
    {
        var exception = (InvalidSaleTransitionException)context.Exception;

        var details = new ProblemDetails
        {
            Title = "Transition de statut non autorisée.",
            Detail = exception.Message,
            Type = "https://docs.gpia.example/problems/invalid-status-transition"
        };

        Enrich(details, context, BusinessErrorCodes.InvalidStatusTransition, StatusCodes.Status409Conflict);

        context.Result = new ObjectResult(details) { StatusCode = StatusCodes.Status409Conflict };

        _logger.LogInformation(
            "[SaleTransition] refused {From} -> {To} on {Path}",
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
