namespace InterviewCopilot.Services.Audio;

public sealed class VadEnergyGate : IVadService
{
    private bool _enabled;
    private int _minVoiceMs = 200;
    private int _maxSilenceMs = 600;
    private const float Threshold = 0.015f; // tune later

    public bool Enabled => _enabled;

    public void Configure(bool enabled, int minVoiceMs, int maxSilenceMs)
    {
        _enabled = enabled;
        _minVoiceMs = minVoiceMs;
        _maxSilenceMs = maxSilenceMs;
    }

    public bool IsSpeech(ReadOnlySpan<float> monoPcm)
    {
        if (!_enabled) return true;
        double sum = 0;
        for (int i = 0; i < monoPcm.Length; i++)
        {
            var v = monoPcm[i];
            sum += v * v;
        }
        var rms = Math.Sqrt(sum / Math.Max(1, monoPcm.Length));
        return rms > Threshold;
    }
}

