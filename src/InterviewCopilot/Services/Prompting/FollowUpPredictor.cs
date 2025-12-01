using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace InterviewCopilot.Services.Prompting;

public static class FollowUpPredictor
{
    private static readonly (string[] Keys, string[] Suggestions)[] TopicMap = new[]
    {
        (new[] { "secure aks", "aks security", "kubernetes security", "hardening", "rbac", "aad", "pod security", "network policy" },
            new[]
            {
                "Deep-dive security controls?",
                "How do you secure container images before they hit AKS?",
                "What about Pod Security/Network Policies?"
            }),
        (new[] { "aks", "kubernetes", "ingress", "service mesh", "helm", "hpa", "kubectl" },
            new[]
            {
                "How do you debug failing pods or ingress routing?",
                "What’s your rollout/canary strategy?",
                "Failover / DR strategy?"
            }),
        (new[] { "ci/cd", "pipeline", "devops", "github actions", "azure devops", "workflow" },
            new[]
            {
                "CI/CD versioning approach?",
                "How are secrets handled across environments?",
                "What tests or gates block a bad deploy?"
            }),
        (new[] { "terraform", "iac", "infrastructure as code", "bicep", "pulumi" },
            new[]
            {
                "How do you structure reusable modules?",
                "What drift detection do you rely on?",
                "How do you manage state and approvals?"
            }),
        (new[] { "monitor", "observability", "prometheus", "grafana", "logs", "alerts", "slo", "mttr" },
            new[]
            {
                "Monitoring and alerting detail?",
                "Which SLO/SLA metrics do you track?",
                "How do you trace issues end-to-end?"
            }),
        (new[] { "cost", "optimization", "spot", "autoscale", "rightsizing" },
            new[]
            {
                "Cost optimization tactics?",
                "How do you forecast spend before changes?",
                "What knobs do you tune (nodes vs pods)?"
            })
    };

    private static readonly string[] GeneralFallback = new[]
    {
        "Deep-dive security controls?",
        "Failover / DR strategy?",
        "CI/CD versioning approach?",
        "Cost optimization tactics?",
        "Monitoring and alerting detail?"
    };

    private static readonly string StatsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "InterviewCopilot",
        "followups_stats.json");

    private static readonly Dictionary<string, int> Stats;
    private static readonly object StatsLock = new();

    static FollowUpPredictor()
    {
        Stats = LoadStats();
    }

    public static IReadOnlyList<string> Predict(string? question)
    {
        var lower = question?.ToLowerInvariant() ?? string.Empty;
        var list = new List<string>();

        foreach (var (keys, suggestions) in TopicMap)
        {
            if (!string.IsNullOrEmpty(lower) && keys.Any(lower.Contains))
            {
                foreach (var s in suggestions)
                {
                    if (!list.Contains(s)) list.Add(s);
                }
            }
        }

        foreach (var fallback in GeneralFallback)
        {
            if (list.Count >= 5) break;
            if (!list.Contains(fallback)) list.Add(fallback);
        }

        if (list.Count == 0) list.AddRange(GeneralFallback);

        return list
            .OrderByDescending(s => GetCount(s))
            .ThenBy(s => s, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
    }

    public static void RecordSelection(string suggestion)
    {
        if (string.IsNullOrWhiteSpace(suggestion)) return;
        lock (StatsLock)
        {
            Stats.TryGetValue(suggestion, out var count);
            Stats[suggestion] = count + 1;
            SaveStats();
        }
    }

    private static int GetCount(string suggestion)
    {
        if (string.IsNullOrWhiteSpace(suggestion)) return 0;
        lock (StatsLock)
        {
            return Stats.TryGetValue(suggestion, out var count) ? count : 0;
        }
    }

    private static Dictionary<string, int> LoadStats()
    {
        try
        {
            if (File.Exists(StatsPath))
            {
                var json = File.ReadAllText(StatsPath);
                var data = JsonSerializer.Deserialize<Dictionary<string, int>>(json);
                if (data is not null) return data;
            }
        }
        catch { }
        return new(StringComparer.OrdinalIgnoreCase);
    }

    private static void SaveStats()
    {
        try
        {
            var dir = Path.GetDirectoryName(StatsPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }
            var json = JsonSerializer.Serialize(Stats, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(StatsPath, json);
        }
        catch { }
    }
}
