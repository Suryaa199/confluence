using System;
using System.Collections.Generic;
using System.Linq;

namespace InterviewCopilot.Services.Prompting;

internal sealed record KnowledgePack(string Name, string[] Keywords, string[] Facts);

internal static class KnowledgePackLibrary
{
    private static readonly KnowledgePack[] Packs = new[]
    {
        new KnowledgePack("AKS", new[] { "aks", "kubernetes", "ingress", "pod" }, new[]
        {
            "AKS best practice: private API, AAD RBAC, Azure Policy/Defender integration.",
            "Use Managed Identities + Key Vault CSI for secrets; enable NetworkPolicies (Calico).",
            "Autoscaling: mix spot + regular node pools, HPA/VPA based on workload type."
        }),
        new KnowledgePack("Terraform", new[] { "terraform", "iac", "bicep" }, new[]
        {
            "Structure modules per layer (network, security, platform) with versioned registries.",
            "Run plan/apply via GitHub Actions + remote state (Azure Storage) with locks.",
            "Use Sentinel/OPA policies to block risky tfvars before apply."
        }),
        new KnowledgePack("CI/CD", new[] { "ci", "cd", "pipeline", "github actions", "devops" }, new[]
        {
            "Implement multi-stage pipelines: lint, scan, unit, integration, deploy.",
            "Inject Trivy/Snyk scans and OPA checks before AKS rollout.",
            "Use deployment rings (dev/test/prod) with automatic rollback triggers."
        }),
        new KnowledgePack("Security", new[] { "secure", "security", "rbac", "policy", "key vault" }, new[]
        {
            "Layered controls: identity (AAD), network (NSG/NP), scan (Trivy), monitor (Defender).",
            "Rotate secrets via Key Vault + Managed Identity; avoid static creds.",
            "Audit with Azure Monitor + Log Analytics, feed into Sentinel." 
        }),
        new KnowledgePack("Observability", new[] { "monitor", "prometheus", "grafana", "alert", "slo" }, new[]
        {
            "Prometheus/Grafana for pod metrics, Azure Monitor for control plane signals.",
            "Define SLOs (latency, availability) and alert thresholds per service tier.",
            "Trace flows with OpenTelemetry, feed to Azure Monitor + Jaeger."
        })
    };

    public static IReadOnlyList<KnowledgePack> Match(string question, QuestionCategory category, int max = 2)
    {
        if (string.IsNullOrWhiteSpace(question)) return Array.Empty<KnowledgePack>();
        var lower = question.ToLowerInvariant();
        var list = new List<KnowledgePack>();
        foreach (var pack in Packs)
        {
            if (pack.Keywords.Any(k => lower.Contains(k)))
            {
                list.Add(pack);
            }
        }
        if (category == QuestionCategory.Security)
        {
            AddPack(list, "Security");
        }
        else if (category == QuestionCategory.Architecture)
        {
            AddPack(list, "AKS");
        }
        return list
            .Distinct()
            .Take(max)
            .ToList();
    }

    private static void AddPack(List<KnowledgePack> list, string name)
    {
        var pack = Packs.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        if (pack is not null) list.Add(pack);
    }
}
