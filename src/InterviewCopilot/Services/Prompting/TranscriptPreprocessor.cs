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
        result = Regex.Replace(result, @"\s+", " ");
        return result.Trim();
    }

    public static string ExtractLatestQuestion(string transcript)
    {
        if (string.IsNullOrWhiteSpace(transcript)) return string.Empty;
        var trimmed = transcript.Trim();
        var pattern = "(?is)(what|how|why|when|where|explain|walk me through|tell me about)[^?]+\\?";
        var matches = Regex.Matches(trimmed, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (matches.Count > 0)
        {
            return matches[^1].Value.Trim();
        }
        var sentences = trimmed.Split(new[] { '.', '!', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = sentences.Length - 1; i >= 0; i--)
        {
            var sentence = sentences[i].Trim();
            if (sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 6)
            {
                return sentence + "?";
            }
        }
        return string.Empty;
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
        if (GreetingTokens.Any(token => ContainsToken(lower, token))) return QuestionCategory.Greeting;
        if (BehavioralTokens.Any(token => ContainsToken(lower, token))) return QuestionCategory.Behavioral;
        if (ArchitectureTokens.Any(token => ContainsToken(lower, token))) return QuestionCategory.Architecture;
        if (TroubleshootTokens.Any(token => ContainsToken(lower, token))) return QuestionCategory.Troubleshooting;
        if (SecurityTokens.Any(token => ContainsToken(lower, token))) return QuestionCategory.Security;
        if (lower.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 3) return QuestionCategory.Noise;
        return QuestionCategory.Technical;
    }

    private static bool ContainsToken(string text, string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        if (token.Contains(' ')) return text.Contains(token);
        var padded = " " + text + " ";
        return padded.Contains(" " + token + " ");
    }
}
