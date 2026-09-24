using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ProjectAPI.Api.Application.Common.Exceptions;

namespace ProjectAPI.Api.Filters;

/// <summary>
/// Shapes the framework's automatic model-binding 400 like every other API error
/// (RFC 9457 + <c>code</c> + <c>requestId</c>, French messages) and keeps
/// internal type names out of the response. Left alone, a malformed body came
/// back as "One or more validation errors occurred." with messages such as
/// "JSON deserialization for type 'ProjectAPI.Api.Application...LoginCommand'…".
/// The status stays 400 (unchanged contract); only the body improves.
/// </summary>
public static class ModelStateProblemFactory
{
    private static readonly Regex NotValid = new(@"^The value '(?<v>.*)' is not valid for (?<f>.+)\.$", RegexOptions.Compiled);
    private static readonly Regex Required = new(@"^The (?<f>.+) field is required\.$", RegexOptions.Compiled);

    public static IActionResult Create(ActionContext context)
    {
        var errors = new Dictionary<string, string[]>();
        foreach (var (rawKey, entry) in context.ModelState)
        {
            if (entry.Errors.Count == 0) continue;

            var messages = entry.Errors.Select(e => Translate(e.ErrorMessage, rawKey)).Distinct().ToArray();

            // "command"/"request" only says the whole body failed to bind; the
            // other entries already carry the reason.
            if (rawKey is "command" or "request" && messages.All(m => m.EndsWith("est obligatoire.", StringComparison.Ordinal)))
                continue;

            var key = rawKey switch
            {
                "$" or "" or "command" or "request" => "body",
                _ when rawKey.StartsWith("$.", StringComparison.Ordinal) => rawKey[2..],
                _ => rawKey
            };
            errors[key] = errors.TryGetValue(key, out var existing) ? existing.Concat(messages).Distinct().ToArray() : messages;
        }

        if (errors.Count == 0) errors["body"] = new[] { "Le corps de la requête est invalide ou incomplet." };

        var details = new ValidationProblemDetails(errors)
        {
            Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title = "Données invalides.",
            Status = StatusCodes.Status400BadRequest,
            Instance = context.HttpContext.Request.Path
        };
        details.Extensions["code"] = BusinessErrorCodes.ValidationFailed;
        details.Extensions["requestId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

        return new ObjectResult(details) { StatusCode = StatusCodes.Status400BadRequest };
    }

    private static string Translate(string message, string key)
    {
        // Deserialiser messages name CLR types and byte positions: never show them.
        if (message.Contains("JSON", StringComparison.Ordinal) || message.Contains("deserialization", StringComparison.OrdinalIgnoreCase))
            return key.StartsWith("$.", StringComparison.Ordinal)
                ? "Valeur invalide ou hors limites."
                : "Le corps de la requête est invalide ou incomplet.";
        if (message.StartsWith("'", StringComparison.Ordinal) && message.Contains("invalid start", StringComparison.Ordinal))
            return "Le corps de la requête est invalide ou incomplet.";

        var notValid = NotValid.Match(message);
        if (notValid.Success) return $"La valeur « {notValid.Groups["v"].Value} » n'est pas valide pour {notValid.Groups["f"].Value}.";
        var required = Required.Match(message);
        if (required.Success) return $"Le champ {required.Groups["f"].Value} est obligatoire.";
        return message;
    }
}
