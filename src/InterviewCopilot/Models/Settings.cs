namespace InterviewCopilot.Models;

public class Settings
{
    public int ChunkSizeMs { get; set; } = 500;
    public bool EnableSileroVad { get; set; } = false;
    public string[]? Keywords { get; set; }
    public string? CompanyBlurb { get; set; }
    public int VadMinVoiceMs { get; set; } = 200;
    public int VadMaxSilenceMs { get; set; } = 600;
    public int SileroWindowMs { get; set; } = 30; // 30ms default
    public float SileroThreshold { get; set; } = 0.5f; // 0..1 speech prob
    public string? CheatSheet { get; set; }
    public string? ResumeText { get; set; }
    public string? JobDescText { get; set; }
    public string ChatModel { get; set; } = "gpt-4o-mini";
    public string AsrModel { get; set; } = "whisper-1";
    public string? PreferredProcessName { get; set; }
    public bool SpeakAnswers { get; set; } = false;
    public bool TtsUseCommunications { get; set; } = true;
    public string[]? EnabledKnowledgePacks { get; set; }

    // Providers
    public string LlmProvider { get; set; } = "OpenAI"; // OpenAI | Ollama
    public string AsrProvider { get; set; } = "OpenAI"; // OpenAI | Local | Deepgram

    // Ollama (Local LLM)
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "llama3.1:8b-instruct";

    // Faster-Whisper (Local ASR)
    public string FasterWhisperUrl { get; set; } = "http://localhost:5055"; // custom server
    public string FasterWhisperModel { get; set; } = "base";

    // Deepgram
    public string DeepgramBaseUrl { get; set; } = "https://api.deepgram.com";
    public string DeepgramModel { get; set; } = "nova-2-general";
}
