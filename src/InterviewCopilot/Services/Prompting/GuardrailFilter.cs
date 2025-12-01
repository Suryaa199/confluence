using System;
using System.Collections.Generic;
using System.Linq;

namespace InterviewCopilot.Services.Prompting;

public static class GuardrailFilter
{
    private static readonly string[] ForbiddenPhrases =
    {
        "hi there",
        "great to connect",
        "thanks for asking",
        "as an ai",
        "let me help",
        "your question seems unclear",
        "could you please clarify",
        "i can help"
    };

    public static string Apply(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var result = text;
        foreach (var phrase in ForbiddenPhrases)
        {
            result = result.Replace(phrase, string.Empty, StringComparison.OrdinalIgnoreCase);
        }
        return result.Trim();
    }
}
