using System.Text.RegularExpressions;
using Kestrel.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Kestrel.Core.Guardrails;

public sealed partial class InputGuard(IOptions<RagOptions> options)
{
    private static readonly string[] InjectionPatterns =
    [
        "ignore previous instructions",
        "ignore all instructions",
        "system prompt",
        "developer message",
        "you are now",
        "jailbreak",
        "disregard your rules"
    ];

    public string SanitizeUserInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new ArgumentException("Message cannot be empty.");
        }

        var trimmed = input.Trim();
        if (trimmed.Length > options.Value.MaxInputChars)
        {
            trimmed = trimmed[..options.Value.MaxInputChars];
        }

        var lowered = trimmed.ToLowerInvariant();
        if (InjectionPatterns.Any(p => lowered.Contains(p, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Request blocked by safety filter.");
        }

        return trimmed;
    }

    public static string SanitizeRetrievedText(string text)
    {
        // Neutralize common instruction-like phrases in retrieved content.
        return InstructionLike().Replace(text, "[filtered]");
    }

    public static bool IsSafetyDocument(string docType)
        => string.Equals(docType, "safety", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"ignore (all|previous) instructions|system prompt|jailbreak", RegexOptions.IgnoreCase)]
    private static partial Regex InstructionLike();
}
