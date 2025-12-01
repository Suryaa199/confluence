using InterviewCopilot.Models;
using System.Text;

namespace InterviewCopilot.Services.Prompting;

public sealed class AnswerPromptBuilder
{
    private readonly Settings _settings;
    private readonly QuestionClassifier _classifier = new();
    private readonly ContextRetriever _contextRetriever;
    private readonly ConversationState _state;

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

        return sb.ToString();
    }
}
