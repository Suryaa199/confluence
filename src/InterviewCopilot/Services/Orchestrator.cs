using InterviewCopilot.Models;
using InterviewCopilot.Services.Audio;
using InterviewCopilot.Services.Prompting;
using System.Text;
using System.Threading;

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
    private string _lastTranscriptChunk = string.Empty;
    private CancellationTokenSource? _answerCts;
    private (string Text, QuestionCategory Category)? _pendingQuestion;
    private int _pendingCheckScheduled;
    public event Action<double>? OnNoiseLevel;
    public event Action? OnSpeechInterruption;

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
        _audio.OnLevel += HandleAudioLevel;
        if (_audio is Audio.NaudioAudioService naudio)
        {
            naudio.OnSilenceDetected += HandleSilence;
        }
    }

    public async Task StartAsync(AudioOptions options)
    {
        _cts = new CancellationTokenSource();
        _lastTranscriptChunk = string.Empty;
        await _audio.StartAsync(options);
        _ = _spooler.FlushAsync(ProcessSpoolChunkAsync, _cts.Token);
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        _answerCts?.Cancel();
        await _audio.StopAsync();
        _cts?.Dispose();
        _cts = null;
        _answerCts?.Dispose();
        _answerCts = null;
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
        var cleaned = TranscriptPreprocessor.Clean(text);
        if (string.IsNullOrWhiteSpace(cleaned)) return;
        if (string.Equals(cleaned, _lastTranscriptChunk, StringComparison.OrdinalIgnoreCase)) return;
        _lastTranscriptChunk = cleaned;
        if (_smallTalkResponder.TryRespond(text, OnAnswerToken))
        {
            OnTranscript?.Invoke(cleaned);
            return;
        }
        _hintEngine.Analyze(cleaned, OnAnswerToken);
        lock (_lock)
        {
            _agg.Append(_agg.Length > 0 ? " " : string.Empty);
            _agg.Append(cleaned);
            _rev++;
        }
        OnTranscript?.Invoke(cleaned);
        _ = DebouncedGenerateAsync();
    }

    public async Task GenerateAnswerAsync(LlmPrompt prompt, CancellationToken ct)
    {
        try
        {
            await _coach.GenerateAsync(
                prompt,
                token => OnAnswerToken?.Invoke(token),
                followUps =>
                {
                    OnFollowUps?.Invoke(followUps);
                    ResetTranscriptBuffer();
                    _ = TryProcessQueuedQuestionAsync();
                },
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
        var extracted = TranscriptPreprocessor.ExtractLatestQuestion(question);
        if (string.IsNullOrWhiteSpace(extracted)) return;
        var category = TranscriptPreprocessor.Classify(extracted);
        extracted = QuestionIntentRebuilder.Rebuild(extracted);
        extracted = QuestionSanitizer.Sanitize(extracted);
        if (string.IsNullOrWhiteSpace(extracted) || extracted.Length < 3)
        {
            LogService.Warn("Skipping question due to insufficient content.");
            lock (_lock) _answerInProgress = false;
            return;
        }
        await Task.Delay(1200);
        if (category == QuestionCategory.Greeting || category == QuestionCategory.Noise)
        {
            return;
        }
        lock (_lock)
        {
            if (startRev != _rev)
            {
                return;
            }
            if (_answerInProgress)
            {
                _pendingQuestion = (extracted, category);
                LogService.Info("Queued question while answer in progress.");
                SchedulePendingQuestionCheck();
                return;
            }
            if (!HasRequiredSilence())
            {
                _pendingQuestion = (extracted, category);
                LogService.Info("Queued question awaiting silence.");
                SchedulePendingQuestionCheck();
                return;
            }
            _answerInProgress = true;
        }
        await RunAnswerFlowAsync(extracted, category);
    }

    private async Task ProcessSpoolChunkAsync(byte[] wavBytes, CancellationToken ct)
    {
        var text = await _asr.TranscribeChunkAsync(wavBytes, ct);
        HandleTranscript(FilterToEnglish(text));
    }

    private static async Task<string> GenerateDraftOutlineAsync(LlmPrompt prompt, CancellationToken ct)
    {
        var sb = new System.Text.StringBuilder();
        try
        {
            await foreach (var token in AppServices.Llm.StreamAnswerAsync(prompt, ct))
            {
                sb.Append(token);
                if (sb.Length > 600) break;
            }
        }
        catch
        {
            return string.Empty;
        }
        return sb.ToString().Trim();
    }

    public void Dispose()
    {
        _audio.OnFrame -= HandleFrame;
        _audio.OnLevel -= HandleAudioLevel;
        if (_audio is Audio.NaudioAudioService naudio)
        {
            naudio.OnSilenceDetected -= HandleSilence;
        }
        _cts?.Cancel();
        _cts?.Dispose();
        _answerCts?.Cancel();
        _answerCts?.Dispose();
        _answerCts = null;
        _lastTranscriptChunk = string.Empty;
    }

    private void HandleSilence()
    {
        // Do not cancel active answers on brief silence; only try processing queued questions.
        _ = TryProcessQueuedQuestionAsync();
    }

    private void HandleAudioLevel(double level)
    {
        OnNoiseLevel?.Invoke(level);
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

    private void ResetTranscriptBuffer()
    {
        lock (_lock)
        {
            _agg.Clear();
            _rev = 0;
            _lastTranscriptChunk = string.Empty;
        }
        LogService.Info("Transcript cleared");
    }

    private bool HasRequiredSilence()
    {
        if (_lastSpeech == DateTime.MinValue) return true;
        var elapsed = DateTime.UtcNow - _lastSpeech;
        return elapsed.TotalMilliseconds >= _settings.VadMaxSilenceMs;
    }

    private async Task RunAnswerFlowAsync(string question, QuestionCategory category)
    {
        CancellationToken token;
        lock (_lock)
        {
            _answerCts?.Cancel();
            _answerCts?.Dispose();
            _answerCts = CancellationTokenSource.CreateLinkedTokenSource(_cts?.Token ?? CancellationToken.None);
            token = _answerCts.Token;
        }
        var draftPrompt = _promptBuilder.BuildDraft(question, category);
        var draftOutline = await GenerateDraftOutlineAsync(draftPrompt, token);
        var prompt = _promptBuilder.Build(question, category, draft: draftOutline);
        try
        {
            await GenerateAnswerAsync(prompt, token);
        }
        finally
        {
            lock (_lock)
            {
                _answerInProgress = false;
                _answerCts?.Dispose();
                _answerCts = null;
            }
        }
    }

    private Task TryProcessQueuedQuestionAsync()
    {
        (string Text, QuestionCategory Category)? pending = null;
        lock (_lock)
        {
            if (_answerInProgress) return Task.CompletedTask;
            if (_pendingQuestion is null) return Task.CompletedTask;
            if (!HasRequiredSilence()) return Task.CompletedTask;
            pending = _pendingQuestion;
            _pendingQuestion = null;
            _answerInProgress = true;
        }
        LogService.Info("Processing queued question.");
        return RunAnswerFlowAsync(pending.Value.Text, pending.Value.Category);
    }

    private void SchedulePendingQuestionCheck()
    {
        if (Interlocked.Exchange(ref _pendingCheckScheduled, 1) == 1) return;
        var delayMs = Math.Max(600, _settings.VadMaxSilenceMs + 200);
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMs);
                await TryProcessQueuedQuestionAsync();
            }
            finally
            {
                Interlocked.Exchange(ref _pendingCheckScheduled, 0);
            }
        });
    }

    public void CancelActiveAnswer()
    {
        CancellationTokenSource? toCancel = null;
        lock (_lock)
        {
            toCancel = _answerCts;
        }
        toCancel?.Cancel();
    }
}
