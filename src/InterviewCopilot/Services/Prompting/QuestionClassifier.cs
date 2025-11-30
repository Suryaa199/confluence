using System.Text.RegularExpressions;

namespace InterviewCopilot.Services.Prompting;

public sealed class QuestionClassifier
{
    private static readonly Regex CommandRegex = new(@"(command|cli|syntax|run|execute)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public QuestionType Classify(string question)
    {
        if (string.IsNullOrWhiteSpace(question)) return QuestionType.General;
        var q = question.Trim();
        var lower = q.ToLowerInvariant();

        if (lower.Contains("what is") || lower.Contains("define") || lower.Contains("difference") || lower.Contains("compare"))
            return QuestionType.Definition;
        if (lower.Contains("challenge") || lower.Contains("issue") || lower.Contains("problem you solved"))
            return QuestionType.Challenge;
        if (lower.Contains("troubleshoot") || lower.Contains("debug") || lower.Contains("fix") || lower.Contains("incident"))
            return QuestionType.Troubleshooting;
        if (lower.Contains("architecture") || lower.Contains("design") || lower.Contains("how would you build"))
            return QuestionType.Architecture;
        if (commandKeywords(lower) || CommandRegex.IsMatch(lower))
            return QuestionType.Command;
        if (lower.Contains("secure") || lower.Contains("security") || lower.Contains("devsecops"))
            return QuestionType.Security;
        if (lower.Contains("experience") || lower.Contains("project") || lower.Contains("tell me about yourself") || lower.Contains("role"))
            return QuestionType.Experience;

        return QuestionType.General;
    }

    private static bool commandKeywords(string lower)
    {
        string[] tokens = { "git", "kubectl", "terraform", "az ", "docker", "helm", "keycloak", "trivy" };
        return tokens.Any(t => lower.Contains(t + " ") || lower.EndsWith(t));
    }
}
