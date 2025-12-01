using InterviewCopilot.Models;
using System.Text;

namespace InterviewCopilot.Services.Prompting;

public sealed class AnswerPromptBuilder
{
    private readonly Settings _settings;
    private readonly QuestionClassifier _classifier = new();
    private readonly ContextRetriever _contextRetriever;
    private readonly ConversationState _state;
    private const string FewShotExamples = """
Example 1:
Q: How do you secure AKS clusters?
A:
1. Enforce AAD RBAC so clusters stay least-privilege.
2. Apply NetworkPolicies + Pod Standards with Trivy gates.
3. Monitor privileged pods via Azure Monitor alerts.
CLI: az aks update --enable-aad --enable-pod-security-policy
Mini Example: Blocked 40% risky deploys via admission policies.

Example 2:
Q: Troubleshoot AKS latency spikes.
A:
1. Check kubectl top + Azure Monitor for saturation.
2. Inspect ingress + HPA events for noisy neighbors.
3. Roll canary or scale pods, confirm p95 drop.
CLI: kubectl describe hpa api -n prod
Mini Example: Reduced p95 420 ms→180 ms using tuned rollout.
""";

    public AnswerPromptBuilder(Settings settings, ConversationState state)
    {
        _settings = settings;
        _state = state;
        _contextRetriever = new ContextRetriever(settings);
    }

    public LlmPrompt Build(string question, bool consumeCue = true)
    {
        var profile = _classifier.Classify(question);
        var (maxSnippets, minScore) = profile switch
        {
            QuestionType.Definition => (2, 0.08),
            QuestionType.Command => (2, 0.08),
            _ => (4, 0.02)
        };
        var snippets = _contextRetriever.GetTopSnippets(question, maxSnippets, minScore);
        var scenario = ScenarioLibrary.MatchScenario(question, snippets);

        var context = ComposeContext(question, snippets, scenario, profile, consumeCue);
        var systemInstruction = PromptTemplates.GetSystemInstruction(profile, _state.Tone);
        if (!string.IsNullOrWhiteSpace(scenario.Topic))
        {
            systemInstruction += $" Highlight tooling around {scenario.Topic} when helpful.";
        }

        return new LlmPrompt(question, context, systemInstruction);
    }

    private string ComposeContext(string question, IReadOnlyList<ContextSnippet> snippets, ScenarioEntry scenario, QuestionType type, bool consumeCue)
    {
        var sb = new StringBuilder();
        var skills = BuildSkillSignals(type, scenario, snippets);
        sb.AppendLine("Context Skills: " + skills);

        var cue = consumeCue ? _state.ConsumeLiveCue() : _state.PeekLiveCue();
        if (!string.IsNullOrWhiteSpace(cue))
        {
            sb.AppendLine("Follow-up requested: " + TrimField(cue, 120));
        }

        sb.AppendLine();
        sb.AppendLine("Interview Question: \"" + question.Trim() + "\"");
        sb.AppendLine();
        sb.AppendLine("Give 3 bullets + CLI + Mini Example.");
        sb.AppendLine();
        sb.AppendLine("Reference Format Samples:");
        sb.AppendLine(FewShotExamples);

        return sb.ToString();
    }

    private string BuildSkillSignals(QuestionType type, ScenarioEntry scenario, IReadOnlyList<ContextSnippet> snippets)
    {
        var pool = new List<string>
        {
            "AKS",
            "Terraform",
            "Docker",
            "GitHub Actions",
            "Trivy",
            "Key Vault",
            "Helm",
            "ArgoCD",
            "Prometheus",
            "Grafana",
            "Security",
            "CI/CD"
        };

        if (!string.IsNullOrWhiteSpace(scenario.Topic))
        {
            pool.Add(scenario.Topic);
        }

        if (_settings.Keywords is { Length: > 0 })
        {
            pool.AddRange(_settings.Keywords);
        }

        var extra = snippets
            .SelectMany(sn => sn.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(word => word.Length <= 18 && char.IsLetter(word[0]))
            .Select(word => word.Trim(',', '.', ';', ':').Trim())
            .Where(word => word.Length > 2)
            .Take(10);
        pool.AddRange(extra);

        if (type == QuestionType.Security)
        {
            pool.AddRange(new[] { "RBAC", "NetworkPolicies", "Trivy", "Defender", "Key Vault" });
        }
        else if (type == QuestionType.Troubleshooting || type == QuestionType.Challenge)
        {
            pool.AddRange(new[] { "kubectl", "Azure Monitor", "HPA", "logs", "alerts" });
        }

        return string.Join(", ",
            pool
                .Select(p => p?.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10));
    }

    private static string TrimField(string? text, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var normalized = text.Replace("\r", " ").Replace("\n", " ").Trim();
        if (normalized.Length <= maxChars) return normalized;
        return normalized[..maxChars] + "…";
    }
}
