namespace InterviewCopilot.Services.Prompting;

public static class PromptTemplates
{
    public static string GetSystemInstruction(QuestionType type, PromptTone tone, QuestionCategory category)
    {
        var baseInstruction =
            "You are Interview Copilot, a senior DevOps/DevSecOps assistant. Always produce exactly three numbered bullets (1., 2., 3.) with short, high-impact sentences that mention tools, metrics, or results. " +
            "Never greet, apologize, or mention question quality. Infer intent even if ASR text is messy. After the bullets, output exactly one CLI line and one Mini Example line. " +
            "Do not repeat achievements, do not output placeholders (TBD, general knowledge, continuous learning, N/A, not sure). Tone must stay confident and concise.";
        var toneInstruction = tone switch
        {
            PromptTone.Concise => "Stay razor-sharp: default to the highest-leverage metrics and tooling per bullet.",
            PromptTone.Detailed => "You can mention one extra specificity (metric/tool) per line but stay under 15 words.",
            _ => string.Empty
        };

        var specialization = type switch
        {
            QuestionType.Definition => "Definition pattern: essence, personal usage, metric impact.",
            QuestionType.Command => "Command pattern: purpose, when/how run, safe flags/result.",
            QuestionType.Challenge => "Challenge pattern: detect, fix, quantify recovery.",
            QuestionType.Troubleshooting => "Troubleshooting: check, validate, fix (mention tool/CLI).",
            QuestionType.Architecture => "Architecture: design stack, secure/automate, scale metric.",
            QuestionType.Security => "Security: RBAC/identity, policies/scans, monitoring metrics.",
            QuestionType.Experience => "Leadership/STAR: situation, action, result metric.",
            _ => "General: each bullet = tool + action + metric."
        };

        var categoryInstruction = category switch
        {
            QuestionCategory.Behavioral => "Use STAR mindset: S, A, R compressed into the three bullets.",
            QuestionCategory.Architecture => "If architecture, mention components flow and optional ASCII arrow diagram (max 2 lines).",
            QuestionCategory.Troubleshooting => "Troubleshooting answers must show Check → Validate → Fix path.",
            QuestionCategory.Security => "Security answers must cite identity, network, scanning, monitoring in order.",
            _ => string.Empty
        };

        return $"{baseInstruction} {toneInstruction} {specialization} {categoryInstruction}".Trim();
    }
}
