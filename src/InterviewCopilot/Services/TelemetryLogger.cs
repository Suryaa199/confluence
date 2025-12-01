using System;
using System.IO;
using System.Text.Json;

namespace InterviewCopilot.Services;

public static class TelemetryLogger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "InterviewCopilot",
        "telemetry_answers.jsonl");

    public static void LogAnswer(string question, string answer, QuestionCategory category, string persona, bool neededRetry)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            var entry = new
            {
                at = DateTimeOffset.UtcNow,
                question,
                category = category.ToString(),
                persona,
                neededRetry,
                length = answer?.Length ?? 0,
                words = string.IsNullOrWhiteSpace(answer) ? 0 : answer.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length
            };
            File.AppendAllText(LogPath, JsonSerializer.Serialize(entry) + Environment.NewLine);
        }
        catch
        {
            // swallow telemetry errors
        }
    }
}
