using System.Collections.Generic;
using System.Linq;

namespace InterviewCopilot.Services.Prompting;

public sealed class ConversationState
{
    public static ConversationState Instance { get; } = new ConversationState();

    private readonly object _lock = new();
    private string _liveCue = string.Empty;
    private PromptTone _tone = PromptTone.Neutral;
    private readonly Queue<(string Question, string Answer)> _history = new();
    private const int MaxHistory = 5;

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

    public void AddHistory(string question, string answer)
    {
        if (string.IsNullOrWhiteSpace(question) || string.IsNullOrWhiteSpace(answer)) return;
        lock (_lock)
        {
            _history.Enqueue((question.Trim(), answer.Trim()));
            while (_history.Count > MaxHistory) _history.Dequeue();
        }
    }

    public IReadOnlyList<(string Question, string Answer)> GetRecentHistory(int take)
    {
        lock (_lock)
        {
            return _history.Reverse().Take(take).ToList();
        }
    }
}
