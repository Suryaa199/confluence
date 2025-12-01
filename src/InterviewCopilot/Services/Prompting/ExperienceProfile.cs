using System;
using System.Collections.Generic;
using System.Linq;

namespace InterviewCopilot.Services.Prompting;

internal static class ExperienceProfile
{
    private static readonly string Summary =
        "6+ years DevOps/DevSecOps at HCL (AI Force) driving AKS, Terraform, CI/CD, security automation.";

    private static readonly (string Match, string Snippet)[] Snippets =
    {
        ("aks", "At AI Force I run multi-tenant AKS with Keycloak SSO, NGINX ingress, and ACR-signed images."),
        ("terraform", "Shipped reusable Terraform modules (network, AKS, Key Vault) with GitHub Actions drift checks."),
        ("security", "Implemented Trivy + OPA gates, private AKS API, and Key Vault rotation to cut high-risk deploys 40%."),
        ("ci/cd", "Built GitHub Actions pipelines with staged approvals, secrets from Key Vault, and automated rollbacks."),
        ("cost", "Optimized AKS spend 30% via spot node pools, VPA, and autoscaling policies."),
        ("genai", "Integrated OpenAI + local LLMs into AI Force, handling secure audio capture and prompt orchestration."),
    };

    public static string GetSummary() => Summary;

    public static string? GetSnippet(string question)
    {
        if (string.IsNullOrWhiteSpace(question)) return null;
        var lower = question.ToLowerInvariant();
        foreach (var (match, snippet) in Snippets)
        {
            if (lower.Contains(match)) return snippet;
        }
        return Snippets.First().Snippet;
    }
}
