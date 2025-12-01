using InterviewCopilot.Models;
using InterviewCopilot.Services.Audio;
using InterviewCopilot.Services.Prompting;
using System.Text;

namespace InterviewCopilot.Services;

public sealed class Orchestrator : IDisposable
{
    private readonly IAudioService _audio;
    private readonly IVadService _vad;
    private readonly IAsrService _asr;
    private readonly ICoachingService _coach;
    private readonly IOfflineSpooler _spooler;
    private readonly Settings _settings;
    private readonly AnswerPromptBuilder _promptBuilder;
    private readonly SmallTalkResponder _smallTalkResponder = new();
    private readonly ConversationHintEngine _hintEngine = new();

    private CancellationTokenSource? _cts;
    private readonly List<float> _currentBuffer = new();
    private readonly object _bufferLock = new();
    private DateTime _lastSpeech = DateTime.MinValue;
    private const int TargetRate = 16000;
    private readonly object _lock = new();
    private int _rev;
    private bool _answerInProgress;
    private readonly System.Text.StringBuilder _agg = new();

    public event Action<string>? OnTranscript;
    public event Action<string>? OnAnswerToken;
    public event Action<IReadOnlyList<string>>? OnFollowUps;
    public event Action<string>? OnAsrError;
    public event Action<string>? OnLlmError;

    public Orchestrator(IAudioService audio, IVadService vad, IAsrService asr, ICoachingService coach, IOfflineSpooler spooler, Settings settings)
    {
        _audio = audio;
        _vad = vad;
        _asr = asr;
        _coach = coach;
        _spooler = spooler;
        _settings = settings;
        _promptBuilder = new AnswerPromptBuilder(settings, ConversationState.Instance);
        _vad.Configure(enabled: true, minVoiceMs: settings.VadMinVoiceMs, maxSilenceMs: settings.VadMaxSilenceMs);
        _audio.OnFrame += HandleFrame;
    }

    public async Task StartAsync(AudioOptions options)
    {
        _cts = new CancellationTokenSource();
        await _audio.StartAsync(options);
        _ = _spooler.FlushAsync(ProcessSpoolChunkAsync, _cts.Token);
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        await _audio.StopAsync();
        _cts?.Dispose();
        _cts = null;
    }

    private async void HandleFrame(AudioFrame frame)
    {
        try
        {
            // Resample to 16k mono
            var mono16k = frame.SampleRate == TargetRate
                ? frame.Samples
                : Resampler.ToSampleRate(frame.Samples, frame.SampleRate, TargetRate);
            var now = DateTime.UtcNow;
            var minChunkSamples = TargetRate * _settings.ChunkSizeMs / 1000;
            float[]? samplesToFlush = null;
            var hasSpeech = _vad.IsSpeech(mono16k);

            lock (_bufferLock)
            {
                if (hasSpeech)
                {
                    _currentBuffer.AddRange(mono16k);
                    _lastSpeech = now;
                    if (_currentBuffer.Count >= minChunkSamples)
                    {
                        samplesToFlush = _currentBuffer.ToArray();
                        _currentBuffer.Clear();
                    }
                }
                else if (_currentBuffer.Count >= minChunkSamples ||
                    (_currentBuffer.Count > 0 && (now - _lastSpeech).TotalMilliseconds >= _settings.VadMaxSilenceMs))
                {
                    samplesToFlush = _currentBuffer.ToArray();
                    _currentBuffer.Clear();
                }
            }

            if (samplesToFlush is not null)
            {
                await FlushChunkAsync(samplesToFlush);
            }
        }
        catch
        {
            // Best-effort; avoid crashing capture thread
        }
    }

    private async Task FlushChunkAsync(float[] samples)
    {
        var wav = WavEncoder.EncodePcm16kMono(samples);
        try
        {
            var text = await _asr.TranscribeChunkAsync(wav, _cts?.Token ?? CancellationToken.None);
            HandleTranscript(FilterToEnglish(text));
        }
        catch (Exception ex)
        {
            // network error → spool and try later
            _spooler.Enqueue(wav);
            OnAsrError?.Invoke(ex.Message);
        }
    }

    private void HandleTranscript(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (_smallTalkResponder.TryRespond(text, OnAnswerToken))
        {
            OnTranscript?.Invoke(text);
            return;
        }
        _hintEngine.Analyze(text, OnAnswerToken);
        lock (_lock)
        {
            _agg.Append(_agg.Length > 0 ? " " : string.Empty);
            _agg.Append(text);
            _rev++;
        }
        OnTranscript?.Invoke(text);
        _ = DebouncedGenerateAsync();
    }

    public async Task GenerateAnswerAsync(LlmPrompt prompt, CancellationToken ct)
    {
        try
        {
            await _coach.GenerateAsync(
                prompt,
                token => OnAnswerToken?.Invoke(token),
                followUps => OnFollowUps?.Invoke(followUps),
                ct);
        }
        catch (Exception ex)
        {
            OnLlmError?.Invoke(ex.Message);
        }
    }

    private async Task DebouncedGenerateAsync()
    {
        int startRev;
        string question;
        lock (_lock)
        {
            startRev = _rev;
            question = _agg.ToString();
        }
        question = TakeRecentQuestion(question);
        question = Prompting.QuestionIntentRebuilder.Rebuild(question);
        await Task.Delay(1200);
        lock (_lock)
        {
            if (startRev != _rev || _answerInProgress) return;
            _answerInProgress = true;
        }
        var prompt = _promptBuilder.Build(question);
        try
        {
            await GenerateAnswerAsync(prompt, _cts?.Token ?? CancellationToken.None);
        }
        finally
        {
            lock (_lock) _answerInProgress = false;
        }
    }

    private async Task ProcessSpoolChunkAsync(byte[] wavBytes, CancellationToken ct)
    {
        var text = await _asr.TranscribeChunkAsync(wavBytes, ct);
        HandleTranscript(FilterToEnglish(text));
    }

    public void Dispose()
    {
        _audio.OnFrame -= HandleFrame;
        _cts?.Cancel();
        _cts?.Dispose();
    }

    private static string FilterToEnglish(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var sb = new System.Text.StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if (ch <= 0x7F || char.IsWhiteSpace(ch) || char.IsPunctuation(ch))
            {
                sb.Append(ch);
            }
        }
        return sb.ToString();
    }

    private static string TakeRecentQuestion(string text, int maxChars = 320)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var trimmed = text.Trim();
        if (trimmed.Length <= maxChars) return trimmed;
        var delimiters = new[] { '?', '.', '!' };
        var last = trimmed.LastIndexOfAny(delimiters);
        if (last >= 0)
        {
            var prev = trimmed.LastIndexOfAny(delimiters, Math.Max(0, last - maxChars));
            if (prev >= 0 && last - prev > 1)
            {
                return trimmed.Substring(prev + 1).Trim();
            }
            return trimmed.Substring(Math.Max(0, last - maxChars)).Trim();
        }
        return trimmed.Substring(trimmed.Length - maxChars).Trim();
    }
}
