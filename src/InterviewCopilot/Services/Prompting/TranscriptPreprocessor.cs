using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace InterviewCopilot.Services.Prompting;

public static class TranscriptPreprocessor
{
    private static readonly string[] NoiseTokens =
    {
        "you you",
        "yeah",
        "um",
        "uh",
        "hmm",
        "erm",
        "please",
        "hello",
        "oh",
        "money",
        "city"
    };

    private static readonly string[] GreetingTokens =
    {
        "hi",
        "hello",
        "good morning",
        "good afternoon",
        "good evening",
        "hey",
        "nice to meet"
    };

    private static readonly string[] BehavioralTokens =
    {
        "tell me about a time",
        "tell me about yourself",
        "describe a time",
        "conflict",
        "challenge",
        "teamwork",
        "leadership",
        "strength",
        "weakness"
    };

    public static string Clean(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var result = text;
        foreach (var token in NoiseTokens)
        {
            result = Regex.Replace(result, Regex.Escape(token), " ", RegexOptions.IgnoreCase);
        }
        result = Regex.Replace(result, @"\b(uh|um|hmm|erm|please)\b", " ", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\b(you)\s+(you)\b", "you", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\b(\w+)(\s+\1\b)+", "$1", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, "\s+", " ");
        return result.Trim();
    }

    public static string ExtractLatestQuestion(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript)) return string.Empty;
        var trimmed = transcript.Trim();
        var lastQuestion = trimmed.LastIndexOf('?');
        if (lastQuestion >= 0)
        {
            var start = trimmed.LastIndexOfAny(new[] { '.', '!', '?', '\n' }, Math.Max(0, lastQuestion - 1));
            var question = trimmed.Substring(start >= 0 ? start + 1 : 0, lastQuestion - (start >= 0 ? start + 1 : 0) + 1);
            return question.Trim();
        }
        var sentences = trimmed.Split(new[] { '.', '!', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = sentences.Length - 1; i >= 0; i--)
        {
            var sentence = sentences[i].Trim();
            if (sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 3)
            {
                return sentence;
            }
        }
        return trimmed;
    }

    private static readonly string[] ArchitectureTokens =
    {
        "architecture",
        "design",
        "diagram",
        "ha",
        "dr",
        "scaling",
        "network",
        "load balancer",
        "ingress"
    };

    private static readonly string[] TroubleshootTokens =
    {
        "debug",
        "troubleshoot",
        "issue",
        "incident",
        "latency",
        "outage",
        "failure",
        "restore"
    };

    private static readonly string[] SecurityTokens =
    {
        "secure",
        "security",
        "rbac",
        "aad",
        "key vault",
        "network policy",
        "policy",
        "ciso",
        "iam"
    };

    public static QuestionCategory Classify(string question)
    {
        if (string.IsNullOrWhiteSpace(question)) return QuestionCategory.Noise;
        var lower = question.ToLowerInvariant();
        if (GreetingTokens.Any(lower.Contains)) return QuestionCategory.Greeting;
        if (BehavioralTokens.Any(lower.Contains)) return QuestionCategory.Behavioral;
        if (ArchitectureTokens.Any(lower.Contains)) return QuestionCategory.Architecture;
        if (TroubleshootTokens.Any(lower.Contains)) return QuestionCategory.Troubleshooting;
        if (SecurityTokens.Any(lower.Contains)) return QuestionCategory.Security;
        if (lower.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 3) return QuestionCategory.Noise;
        return QuestionCategory.Technical;
    }
}
