using System;
using System.Collections.Generic;
using System.Linq;

namespace InterviewCopilot.Services.Prompting;

public sealed class ConversationState
{
    public static ConversationState Instance { get; } = new ConversationState();

    private readonly object _lock = new();
    private sealed record ConversationEntry(string Question, string Answer, HashSet<string> Keywords);
    private string _liveCue = string.Empty;
    private PromptTone _tone = PromptTone.Neutral;
    private readonly Queue<ConversationEntry> _history = new();
    private const int MaxHistory = 5;
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the","and","you","for","with","that","this","from","about","into",
        "have","has","had","were","was","are","any","your","their","they","them",
        "what","when","where","which","would","could","should","please","thanks",
        "tell","more","need","want","like","just","know","give","take","been","being"
    };

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
            var entry = new ConversationEntry(
                question.Trim(),
                answer.Trim(),
                ExtractKeywords(question));
            _history.Enqueue(entry);
            while (_history.Count > MaxHistory) _history.Dequeue();
        }
    }

    public IReadOnlyList<(string Question, string Answer)> GetRecentHistory(int take)
    {
        lock (_lock)
        {
            return _history.Reverse()
                .Take(take)
                .Select(e => (e.Question, e.Answer))
                .ToList();
        }
    }

    public IReadOnlyList<(string Question, string Answer)> GetRelatedHistory(string question, int take = 1)
    {
        if (string.IsNullOrWhiteSpace(question)) return Array.Empty<(string, string)>();
        var target = ExtractKeywords(question);
        if (target.Count == 0) return Array.Empty<(string, string)>();
        lock (_lock)
        {
            return _history
                .Select(entry => new
                {
                    entry.Question,
                    entry.Answer,
                    Score = Score(entry.Keywords, target)
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Answer.Length)
                .Take(take)
                .Select(x => (x.Question, x.Answer))
                .ToList();
        }
    }

    private static HashSet<string> ExtractKeywords(string text)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text)) return set;
        var tokens = text
            .ToLowerInvariant()
            .Split(new[] { ' ', '\t', '\r', '\n', ',', '.', ';', ':', '?', '!' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            var clean = new string(token.Where(char.IsLetterOrDigit).ToArray());
            if (clean.Length <= 2) continue;
            if (StopWords.Contains(clean)) continue;
            set.Add(clean);
        }
        return set;
    }

    private static int Score(HashSet<string> source, HashSet<string> target)
    {
        if (source.Count == 0 || target.Count == 0) return 0;
        return source.Count > target.Count
            ? target.Count(word => source.Contains(word))
            : source.Count(word => target.Contains(word));
    }
}
