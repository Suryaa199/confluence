namespace InterviewCopilot.Services.Prompting;

public sealed class ConversationState
{
    public static ConversationState Instance { get; } = new ConversationState();

    private readonly object _lock = new();
    private string _liveCue = string.Empty;
    private PromptTone _tone = PromptTone.Neutral;

    private ConversationState() { }

    public void SetLiveCue(string cue)
    {
        lock (_lock)
        {
            _liveCue = cue?.Trim() ?? string.Empty;
        }
    }

    public string ConsumeLiveCue()
    {
        lock (_lock)
        {
            var cue = _liveCue;
            _liveCue = string.Empty;
            return cue;
        }
    }

    public string PeekLiveCue()
    {
        lock (_lock)
        {
            return _liveCue;
        }
    }

    public PromptTone Tone
    {
        get
        {
            lock (_lock) return _tone;
        }
    }

    public void SetTone(PromptTone tone)
    {
        lock (_lock)
        {
            _tone = tone;
        }
    }
}
