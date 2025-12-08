using System.Text;

namespace InterviewCopilot.Services.Prompting;

public static class ScenarioLibrary
{
    private static readonly ScenarioEntry DefaultEntry = new(
        string.Empty,
        string.Empty,
        string.Empty);

    private static readonly (string[] keywords, ScenarioEntry entry)[] Entries = new[]
    {
        (new[] { "git", "branch", "merge", "repo" },
            new ScenarioEntry("Git",
                "Hardened feature flow using GitHub Actions, enforced PR checks, cleaned branches nightly.",
                "git fetch origin\ngit merge origin/main\ngit log --oneline -5")),
        (new[] { "aks", "kubernetes", "cluster", "pod", "helm" },
            new ScenarioEntry("AKS",
                "Managed AKS multi-tenant clusters with Keycloak SSO, HPA tuning, and canary rollouts.",
                "kubectl describe pod aiforce-api\nkubectl top pod\ntilt up")),
        (new[] { "terraform", "iac", "infrastructure" },
            new ScenarioEntry("Terraform",
                "Provisioned AKS + VNets + Key Vault via reusable Terraform modules with policy gates.",
                "terraform init\nterraform plan -var-file prod.tfvars\nterraform apply -auto-approve")),
        (new[] { "security", "devsecops", "scan", "cve" },
            new ScenarioEntry("Security",
                "Blocked OpenSSL CVE via Trivy + ACR Tasks, piped findings into Azure DevOps approvals.",
                "trivy image aifcontainer.azurecr.io/aiforce-api:v2\naz acr task run --name trivyGate")),
        (new[] { "python", "automation", "script" },
            new ScenarioEntry("Automation",
                "Built Python ops toolkit triggering Azure DevOps releases + Key Vault rotation flows.",
                "python scripts/rotate_secrets.py --env prod\naz pipelines run aiforce-release")),
        (new[] { "keycloak", "auth", "sso" },
            new ScenarioEntry("Keycloak",
                "Integrated Keycloak with Azure AD, mapped RBAC to AKS namespaces, automated realm backup.",
                "kubectl edit configmap keycloak-config -n platform\nkcadmin export --dir backups/")),
        (new[] { "nginx", "ingress", "traffic" },
            new ScenarioEntry("Ingress",
                "Tuned NGINX ingress with mTLS + WAF rules to protect AI Force APIs and websocket streams.",
                "kubectl describe ingress aiforce-api\nnginx -t")),
        (new[] { "openai", "llm", "gpt" },
            new ScenarioEntry("OpenAI",
                "Embedded GPT-4o mini streaming answers into AI Force, added offline Whisper spool.",
                "curl https://api.openai.com/v1/chat/completions -d '{...}'\npython scripts/offline_spool.py")),
        (new[] { "gitops", "argocd", "flux" },
            new ScenarioEntry("GitOps",
                "Implemented GitOps with ArgoCD, managing AKS releases via declarative manifests and automated rollbacks.",
                "argocd app sync ai-force\nkubectl get applications.argoproj.io")),
        (new[] { "helm", "chart" },
            new ScenarioEntry("Helm",
                "Refactored Helm charts into modular templates, enabling blue/green rollout parameters per tenant.",
                "helm template charts/api | helm upgrade aiforce charts/api -n prod")),
        (new[] { "monitor", "prometheus", "grafana", "azure monitor" },
            new ScenarioEntry("Observability",
                "Built Prometheus + Azure Monitor dashboards for pod latency and queue depth with alert rules.",
                "kubectl logs -n monitoring prometheus-0\naz monitor metrics list --resource aiforce-aks")),
        (new[] { "service mesh", "istio", "linkerd" },
            new ScenarioEntry("ServiceMesh",
                "Piloted Istio for mTLS and traffic shifting between API versions, integrating with Keycloak JWT policies.",
                "istioctl proxy-status\nkubectl apply -f virtualservice-canary.yaml")),
        (new[] { "cost", "optimize", "scaling" },
            new ScenarioEntry("CostOptimization",
                "Rightsized AKS node pools and enabled VPA/spot nodes, cutting monthly spend 28%.",
                "az aks nodepool scale --name spot --node-count 2\nkubectl describe vpa api-vpa")),
        (new[] { "azure devops", "pipeline", "github actions" },
            new ScenarioEntry("CI/CD",
                "Standardized GitHub Actions pipelines with reusable workflows, secrets in Key Vault, and Trivy gates.",
                "gh workflow run deploy.yml\naz keyvault secret show --name api-token --vault-name ai-kv"))
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
