namespace InterviewCopilot.Models;

public class Settings
{
    public int ChunkSizeMs { get; set; } = 750;
    public bool EnableSileroVad { get; set; } = false;
    public string[]? Keywords { get; set; }
    public string? CompanyBlurb { get; set; }
    public int VadMinVoiceMs { get; set; } = 200;
    public int VadMaxSilenceMs { get; set; } = 600;
    public string? CheatSheet { get; set; }
    public string? ResumeText { get; set; }
    public string? JobDescText { get; set; }
    public string ChatModel { get; set; } = "gpt-4o-mini";
    public string AsrModel { get; set; } = "whisper-1";
    public string? PreferredProcessName { get; set; }
    public bool SpeakAnswers { get; set; } = false;
    public bool TtsUseCommunications { get; set; } = true;
}
