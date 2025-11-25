using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace InterviewCopilot.Services.Audio;

// Silero VAD using ONNXRuntime with stateful h/c tensors when available.
// Expects 16kHz mono float PCM. Maintains a hold period for min voice and max silence.
public sealed class SileroVadService : IVadService, IDisposable
{
    private const int SampleRate = 16000;
    private int _windowMs = 30; // typical Silero window

    private bool _enabled;
    private int _minVoiceMs = 200;
    private int _maxSilenceMs = 600;
    private float _speechThreshold = 0.5f; // default; can be tuned via settings

    private InferenceSession? _session;
    private string _inputName = "input";
    private string? _srName;
    private string? _hName;
    private string? _cName;
    private string _outName = "output";
    private string? _hnName;
    private string? _cnName;

    private DenseTensor<float>? _h;
    private DenseTensor<float>? _c;
    private DateTime _lastSpeech = DateTime.MinValue;
    private DateTime _lastVoiceStart = DateTime.MinValue;
    private float[] _residual = Array.Empty<float>();

    public bool Enabled => _enabled && _session is not null;

    public SileroVadService()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "models", "silero_vad.onnx");
            if (File.Exists(path))
            {
                _session = new InferenceSession(path, new SessionOptions());
                // probe input/output names
                foreach (var kv in _session.InputMetadata)
                {
                    var name = kv.Key;
                    if (name.Equals("input", StringComparison.OrdinalIgnoreCase)) _inputName = name;
                    else if (name.Equals("sr", StringComparison.OrdinalIgnoreCase)) _srName = name;
                    else if (name.Equals("h", StringComparison.OrdinalIgnoreCase)) _hName = name;
                    else if (name.Equals("c", StringComparison.OrdinalIgnoreCase)) _cName = name;
                }
                foreach (var kv in _session.OutputMetadata)
                {
                    var name = kv.Key;
                    if (name.Equals("output", StringComparison.OrdinalIgnoreCase)) _outName = name;
                    else if (name.Equals("hn", StringComparison.OrdinalIgnoreCase)) _hnName = name;
                    else if (name.Equals("cn", StringComparison.OrdinalIgnoreCase)) _cnName = name;
                }
            }
        }
        catch { }
    }

    public void Configure(bool enabled, int minVoiceMs, int maxSilenceMs)
    {
        _enabled = enabled;
        _minVoiceMs = minVoiceMs;
        _maxSilenceMs = maxSilenceMs;
    }

    public void SetParameters(int windowMs, float threshold)
    {
        _windowMs = Math.Clamp(windowMs, 10, 100);
        _speechThreshold = Math.Clamp(threshold, 0.05f, 0.95f);
    }

    public bool IsSpeech(ReadOnlySpan<float> monoPcm)
    {
        if (!_enabled || _session is null)
        {
            // simple energy fallback
            double sum = 0;
            for (int i = 0; i < monoPcm.Length; i++) { var v = monoPcm[i]; sum += v * v; }
            var rms = Math.Sqrt(sum / Math.Max(1, monoPcm.Length));
            return rms > 0.0125f;
        }

        // accumulate residual + current into windows
        int windowSamples = Math.Max(1, SampleRate * _windowMs / 1000);
        float prob = 0f;
        if (_residual.Length == 0 && monoPcm.Length < windowSamples)
        {
            _residual = monoPcm.ToArray();
        }
        else
        {
            var buf = new float[_residual.Length + monoPcm.Length];
            if (_residual.Length > 0) Array.Copy(_residual, buf, _residual.Length);
            monoPcm.CopyTo(buf.AsSpan(_residual.Length));
            int idx = 0;
            while (idx + windowSamples <= buf.Length)
            {
                var win = new ReadOnlySpan<float>(buf, idx, windowSamples);
                prob = Math.Max(prob, Infer(win));
                idx += windowSamples;
            }
            int remain = buf.Length - idx;
            _residual = remain > 0 ? buf.AsSpan(idx, remain).ToArray() : Array.Empty<float>();
        }

        var now = DateTime.UtcNow;
        bool speechNow = prob >= _speechThreshold;
        if (speechNow)
        {
            _lastSpeech = now;
            if (_lastVoiceStart == DateTime.MinValue) _lastVoiceStart = now;
            return true; // in speech
        }

        // hold logic: if recently in speech, continue for maxSilenceMs
        if (_lastSpeech != DateTime.MinValue && (now - _lastSpeech).TotalMilliseconds < _maxSilenceMs)
        {
            return true;
        }

        _lastVoiceStart = DateTime.MinValue;
        return false;
    }

    private float Infer(ReadOnlySpan<float> win)
    {
        if (_session is null) return 0f;

        // Prepare tensors
        // Support common layouts: [1,1,T] with optional sr(int64), h([2,1,64]), c([2,1,64])
        var input = new DenseTensor<float>(new[] { 1, 1, win.Length });
        for (int i = 0; i < win.Length; i++) input[0, 0, i] = win[i];
        var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(_inputName, input) };
        if (!string.IsNullOrEmpty(_srName)) inputs.Add(NamedOnnxValue.CreateFromTensor(_srName!, new DenseTensor<long>(new long[] { SampleRate }, Array.Empty<int>())));
        if (!string.IsNullOrEmpty(_hName) && _h is null) _h = new DenseTensor<float>(new[] { 2, 1, 64 });
        if (!string.IsNullOrEmpty(_cName) && _c is null) _c = new DenseTensor<float>(new[] { 2, 1, 64 });
        if (!string.IsNullOrEmpty(_hName) && _h is not null) inputs.Add(NamedOnnxValue.CreateFromTensor(_hName!, _h));
        if (!string.IsNullOrEmpty(_cName) && _c is not null) inputs.Add(NamedOnnxValue.CreateFromTensor(_cName!, _c));

        try
        {
            using var results = _session.Run(inputs);
            float prob = 0f;
            foreach (var r in results)
            {
                if (r.Name == _outName)
                {
                    var t = (r.AsEnumerable<float>()).ToArray();
                    if (t.Length > 0) prob = t[0];
                }
                else if (!string.IsNullOrEmpty(_hnName) && r.Name == _hnName)
                {
                    _h = (DenseTensor<float>)r.AsTensor<float>();
                }
                else if (!string.IsNullOrEmpty(_cnName) && r.Name == _cnName)
                {
                    _c = (DenseTensor<float>)r.AsTensor<float>();
                }
            }
            return prob;
        }
        catch
        {
            return 0f;
        }
    }

    public void Dispose()
    {
        _session?.Dispose();
        _session = null;
    }
}
