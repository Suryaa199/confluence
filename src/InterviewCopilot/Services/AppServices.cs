using InterviewCopilot.Models;

namespace InterviewCopilot.Services;

public static class AppServices
{
    private static readonly ISettingsStore SettingsStore = new JsonSettingsStore();
    private static readonly ISecretStore SecretStore = new DpapiSecretStore();

    public static IAudioService Audio { get; } = new Audio.NaudioAudioService();
    public static IVadService Vad { get; } = new Audio.VadEnergyGate();

    public static IAsrService Asr { get; private set; } = CreateAsr();
    public static ILlmService Llm { get; private set; } = CreateLlm();
    public static ICoachingService Coaching { get; } = new DefaultCoachingService(Llm);
    public static IStoryRepository Stories { get; } = new FileStoryRepository();
    public static ITtsService Tts { get; private set; } = CreateTts();

    public static Settings LoadSettings() => SettingsStore.Load();

    public static Orchestrator CreateOrchestrator()
    {
        ReloadAiClients();
        return new Orchestrator(Audio, Vad, Asr, Coaching, new DiskOfflineSpooler(), LoadSettings());
    }

    private static ILlmService CreateLlm()
    {
        var key = SecretStore.GetSecret("OpenAI:ApiKey");
        var model = LoadSettings().ChatModel ?? "gpt-4o-mini";
        return string.IsNullOrWhiteSpace(key) ? new NoopLlmService() : new OpenAI.OpenAiLlmService(key, model);
    }

    private static IAsrService CreateAsr()
    {
        var key = SecretStore.GetSecret("OpenAI:ApiKey");
        var model = LoadSettings().AsrModel ?? "whisper-1";
        return string.IsNullOrWhiteSpace(key) ? new NoopAsrService() : new OpenAI.OpenAiWhisperAsrService(key) { Model = model };
    }

    public static void ReloadAiClients()
    {
        Llm = CreateLlm();
        Asr = CreateAsr();
        Tts = CreateTts();
    }

    private static ITtsService CreateTts()
    {
        var s = LoadSettings();
        return new Tts.SapiTtsService(s.TtsUseCommunications);
    }
}
