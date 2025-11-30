using System.Text;

namespace InterviewCopilot.Services.Prompting;

public static class ScenarioLibrary
{
    private static readonly ScenarioEntry DefaultEntry = new(
        "DevSecOps",
        "Optimized AI Force AKS platform (reduced image size 15GB→4GB, startup latency -40%, enforced Trivy gates).",
        "az aks get-credentials -g ai-rg -n aiforce-aks | kubectl rollout restart deployment aiforce-api");

    private static readonly (string[] keywords, ScenarioEntry entry)[] Entries = new[]
    {
        (new[] { "git", "branch", "merge", "repo" },
            new ScenarioEntry("Git",
                "Hardened feature flow using GitHub Actions, enforced PR checks, cleaned branches nightly.",
                "git fetch origin && git merge origin/main | git log --oneline -5")),
        (new[] { "aks", "kubernetes", "cluster", "pod", "helm" },
            new ScenarioEntry("AKS",
                "Managed AKS multi-tenant clusters with Keycloak SSO, HPA tuning, and canary rollouts.",
                "kubectl describe pod aiforce-api | helm upgrade aiforce charts/api -n prod")),
        (new[] { "terraform", "iac", "infrastructure" },
            new ScenarioEntry("Terraform",
                "Provisioned AKS + VNets + Key Vault via reusable Terraform modules with policy gates.",
                "terraform plan -var-file prod.tfvars | terraform apply -auto-approve")),
        (new[] { "security", "devsecops", "scan", "cve" },
            new ScenarioEntry("Security",
                "Blocked OpenSSL CVE via Trivy + ACR Tasks, piped findings into Azure DevOps approvals.",
                "trivy image aifcontainer.azurecr.io/aiforce-api:v2 | az acr task run --name trivyGate")),
        (new[] { "python", "automation", "script" },
            new ScenarioEntry("Automation",
                "Built Python ops toolkit triggering Azure DevOps releases + Key Vault rotation flows.",
                "python scripts/rotate_secrets.py --env prod | az pipelines run aiforce-release")),
        (new[] { "keycloak", "auth", "sso" },
            new ScenarioEntry("Keycloak",
                "Integrated Keycloak with Azure AD, mapped RBAC to AKS namespaces, automated realm backup.",
                "kubectl edit configmap keycloak-config -n platform | kcadmin export --dir backups/")),
        (new[] { "nginx", "ingress", "traffic" },
            new ScenarioEntry("Ingress",
                "Tuned NGINX ingress with mTLS + WAF rules to protect AI Force APIs and websocket streams.",
                "kubectl describe ingress aiforce-api | nginx -t")),
        (new[] { "openai", "llm", "gpt" },
            new ScenarioEntry("OpenAI",
                "Embedded GPT-4o mini streaming answers into AI Force, added offline Whisper spool.",
                "curl https://api.openai.com/v1/chat/completions -d '{...}' | python scripts/offline_spool.py"))
    };

    public static ScenarioEntry MatchScenario(string question, IReadOnlyList<ContextSnippet> snippets)
    {
        var lower = question?.ToLowerInvariant() ?? string.Empty;
        foreach (var (keys, entry) in Entries)
        {
            if (keys.Any(k => lower.Contains(k)))
            {
                return entry;
            }
        }

        if (snippets != null)
        {
            foreach (var snippet in snippets)
            {
                var snLower = snippet.Text.ToLowerInvariant();
                foreach (var (keys, entry) in Entries)
                {
                    if (keys.Any(snLower.Contains))
                        return entry;
                }
            }
        }

        return DefaultEntry;
    }
}

public readonly record struct ScenarioEntry(string Topic, string Example, string CliExamples)
{
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append(Topic);
        sb.Append(": ");
        sb.Append(Example);
        return sb.ToString();
    }
}
