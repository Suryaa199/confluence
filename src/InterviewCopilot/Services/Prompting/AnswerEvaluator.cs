using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace InterviewCopilot.Services.Prompting;

public static class AnswerEvaluator
{
    private static readonly Regex BulletRegex = new(@"^\s*\d+\.\s+(.+)$", RegexOptions.Compiled);
    private static readonly string[] Forbidden = { "unclear", "seems", "usually", "maybe", "i can help" };

    public static bool NeedsRetry(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer)) return true;
        var lines = answer.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .ToArray();

        var bullets = lines.Where(l => BulletRegex.IsMatch(l)).Take(3).ToArray();
        if (bullets.Length != 3) return true;
        if (bullets.Any(b => WordCount(b) > 12)) return true;
        if (!lines.Any(l => l.StartsWith("CLI:", StringComparison.OrdinalIgnoreCase))) return true;
        if (!lines.Any(l => l.StartsWith("Mini", StringComparison.OrdinalIgnoreCase))) return true;
        var lower = answer.ToLowerInvariant();
        if (Forbidden.Any(f => lower.Contains(f))) return true;
        return false;
    }

    private static int WordCount(string text)
    {
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
