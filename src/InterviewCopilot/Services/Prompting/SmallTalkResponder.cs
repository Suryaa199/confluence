namespace InterviewCopilot.Services.Prompting;

public sealed class SmallTalkResponder
{
    private readonly TimeSpan _cooldown = TimeSpan.FromSeconds(20);
    private DateTime _lastResponse = DateTime.MinValue;

    private static readonly (string[] Keys, string Response)[] Responses =
    {
        (new[] { "good morning", "morning" }, "Good morning! Thanks for having me—excited to get started."),
        (new[] { "good afternoon", "afternoon" }, "Good afternoon! Appreciate the time today."),
        (new[] { "good evening", "evening" }, "Good evening! Looking forward to our chat."),
        (new[] { "hi ", "hi,", " hi", "hello", "hey" }, "Hi there! Great to connect—ready when you are."),
        (new[] { "how are you", "how's it going" }, "I'm doing great, thanks for asking! Hope you're doing well too."),
        (new[] { "nice to meet", "pleasure to meet" }, "Nice to meet you as well, and thanks for the warm welcome."),
        (new[] { "thanks", "thank you", "appreciate" }, "Thank you! Really appreciate the opportunity.")
    };

    public bool TryRespond(string text, Action<string>? onAnswerToken)
    {
        if (onAnswerToken is null) return false;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if ((DateTime.UtcNow - _lastResponse) < _cooldown) return false;

        var normalized = text.Trim().ToLowerInvariant();
        if (normalized.Length > 90) return false;

        foreach (var entry in Responses)
        {
            if (entry.Keys.Any(k => normalized.Contains(k)))
            {
                _lastResponse = DateTime.UtcNow;
                onAnswerToken($"\n{entry.Response}\n");
                return true;
            }
        }
        return false;
    }
}
