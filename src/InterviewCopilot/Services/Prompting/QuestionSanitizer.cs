using System;
using System.Linq;
using System.Text;

namespace InterviewCopilot.Services.Prompting;

public static class QuestionSanitizer
{
    public static string Sanitize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if (char.IsControl(ch) && !char.IsWhiteSpace(ch)) continue;
            sb.Append(ch);
        }
        var collapsed = string.Join(' ',
            sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (collapsed.Length > 0 && !collapsed.EndsWith("?", StringComparison.Ordinal))
        {
            collapsed += "?";
        }
        return collapsed.Trim();
    }
}
