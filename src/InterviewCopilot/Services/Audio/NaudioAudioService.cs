// NOTE: Requires NAudio package. This is a best-effort skeleton to wire capture.
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.Diagnostics;

namespace InterviewCopilot.Services.Audio;

public sealed class NaudioAudioService : IAudioService
{
    private readonly object _gate = new();
    private WasapiLoopbackCapture? _loopback;
    private WasapiCapture? _mic;
    private MMDevice? _renderDevice;
    private CancellationTokenSource? _monCts;
    private Services.AudioOptions? _options;
    private volatile bool _sessionActive;
    private volatile bool _hasMatchingSessions;
    private DateTime _lastSessionActivity = DateTime.MinValue;
    private const int SessionTailMs = 750;
    public bool IsCapturing { get; private set; }
    public event Action<AudioFrame>? OnFrame;
    public event Action<double>? OnLevel;
    public event Action? OnSilenceDetected;

    public Task StartAsync(AudioOptions options)
    {
        lock (_gate)
        {
            if (IsCapturing) return Task.CompletedTask;
            _options = options;
            _lastSessionActivity = DateTime.MinValue;
            switch (options.Source)
            {
                case AudioSourceKind.System:
                case AudioSourceKind.PerApp:
                    StartLoopback(options.Device, options.EndpointId);
                    break;
                case AudioSourceKind.Microphone:
                    StartMicrophone(options.Device, options.EndpointId);
                    break;
            }
            IsCapturing = true;
            if (options.Source != AudioSourceKind.Microphone)
            {
                StartSessionMonitor(options.Session);
            }
        }
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        lock (_gate)
        {
            IsCapturing = false;
            _monCts?.Cancel();
            _monCts?.Dispose();
            _monCts = null;
            if (_loopback is not null)
            {
                _loopback.DataAvailable -= OnData;
                _loopback.StopRecording();
                _loopback.Dispose();
                _loopback = null;
            }
            if (_mic is not null)
            {
                _mic.DataAvailable -= OnData;
                _mic.StopRecording();
                _mic.Dispose();
                _mic = null;
            }
            _renderDevice?.Dispose();
            _renderDevice = null;
        }
        return Task.CompletedTask;
    }

    private void StartLoopback(DevicePreference devicePref, string? endpointId)
    {
        var enumerator = new MMDeviceEnumerator();
        MMDevice device;
        if (!string.IsNullOrWhiteSpace(endpointId))
        {
            device = enumerator.GetDevice(endpointId);
        }
        else
        {
            var role = devicePref == DevicePreference.Communications ? Role.Communications : Role.Multimedia;
            device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, role);
        }
        _renderDevice = device;
        try
        {
            _loopback = new WasapiLoopbackCapture(device);
            _loopback.DataAvailable += OnData;
            _loopback.RecordingStopped += (s, e) => { };
            _loopback.StartRecording();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Unable to start system audio capture. Verify an output device is available.", ex);
        }
    }

    private void StartMicrophone(DevicePreference devicePref, string? endpointId)
    {
        var enumerator = new MMDeviceEnumerator();
        MMDevice device;
        if (!string.IsNullOrWhiteSpace(endpointId))
        {
            device = enumerator.GetDevice(endpointId);
        }
        else
        {
            var role = devicePref == DevicePreference.Communications ? Role.Communications : Role.Multimedia;
            device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, role);
        }
        try
        {
            _mic = new WasapiCapture(device);
            _mic.DataAvailable += OnData;
            _mic.StartRecording();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Unable to activate the selected microphone. Check audio permissions and default devices.", ex);
        }
    }

    private readonly byte[] _work = Array.Empty<byte>();

    private void OnData(object? sender, WaveInEventArgs e)
    {
        if (!IsCapturing) return;
        // Convert to float mono and forward. This naïve downmix is a placeholder.
        // WasapiCapture/WasapiLoopbackCapture don't always implement IWaveIn; use sender directly.
        var format = (sender as WasapiCapture)?.WaveFormat ?? (sender as WasapiLoopbackCapture)?.WaveFormat;
        if (format is null) return;
        var samples = BytesToMonoFloat(e.Buffer, e.BytesRecorded, format);
        // Level meter (RMS)
        double sum = 0;
        for (int i = 0; i < samples.Length; i++) { var v = samples[i]; sum += v * v; }
        double rms = Math.Sqrt(sum / Math.Max(1, samples.Length));
        var normalized = Math.Min(1.0, rms * 4);
        OnLevel?.Invoke(normalized);
        if (normalized < 0.02)
        {
            OnSilenceDetected?.Invoke();
        }
        // Session gating (PerApp only) while still allowing short post-activity tails
        if (_options?.Source == AudioSourceKind.PerApp)
        {
            if (!_hasMatchingSessions)
            {
                return;
            }
            var tailActive = _lastSessionActivity != DateTime.MinValue &&
                (DateTime.UtcNow - _lastSessionActivity).TotalMilliseconds <= SessionTailMs;
            if (!_sessionActive && !tailActive)
            {
                return;
            }
        }
        OnFrame?.Invoke(new AudioFrame { Samples = samples, SampleRate = format.SampleRate });
    }

    private static float[] BytesToMonoFloat(byte[] buffer, int count, WaveFormat fmt)
    {
        if (fmt.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            var floats = new float[count / 4];
            Buffer.BlockCopy(buffer, 0, floats, 0, count);
            if (fmt.Channels == 1) return floats;
            return DownmixToMono(floats, fmt.Channels);
        }
        if (fmt.BitsPerSample == 16)
        {
            int frames = count / (2 * fmt.Channels);
            var mono = new float[frames];
            int idx = 0;
            for (int i = 0; i < frames; i++)
            {
                float sum = 0;
                for (int ch = 0; ch < fmt.Channels; ch++)
                {
                    short s = BitConverter.ToInt16(buffer, idx);
                    sum += s / 32768f;
                    idx += 2;
                }
                mono[i] = sum / fmt.Channels;
            }
            return mono;
        }
        // Fallback
        return Array.Empty<float>();
    }

    private static float[] DownmixToMono(float[] interleaved, int channels)
    {
        if (channels <= 1) return interleaved;
        int frames = interleaved.Length / channels;
        var mono = new float[frames];
        int idx = 0;
        for (int i = 0; i < frames; i++)
        {
            float sum = 0;
            for (int ch = 0; ch < channels; ch++) sum += interleaved[idx++];
            mono[i] = sum / channels;
        }
        return mono;
    }

    public IReadOnlyList<AudioEndpoint> ListEndpoints(AudioSourceKind source)
    {
        var flow = (source == AudioSourceKind.Microphone) ? DataFlow.Capture : DataFlow.Render;
        var list = new List<AudioEndpoint>();
        try
        {
            var enumerator = new MMDeviceEnumerator();
            // Always include logical defaults first for UX clarity
            try
            {
                var defMultimedia = enumerator.GetDefaultAudioEndpoint(flow, Role.Multimedia);
                if (defMultimedia is not null)
                    list.Add(new AudioEndpoint { Id = defMultimedia.ID, Name = defMultimedia.FriendlyName + " (Default)" });
            }
            catch { }
            try
            {
                var defComm = enumerator.GetDefaultAudioEndpoint(flow, Role.Communications);
                if (defComm is not null && !list.Any(e => e.Id == defComm.ID))
                    list.Add(new AudioEndpoint { Id = defComm.ID, Name = defComm.FriendlyName + " (Communications)" });
            }
            catch { }
            // Then list all active endpoints
            foreach (var dev in new MMDeviceEnumerator().EnumerateAudioEndPoints(flow, DeviceState.Active))
            {
                if (!list.Any(e => e.Id == dev.ID))
                    list.Add(new AudioEndpoint { Id = dev.ID, Name = dev.FriendlyName });
            }
        }
        catch { }
        return list;
    }

    private void StartSessionMonitor(SessionHint hint)
    {
        _monCts = new CancellationTokenSource();
        var ct = _monCts.Token;
        _ = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    UpdateSessionActivity(hint);
                }
                catch { }
                await Task.Delay(150, ct);
            }
        }, ct);
    }

    private void UpdateSessionActivity(SessionHint hint)
    {
        if (_renderDevice is null) { _hasMatchingSessions = false; _sessionActive = false; return; }
        var sessions = _renderDevice.AudioSessionManager?.Sessions;
        if (sessions is null) { _hasMatchingSessions = false; _sessionActive = false; return; }
        bool hasMatch = false;
        bool active = false;
        var prefer = _options?.PreferredProcessName?.ToLowerInvariant();
        for (int i = 0; i < sessions.Count; i++)
        {
            var s = sessions[i];
            string proc = string.Empty;
            try
            {
                var pi = s.GetType().GetProperty("Process");
                if (pi != null)
                {
                    var p = pi.GetValue(s) as System.Diagnostics.Process;
                    if (p != null) proc = (p.ProcessName ?? string.Empty).ToLowerInvariant();
                }
            }
            catch { }
            bool match = false;
            if (!string.IsNullOrEmpty(prefer))
            {
                match = proc.Contains(prefer);
            }
            else if (hint != SessionHint.None)
            {
                match = IsHintMatch(proc, hint);
            }
            if (match)
            {
                hasMatch = true;
                try
                {
                    var meter = s.AudioMeterInformation?.MasterPeakValue ?? 0f;
                    if (meter > 0.02f) active = true;
                }
                catch { }
            }
        }
        _hasMatchingSessions = hasMatch;
        if (active)
        {
            _lastSessionActivity = DateTime.UtcNow;
            _sessionActive = true;
        }
        else
        {
            _sessionActive = false;
        }
    }

    private static bool IsHintMatch(string proc, SessionHint hint)
    {
        if (string.IsNullOrEmpty(proc)) return false;
        return hint switch
        {
            SessionHint.Teams => proc.Contains("teams"),
            SessionHint.Zoom => proc.Contains("zoom"),
            SessionHint.Meet => proc.Contains("chrome") || proc.Contains("msedge") || proc.Contains("firefox") || proc.Contains("brave"),
            SessionHint.Browser => proc.Contains("chrome") || proc.Contains("msedge") || proc.Contains("firefox") || proc.Contains("brave"),
            _ => false
        };
    }
}
