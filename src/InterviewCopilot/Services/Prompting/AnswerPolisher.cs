using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace InterviewCopilot.Services.Prompting;

public static class AnswerPolisher
{
    private static readonly Regex BulletRegex = new(@"^\s*(\d+\.)(.+)$", RegexOptions.Compiled);
    private static readonly string[] Filler = { "maybe", "usually", "generally", "i can", "i'm able to" };

    public static string Polish(string? answer)
    {
        if (string.IsNullOrWhiteSpace(answer)) return string.Empty;
        var lines = answer.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .ToList();

        var bullets = new List<string>();
        string? cli = null;
        string? mini = null;
        var extras = new List<string>();

        foreach (var line in lines)
        {
            var match = BulletRegex.Match(line);
            if (match.Success)
            {
                var content = match.Groups[2].Value.Trim();
                bullets.Add(NormalizeBullet(content));
                continue;
            }
            if (line.StartsWith("CLI", StringComparison.OrdinalIgnoreCase))
            {
                cli ??= NormalizeCli(line);
                continue;
            }
            if (line.StartsWith("Mini", StringComparison.OrdinalIgnoreCase))
            {
                mini ??= NormalizeMini(line);
                continue;
            }
            extras.Add(line);
        }

        bullets = bullets
            .Where(b => !string.IsNullOrWhiteSpace(b))
            .Select(TrimWords)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        if (bullets.Count < 3)
        {
            bullets.AddRange(Enumerable.Repeat(string.Empty, 3 - bullets.Count));
        }

        cli ??= "CLI: n/a";
        mini ??= "Mini Example: Impact delivered.";

        var sb = new StringBuilder();
        for (int i = 0; i < 3; i++)
        {
            var text = string.IsNullOrWhiteSpace(bullets[i]) ? "TBD." : bullets[i];
            sb.AppendLine($"{i + 1}. {text}");
        }
        sb.AppendLine(cli);
        sb.AppendLine(mini);
        foreach (var extra in extras)
        {
            sb.AppendLine(extra);
        }
        return sb.ToString().TrimEnd();
    }

    private static string NormalizeBullet(string text)
    {
        var cleaned = RemoveFiller(text);
        return cleaned.Trim();
    }

    private static string NormalizeCli(string line)
    {
        var parts = line.Split(new[] { ':' }, 2);
        var payload = parts.Length == 2 ? parts[1].Trim() : line.Trim();
        if (payload.Contains('\n'))
        {
            payload = payload.Split('\n')[0].Trim();
        }
        if (string.IsNullOrWhiteSpace(payload)) payload = "n/a";
        return $"CLI: {payload}";
    }

    private static string NormalizeMini(string line)
    {
        var parts = line.Split(new[] { ':' }, 2);
        var payload = parts.Length == 2 ? parts[1].Trim() : line.Trim();
        payload = TrimWords(payload, maxWords: 12);
        if (string.IsNullOrWhiteSpace(payload)) payload = "Impact achieved.";
        return $"Mini Example: {payload}";
    }

    private static string TrimWords(string text, int maxWords = 12)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= maxWords) return text.Trim();
        return string.Join(' ', words.AsSpan(0, maxWords).ToArray()).Trim();
    }

    private static string RemoveFiller(string text)
    {
        var lower = text.ToLowerInvariant();
        foreach (var filler in Filler)
        {
            if (lower.StartsWith(filler))
            {
                return text.Substring(filler.Length).TrimStart();
            }
        }
        return text;
    }
}
