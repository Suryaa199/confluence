namespace InterviewCopilot.Services;

public enum AudioSourceKind { PerApp, System, Microphone }

public enum DevicePreference { Default, Communications }
public enum SessionHint { None, Teams, Zoom, Meet, Browser }

public sealed class AudioOptions
{
    public AudioSourceKind Source { get; init; } = AudioSourceKind.System;
    public DevicePreference Device { get; init; } = DevicePreference.Default;
    public SessionHint Session { get; init; } = SessionHint.None;
    public string? EndpointId { get; init; }
    public string? PreferredProcessName { get; init; }
}

public sealed class AudioFrame
{
    public required float[] Samples { get; init; }
    public required int SampleRate { get; init; }
}

public sealed class AudioEndpoint
{
    public required string Id { get; init; }
    public required string Name { get; init; }
}

public interface IAudioService
{
    bool IsCapturing { get; }
    event Action<double>? OnLevel; // 0..1 RMS
    event Action<AudioFrame>? OnFrame;
    Task StartAsync(AudioOptions options);
    Task StopAsync();
    IReadOnlyList<AudioEndpoint> ListEndpoints(AudioSourceKind source);
}

public interface IVadService
{
    bool Enabled { get; }
    void Configure(bool enabled, int minVoiceMs, int maxSilenceMs);
    bool IsSpeech(ReadOnlySpan<float> monoPcm);
}

public interface IAsrService
{
    Task<string> TranscribeChunkAsync(byte[] wavBytes, CancellationToken ct);
}

public interface ILlmService
{
    IAsyncEnumerable<string> StreamAnswerAsync(string question, string context, CancellationToken ct);
    Task<IReadOnlyList<string>> GenerateFollowUpsAsync(string question, string context, CancellationToken ct);
}

public interface ICoachingService
{
    Task<(string Answer, IReadOnlyList<string> FollowUps)> GenerateAsync(string question, string? cheatSheet, CancellationToken ct);
}

public interface ITtsService
{
    Task SpeakAsync(string text, CancellationToken ct);
}

public interface IOfflineSpooler
{
    void Enqueue(byte[] wavBytes);
    Task FlushAsync(CancellationToken ct);
}

public interface IStoryRepository
{
    Task SaveAsync(string question, string answer, DateTimeOffset at);
    Task<IReadOnlyList<(DateTimeOffset At, string Question, string Answer)>> SearchAsync(string query);
}

public interface ISettingsStore
{
    Models.Settings Load();
    void Save(Models.Settings settings);
}

public interface ISecretStore
{
    void SaveSecret(string name, string value);
    string? GetSecret(string name);
}
