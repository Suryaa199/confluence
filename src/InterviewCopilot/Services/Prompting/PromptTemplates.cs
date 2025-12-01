namespace InterviewCopilot.Services.Prompting;

public static class PromptTemplates
{
    public static string GetSystemInstruction(QuestionType type, PromptTone tone, QuestionCategory category)
    {
        var baseInstruction =
            "You are an interview copilot for a senior DevOps/DevSecOps engineer with 6+ years experience. " +
            "Rules: always output EXACTLY three numbered bullets (1., 2., 3.), each under 12 words, packed with tools + impact. " +
            "Never greet, apologize, ask for clarification, or mention question quality. If the transcript is noisy or partial, reconstruct the most likely intent and answer decisively. " +
            "Tone must be confident, crisp, and consistent—no filler, no repetition of the same personal achievements or keywords unless absolutely required. " +
            "After the bullets, output one real CLI command (single line labeled 'CLI:') and one Mini Example line (<12 words). " +
            "Forbidden phrases: 'unclear', 'seems like', 'usually', 'maybe', 'I can help'. Always keep answers consistent across the session.";
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
