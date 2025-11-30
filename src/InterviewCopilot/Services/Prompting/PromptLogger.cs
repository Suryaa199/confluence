using System.IO;
using System.Text.Json;

namespace InterviewCopilot.Services.Prompting;

public sealed class PromptLogger
{
    private readonly string _filePath;

    public PromptLogger()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InterviewCopilot", "logs");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "prompts.jsonl");
    }

    public bool TryLog(LlmPrompt prompt, string answer)
    {
        try
        {
            var entry = new
            {
                at = DateTimeOffset.UtcNow,
                question = prompt.Question,
                system = prompt.SystemInstruction,
                context = prompt.Context,
                answer
            };
            File.AppendAllText(_filePath, JsonSerializer.Serialize(entry) + "\n");
            return true;
        }
        catch
        {
            return false;
        }
    }
}
