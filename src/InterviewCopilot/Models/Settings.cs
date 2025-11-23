namespace InterviewCopilot.Models;

public class Settings
{
    public int ChunkSizeMs { get; set; } = 750;
    public bool EnableSileroVad { get; set; } = false;
    public string[]? Keywords { get; set; }
    public string? CompanyBlurb { get; set; }
}

