using Dsw2026Tpi.CrossCutting.Models;
using Dsw2026Tpi.CrossCutting.Resources;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Dsw2026Tpi.Api.Configurations;

/// <summary>
/// Traduce los errores de model binding al contrato de errores de la API: campo en camelCase e issue en español.
/// </summary>
public static class ModelStateErrors
{
    public static ErrorResponse From(ModelStateDictionary modelState)
    {
        var error = new ErrorResponse(nameof(ErrorCodes.VALIDATION_ERROR), ErrorCodes.VALIDATION_ERROR);

        foreach (var (key, entry) in modelState)
        {
            if (entry.Errors.Count == 0)
                continue;

            var field = ToFieldName(key);

            foreach (var modelError in entry.Errors)
                error.AddDetail(field, Translate(modelError));
        }

        return error;
    }

    private static string ToFieldName(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key == "$")
            return "request";

        var parts = key.TrimStart('$', '.').Split('.', StringSplitOptions.RemoveEmptyEntries);

        return string.Join('.', parts.Select(part => char.ToLowerInvariant(part[0]) + part[1..]));
    }

    private static string Translate(ModelError modelError)
    {
        var message = modelError.ErrorMessage ?? string.Empty;

        if (message.EndsWith("field is required.", StringComparison.OrdinalIgnoreCase))
            return "es obligatorio";

        if (message.Contains("could not be converted", StringComparison.OrdinalIgnoreCase)
            || message.Contains("is not valid for", StringComparison.OrdinalIgnoreCase))
            return "formato inválido";

        return "valor inválido";
    }
}
