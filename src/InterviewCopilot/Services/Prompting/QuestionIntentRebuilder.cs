using System;
using System.Collections.Generic;
using System.Linq;

namespace InterviewCopilot.Services.Prompting;

public static class QuestionIntentRebuilder
{
    private static readonly (string[] Keys, string Intent)[] Map = new[]
    {
        (new[] { "aks", "sec" }, "How do you secure AKS clusters?"),
        (new[] { "aks", "ha", "latency" }, "How do you troubleshoot AKS latency spikes?"),
        (new[] { "aks", "ingress" }, "How does AKS ingress/traffic management work?"),
        (new[] { "ci", "cd", "pipeline" }, "How do you design secure CI/CD pipelines?"),
        (new[] { "terraform", "iac" }, "How do you structure Terraform infrastructure as code?"),
        (new[] { "monitor", "observ", "prometheus" }, "How do you monitor and alert on AKS workloads?"),
        (new[] { "cost", "optimize", "spot" }, "How do you optimize cloud costs for AKS workloads?"),
        (new[] { "dr", "failover" }, "How do you design AKS DR/failover?"),
        (new[] { "container", "image", "scan" }, "How do you secure container images before deployment?"),
        (new[] { "lead", "team", "manage" }, "Share a leadership example driving a DevOps initiative.")
    };

    private static readonly string LogPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "InterviewCopilot",
        "intent_mappings.jsonl");

    public static string Rebuild(string question)
    {
        if (string.IsNullOrWhiteSpace(question)) return question;
        var trimmed = question.Trim();
        var normalized = trimmed.ToLowerInvariant();
        var tokenized = Tokenize(normalized);

        foreach (var (keys, intent) in Map)
        {
            if (keys.All(k => tokenized.Any(t => t.Contains(k, StringComparison.OrdinalIgnoreCase))))
            {
                LogMapping(trimmed, intent);
                return intent;
            }
        }

        if (trimmed.Length < 15)
        {
            if (tokenized.Any(t => t.Contains("aks")))
            {
                var fallback = "How do you secure AKS clusters?";
                LogMapping(trimmed, fallback);
                return fallback;
            }
            if (tokenized.Any(t => t.Contains("terraform") || t.Contains("iac")))
            {
                var fallback = "How do you structure Terraform infrastructure as code?";
                LogMapping(trimmed, fallback);
                return fallback;
            }
        }

        return trimmed;
    }

    private static IReadOnlyList<string> Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<string>();
        return text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static void LogMapping(string original, string rewritten)
    {
        try
        {
            if (string.Equals(original, rewritten, StringComparison.Ordinal)) return;
            var dir = System.IO.Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                at = DateTimeOffset.UtcNow,
                original,
                rewritten
            });
            System.IO.File.AppendAllText(LogPath, json + Environment.NewLine);
        }
        catch { }
    }
}
