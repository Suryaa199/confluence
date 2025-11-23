using InterviewCopilot.Models;

namespace InterviewCopilot.Services;

public static class AppServices
{
    private static readonly ISettingsStore SettingsStore = new JsonSettingsStore();
    private static readonly ISecretStore SecretStore = new DpapiSecretStore();

    public static IAudioService Audio { get; } = new Audio.NaudioAudioService();
    public static IVadService Vad { get; } = new Audio.VadEnergyGate();

    public static IAsrService Asr { get; } = CreateAsr();
    public static ILlmService Llm { get; } = CreateLlm();
    public static ICoachingService Coaching { get; } = new DefaultCoachingService(Llm);

    public static Settings LoadSettings() => SettingsStore.Load();

    public static Orchestrator CreateOrchestrator()
        => new Orchestrator(Audio, Vad, Asr, Coaching, new DiskOfflineSpooler(), LoadSettings());

    private static ILlmService CreateLlm()
    {
        var key = SecretStore.GetSecret("OpenAI:ApiKey");
        return string.IsNullOrWhiteSpace(key) ? new NoopLlmService() : new OpenAI.OpenAiLlmService(key);
    }

    private static IAsrService CreateAsr()
    {
        var key = SecretStore.GetSecret("OpenAI:ApiKey");
        return string.IsNullOrWhiteSpace(key) ? new NoopAsrService() : new OpenAI.OpenAiWhisperAsrService(key);
    }
}
