using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace UniBank.Notifications.Services;

/// <summary>
/// Renders notification templates by substituting {placeholder} variables with actual values.
/// Unresolved placeholders are left as-is and logged as warnings.
/// </summary>
public sealed partial class TemplateEngine
{
    private readonly ILogger<TemplateEngine> _logger;

    public TemplateEngine(ILogger<TemplateEngine> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Renders a template string by replacing {variable} placeholders with values
    /// from the provided dictionary.
    /// </summary>
    /// <param name="template">Template string with {placeholder} syntax.</param>
    /// <param name="variables">Variable name-to-value mappings.</param>
    /// <returns>The rendered string. Unresolved placeholders remain as-is.</returns>
    public string Render(string template, IReadOnlyDictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        return VariablePattern().Replace(template, match =>
        {
            var key = match.Groups[1].Value;

            if (variables.TryGetValue(key, out var value))
            {
                return value;
            }

            _logger.LogWarning(
                "Template variable {Variable} not found in provided values; leaving placeholder as-is",
                key);

            return match.Value;
        });
    }

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex VariablePattern();
}
