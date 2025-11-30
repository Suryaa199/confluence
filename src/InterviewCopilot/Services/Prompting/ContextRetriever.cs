using InterviewCopilot.Models;
using System.IO;
using System.Linq;
using System.Text;

namespace InterviewCopilot.Services.Prompting;

public sealed class ContextRetriever
{
    private readonly List<ContextSnippet> _snippets = new();
    private readonly HashSet<string> _stopWords = new(new[] { "the", "a", "to", "and", "of", "in", "on", "for", "with", "what", "how", "do", "you", "is" });

    public string DefaultKeywords { get; }

    public ContextRetriever(Settings settings)
    {
        DefaultKeywords = string.Join(", ", new[]
        {
            "Azure",
            "AKS",
            "Kubernetes",
            "Terraform",
            "DevOps",
            "DevSecOps",
            "CI/CD",
            "Docker",
            "Keycloak",
            "NGINX",
            "Azure Container Registry (ACR)",
            "Python automation",
            "OpenAI",
            "Generative AI",
            "Confluence connectors"
        });

        AddSnippets(settings.ResumeText, "Resume");
        AddSnippets(settings.JobDescText, "JobDescription");
        AddSnippets(settings.CheatSheet, "CheatSheet");
        if (settings.Keywords is { Length: > 0 })
        {
            _snippets.Add(new ContextSnippet("Keywords", string.Join(", ", settings.Keywords)));
        }
        if (!string.IsNullOrWhiteSpace(settings.CompanyBlurb))
        {
            _snippets.Add(new ContextSnippet("Company", settings.CompanyBlurb));
        }
    }

    private void AddSnippets(string? source, string label)
    {
        if (string.IsNullOrWhiteSpace(source)) return;
        foreach (var chunk in SplitSegments(source))
        {
            _snippets.Add(new ContextSnippet(label, chunk));
        }
    }

    private static IEnumerable<string> SplitSegments(string text)
    {
        var sb = new StringBuilder();
        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (sb.Length > 0)
                {
                    yield return sb.ToString().Trim();
                    sb.Clear();
                }
                continue;
            }
            sb.Append(line.Trim());
            sb.Append(' ');
        }
        if (sb.Length > 0) yield return sb.ToString().Trim();
    }

    public IReadOnlyList<ContextSnippet> GetTopSnippets(string question, int max = 4, double minScore = 0.02)
    {
        if (_snippets.Count == 0) return Array.Empty<ContextSnippet>();
        var qTokens = Tokenize(question);
        var ranked = _snippets
            .Select(sn => new { Snippet = sn, Score = ScoreSnippet(sn.Text, qTokens) })
            .Where(x => x.Score >= minScore)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Snippet.Source)
            .Take(max)
            .Select(x => x.Snippet)
            .ToList();
        return ranked;
    }

    private double ScoreSnippet(string text, HashSet<string> questionTokens)
    {
        if (questionTokens.Count == 0) return 0;
        var tokens = Tokenize(text);
        if (tokens.Count == 0) return 0;
        var overlap = tokens.Count(questionTokens.Contains);
        return (double)overlap / Math.Max(tokens.Count, 1);
    }

    private HashSet<string> Tokenize(string? value)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value)) return set;
        foreach (var part in value.Split(new[] { ' ', ',', '.', ';', ':', '/', '\\', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var token = part.Trim().ToLowerInvariant();
            if (token.Length <= 2 || _stopWords.Contains(token)) continue;
            set.Add(token);
        }
        return set;
    }
}

public readonly record struct ContextSnippet(string Source, string Text);
