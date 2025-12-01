using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO;

namespace InterviewCopilot.Services;

public sealed class NoopAudioService : IAudioService
{
    public bool IsCapturing { get; private set; }
    public event Action<double>? OnLevel;
    public event Action<AudioFrame>? OnFrame;
    public Task StartAsync(AudioOptions options) { IsCapturing = true; return Task.CompletedTask; }
    public Task StopAsync() { IsCapturing = false; return Task.CompletedTask; }
    public IReadOnlyList<AudioEndpoint> ListEndpoints(AudioSourceKind source) => Array.Empty<AudioEndpoint>();
}

public sealed class DefaultVadService : IVadService
{
    private bool _enabled;
    public bool Enabled => _enabled;
    public void Configure(bool enabled, int minVoiceMs, int maxSilenceMs) { _enabled = enabled; }
    public bool IsSpeech(ReadOnlySpan<float> monoPcm) => monoPcm.Length > 0; // placeholder
}

public sealed class NoopAsrService : IAsrService
{
    public Task<string> TranscribeChunkAsync(byte[] wavBytes, CancellationToken ct)
        => Task.FromResult("[transcript stub]");
}

public sealed class NoopLlmService : ILlmService
{
    public async IAsyncEnumerable<string> StreamAnswerAsync(LlmPrompt prompt, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var parts = new[] { "Here ", "is ", "a ", "stub ", "answer." };
        foreach (var p in parts)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(80, ct);
            yield return p;
        }
    }

    public Task<IReadOnlyList<string>> GenerateFollowUpsAsync(string question, string context, CancellationToken ct)
    {
        IReadOnlyList<string> list = new[]
        {
            "Could you share a concrete example?",
            "What metrics improved?",
            "What trade-offs did you consider?"
        };
        return Task.FromResult(list);
    }
}

public sealed class DefaultCoachingService : ICoachingService
{
    private readonly ILlmService _llm;
    private readonly Prompting.PromptLogger _logger = new();
    private static readonly IReadOnlyList<string> DefaultFollowUps = new[]
    {
        "Do you want me to double-click on the remediation steps?",
        "Should I walk through how we monitored the rollout?",
        "Want a CLI walkthrough of the tooling?"
    };
    public DefaultCoachingService(ILlmService llm) { _llm = llm; }

    public async Task GenerateAsync(
        LlmPrompt prompt,
        Action<string>? onAnswerToken,
        Action<IReadOnlyList<string>>? onFollowUps,
        CancellationToken ct)
    {
        var answerBuffer = new System.Text.StringBuilder();
        try
        {
            await foreach (var token in _llm.StreamAnswerAsync(prompt, ct))
            {
                onAnswerToken?.Invoke(token);
                answerBuffer.Append(token);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to stream coaching answer: {ex.Message}", ex);
        }
        finally
        {
            if (answerBuffer.Length > 0)
            {
                _ = _logger.TryLog(prompt, answerBuffer.ToString());
            }
        }

        IReadOnlyList<string> followUps = Array.Empty<string>();
        try
        {
            followUps = await _llm.GenerateFollowUpsAsync(prompt.Question, prompt.Context, ct);
        }
        catch
        {
            // If follow-up generation fails, surface empty list so UX can continue.
        }
        if (followUps is null || followUps.Count == 0)
        {
            followUps = DefaultFollowUps;
        }
        onFollowUps?.Invoke(followUps);
    }
}

public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InterviewCopilot");
    private static readonly string FilePath = Path.Combine(Dir, "settings.json");

    public Models.Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var s = JsonSerializer.Deserialize<Models.Settings>(json) ?? new Models.Settings();
                return s;
            }
        }
        catch { }
        return new Models.Settings();
    }

    public void Save(Models.Settings settings)
    {
        Directory.CreateDirectory(Dir);
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(FilePath, json);
    }
}

public sealed class DpapiSecretStore : ISecretStore
{
    private static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InterviewCopilot");
    private static readonly string FilePath = Path.Combine(Dir, "secrets.json");

    public void SaveSecret(string name, string value)
    {
        Directory.CreateDirectory(Dir);
        var dict = LoadAll();
        var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser);
        dict[name] = Convert.ToBase64String(encrypted);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(dict));
    }

    public string? GetSecret(string name)
    {
        var env = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(env)) return env;
        var dict = LoadAll();
        if (dict.TryGetValue(name, out var b64))
        {
            try
            {
                var bytes = Convert.FromBase64String(b64);
                var clear = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(clear);
            }
            catch { }
        }
        return null;
    }

    private Dictionary<string, string> LoadAll()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
        }
        catch { }
        return new();
    }

    public bool HasStoredSecret(string name)
    {
        try
        {
            var dict = LoadAll();
            return dict.ContainsKey(name);
        }
        catch
        {
            return false;
        }
    }
}
