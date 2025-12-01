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
Q: How do you secure AKS clusters for production?
A:
1. I wire RBAC to Azure AD groups so every action is least-privilege.
2. I enforce NetworkPolicies + Trivy scanning + Pod Security to stop drift before deploys.
3. I keep the API private, rotate Key Vault secrets, and alert on suspicious pods.
Mini Example: Blocked OpenSSL CVE in 40 min by rebuilding images and rotating creds.
CLI Example:
kubectl get clusterrolebindings
trivy image aifregistry.azurecr.io/api:latest

Example 2:
Q: Troubleshoot high latency on an AKS service.
A:
1. I check `kubectl top` + Azure Monitor to see if nodes or pods are saturated.
2. I inspect ingress logs/HPA events to catch noisy neighbors or throttled replicas.
3. I roll out a tuned deployment (canary or scale-up) and watch Prometheus p95 recover.
Mini Example: Cut API latency from 420 ms→180 ms by right-sizing the canary pool.
CLI Example:
kubectl top pod -n prod
kubectl describe hpa api -n prod
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

        var context = ComposeContext(snippets, scenario, profile, consumeCue);
        var systemInstruction = PromptTemplates.GetSystemInstruction(profile, _state.Tone);
        if (!string.IsNullOrWhiteSpace(scenario.Topic))
        {
            systemInstruction += $" Highlight tooling around {scenario.Topic} when helpful.";
        }

        return new LlmPrompt(question, context, systemInstruction);
    }

    private string ComposeContext(IReadOnlyList<ContextSnippet> snippets, ScenarioEntry scenario, QuestionType type, bool consumeCue)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Candidate: Surya — DevSecOps Engineer building AI Force at HCL.");
        if (type != QuestionType.Definition && type != QuestionType.Command)
        {
            sb.AppendLine("Core Stack: Azure, AKS, Terraform, Docker, Keycloak, NGINX, ACR, CI/CD, Python automation, OpenAI.");
            sb.AppendLine("Keywords: " + _contextRetriever.DefaultKeywords);
            if (_settings.Keywords is { Length: > 0 })
            {
                sb.AppendLine("Custom Keywords: " + string.Join(", ", _settings.Keywords));
            }
        }

        if (snippets.Count > 0)
        {
            var header = (type == QuestionType.Definition || type == QuestionType.Command)
                ? "Personal Usage:"
                : "Relevant Context:";
            sb.AppendLine(header);
            foreach (var sn in snippets)
            {
                sb.Append("- ");
                sb.Append(sn.Source);
                sb.Append(": ");
                sb.AppendLine(sn.Text);
            }
        }

        if (!string.IsNullOrWhiteSpace(scenario.Example))
        {
            sb.AppendLine("ScenarioHint: " + scenario.Example);
        }
        if (!string.IsNullOrWhiteSpace(scenario.CliExamples))
        {
            sb.AppendLine("CLIHints: " + scenario.CliExamples);
        }
        var cue = consumeCue ? _state.ConsumeLiveCue() : _state.PeekLiveCue();
        if (!string.IsNullOrWhiteSpace(cue))
        {
            sb.AppendLine("ManualCue: " + cue);
        }

        sb.AppendLine();
        sb.AppendLine("Reference Format Samples:");
        sb.AppendLine(FewShotExamples);

        return sb.ToString();
    }
}
