namespace InterviewCopilot.Services.Prompting;

public sealed class ConversationHintEngine
{
    private readonly TimeSpan _hintCooldown = TimeSpan.FromSeconds(25);
    private DateTime _lastHint = DateTime.MinValue;

    private static readonly string[] ClarifyPhrases =
    {
        "could you explain",
        "can you explain",
        "can you clarify",
        "could you clarify",
        "tell me more",
        "can you elaborate",
        "need more detail",
        "more detail on"
    };

    private static readonly string[] FollowupPhrases =
    {
        "sounds good",
        "anything else",
        "do you have anything else",
        "any other example",
        "something else you want to add"
    };

    private static readonly string[] ConciseTone =
    {
        "keep it high level",
        "short on time",
        "be brief",
        "quick summary"
    };

    private static readonly string[] DetailedTone =
    {
        "deep dive",
        "go into detail",
        "walk me through step by step",
        "give me more detail"
    };

    private static readonly string[] ResetTone =
    {
        "that's enough detail",
        "understood",
        "got it thanks"
    };

    public void Analyze(string text, Action<string>? onHint)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var lower = text.Trim().ToLowerInvariant();
        bool hinted = false;

        if (ClarifyPhrases.Any(lower.Contains))
        {
            hinted |= TryEmitHint(onHint, "\nTip: interviewer wants clarification—confirm the scope before diving in.\n");
        }
        if (FollowupPhrases.Any(lower.Contains))
        {
            hinted |= TryEmitHint(onHint, "\nTip: consider offering one more example or asking if they want more detail.\n");
        }

        if (ConciseTone.Any(lower.Contains))
        {
            ConversationState.Instance.SetTone(PromptTone.Concise);
        }
        else if (DetailedTone.Any(lower.Contains))
        {
            ConversationState.Instance.SetTone(PromptTone.Detailed);
        }
        else if (ResetTone.Any(lower.Contains))
        {
            ConversationState.Instance.SetTone(PromptTone.Neutral);
        }
    }

    private bool TryEmitHint(Action<string>? onHint, string message)
    {
        if (onHint is null) return false;
        if ((DateTime.UtcNow - _lastHint) < _hintCooldown) return false;
        _lastHint = DateTime.UtcNow;
        onHint(message);
        return true;
    }
}
