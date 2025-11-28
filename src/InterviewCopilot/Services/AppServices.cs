using InterviewCopilot.Models;

namespace InterviewCopilot.Services;

public enum OpenAiKeySource
{
    None,
    Stored,
    Environment
}

public static class AppServices
{
    private static readonly ISettingsStore SettingsStore = new JsonSettingsStore();
    private static readonly ISecretStore SecretStore = new DpapiSecretStore();

    public static IAudioService Audio { get; } = new Audio.NaudioAudioService();
    public static IVadService Vad { get; private set; } = CreateVad();

    public static IAsrService Asr { get; private set; } = CreateAsr();
    public static ILlmService Llm { get; private set; } = CreateLlm();
    public static ICoachingService Coaching { get; } = new DefaultCoachingService(Llm);
    public static IStoryRepository Stories { get; } = new FileStoryRepository();
    public static ITtsService Tts { get; private set; } = CreateTts();

    public static Settings LoadSettings() => SettingsStore.Load();

    public static bool HasOpenAiKey() => GetOpenAiKeySource() != OpenAiKeySource.None;

    public static bool HasStoredOpenAiKey()
        => SecretStore is DpapiSecretStore dpapi && dpapi.HasStoredSecret("OpenAI:ApiKey");

    public static OpenAiKeySource GetOpenAiKeySource()
    {
        var env = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(env)) return OpenAiKeySource.Environment;
        return HasStoredOpenAiKey() ? OpenAiKeySource.Stored : OpenAiKeySource.None;
    }

    public static Orchestrator CreateOrchestrator()
    {
        ReloadAiClients();
        return new Orchestrator(Audio, Vad, Asr, Coaching, new DiskOfflineSpooler(), LoadSettings());
    }

    private static ILlmService CreateLlm()
    {
        var s = LoadSettings();
        if (string.Equals(s.LlmProvider, "Ollama", StringComparison.OrdinalIgnoreCase))
        {
            return new Local.OllamaLlmService(s.OllamaBaseUrl, s.OllamaModel);
        }
        var key = SecretStore.GetSecret("OpenAI:ApiKey");
        var model = s.ChatModel ?? "gpt-4o-mini";
        return string.IsNullOrWhiteSpace(key) ? new NoopLlmService() : new OpenAI.OpenAiLlmService(key, model);
    }

    private static IAsrService CreateAsr()
    {
        var s = LoadSettings();
        if (string.Equals(s.AsrProvider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            return new Local.LocalAsrService(s.FasterWhisperUrl, s.FasterWhisperModel);
        }
        var key = SecretStore.GetSecret("OpenAI:ApiKey");
        var model = s.AsrModel ?? "whisper-1";
        return string.IsNullOrWhiteSpace(key) ? new NoopAsrService() : new OpenAI.OpenAiWhisperAsrService(key) { Model = model };
    }

    public static void ReloadAiClients()
    {
        Llm = CreateLlm();
        Asr = CreateAsr();
        Tts = CreateTts();
        Vad = CreateVad();
    }

    private static ITtsService CreateTts()
    {
        var s = LoadSettings();
        return new Tts.SapiTtsService(s.TtsUseCommunications);
    }

    private static IVadService CreateVad()
    {
        var s = LoadSettings();
        if (s.EnableSileroVad)
        {
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, "models", "silero_vad.onnx");
            if (System.IO.File.Exists(path))
            {
                var vad = new Audio.SileroVadService();
                vad.Configure(true, s.VadMinVoiceMs, s.VadMaxSilenceMs);
                vad.SetParameters(s.SileroWindowMs, s.SileroThreshold);
                return vad;
            }
        }
        var def = new Audio.VadEnergyGate();
        def.Configure(true, LoadSettings().VadMinVoiceMs, LoadSettings().VadMaxSilenceMs);
        return def;
    }
}
