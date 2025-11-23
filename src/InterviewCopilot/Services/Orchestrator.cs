using InterviewCopilot.Models;
using InterviewCopilot.Services.Audio;

namespace InterviewCopilot.Services;

public sealed class Orchestrator : IDisposable
{
    private readonly IAudioService _audio;
    private readonly IVadService _vad;
    private readonly IAsrService _asr;
    private readonly ICoachingService _coach;
    private readonly IOfflineSpooler _spooler;
    private readonly Settings _settings;

    private CancellationTokenSource? _cts;
    private readonly List<float> _currentBuffer = new();
    private DateTime _lastSpeech = DateTime.MinValue;
    private const int TargetRate = 16000;
    private readonly object _lock = new();
    private int _rev;
    private bool _answerInProgress;
    private readonly System.Text.StringBuilder _agg = new();

    public event Action<string>? OnTranscript;
    public event Action<string>? OnAnswerToken;
    public event Action<IReadOnlyList<string>>? OnFollowUps;

    public Orchestrator(IAudioService audio, IVadService vad, IAsrService asr, ICoachingService coach, IOfflineSpooler spooler, Settings settings)
    {
        _audio = audio;
        _vad = vad;
        _asr = asr;
        _coach = coach;
        _spooler = spooler;
        _settings = settings;
        _vad.Configure(enabled: true, minVoiceMs: 200, maxSilenceMs: 600);
        _audio.OnFrame += HandleFrame;
    }

    public async Task StartAsync(AudioOptions options)
    {
        _cts = new CancellationTokenSource();
        await _audio.StartAsync(options);
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

            if (!_vad.IsSpeech(mono16k))
            {
                // If enough buffered and we hit silence, flush
                if (_currentBuffer.Count >= (TargetRate * _settings.ChunkSizeMs / 1000))
                {
                    await FlushChunkAsync();
                }
                return;
            }

            _lastSpeech = DateTime.UtcNow;
            _currentBuffer.AddRange(mono16k);
            var minChunkSamples = TargetRate * _settings.ChunkSizeMs / 1000;
            if (_currentBuffer.Count >= minChunkSamples)
            {
                await FlushChunkAsync();
            }
        }
        catch
        {
            // Best-effort; avoid crashing capture thread
        }
    }

    private async Task FlushChunkAsync()
    {
        if (_currentBuffer.Count == 0) return;
        var samples = _currentBuffer.ToArray();
        _currentBuffer.Clear();
        var wav = WavEncoder.EncodePcm16kMono(samples);
        try
        {
            var text = await _asr.TranscribeChunkAsync(wav, _cts?.Token ?? CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(text))
            {
                lock (_lock)
                {
                    _agg.Append(_agg.Length > 0 ? " " : string.Empty);
                    _agg.Append(text);
                    _rev++;
                }
                OnTranscript?.Invoke(text);
                _ = DebouncedGenerateAsync();
            }
        }
        catch
        {
            // network error → spool and try later
            _spooler.Enqueue(wav);
        }
    }

    public async Task GenerateAnswerAsync(string question, string context, CancellationToken ct)
    {
        try
        {
            await foreach (var token in AppServices.Llm.StreamAnswerAsync(question, context, ct))
            {
                OnAnswerToken?.Invoke(token);
            }
            var (answer, follow) = await _coach.GenerateAsync(question, context, ct);
            OnFollowUps?.Invoke(follow);
        }
        catch { }
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
        await Task.Delay(1200);
        lock (_lock)
        {
            if (startRev != _rev || _answerInProgress) return;
            _answerInProgress = true;
        }
        var ctx = BuildContext();
        try
        {
            await GenerateAnswerAsync(question, ctx, _cts?.Token ?? CancellationToken.None);
        }
        finally
        {
            lock (_lock) _answerInProgress = false;
        }
    }

    private string BuildContext()
    {
        var s = _settings;
        var ctx = string.Empty;
        if (s.Keywords is { Length: > 0 }) ctx += "Keywords: " + string.Join(", ", s.Keywords) + "\n";
        if (!string.IsNullOrWhiteSpace(s.CompanyBlurb)) ctx += "Company: " + s.CompanyBlurb + "\n";
        return ctx;
    }

    public void Dispose()
    {
        _audio.OnFrame -= HandleFrame;
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
