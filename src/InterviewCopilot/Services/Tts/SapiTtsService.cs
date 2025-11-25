using System;
using System.Threading;

namespace InterviewCopilot.Services.Tts;

public sealed class SapiTtsService : ITtsService
{
    private readonly bool _useCommunications;
    public SapiTtsService(bool useCommunications) { _useCommunications = useCommunications; }

    public async Task SpeakAsync(string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        await Task.Run(() =>
        {
            try
            {
                var type = Type.GetTypeFromProgID("SAPI.SpVoice");
                if (type == null) return;
                dynamic sp = Activator.CreateInstance(type)!;

                if (_useCommunications)
                {
                    try
                    {
                        // Map WASAPI Communications endpoint to a waveOut device by name.
                        var deviceName = GetCommunicationsDeviceFriendlyName();
                        int deviceId = FindWaveOutDeviceIdByName(deviceName);
                        if (deviceId < 0) deviceId = FindCommunicationsWaveOutDeviceId();
                        if (deviceId >= 0)
                        {
                            var outType = Type.GetTypeFromProgID("SAPI.SpMMAudioOut");
                            if (outType != null)
                            {
                                dynamic audio = Activator.CreateInstance(outType)!;
                                audio.DeviceId = deviceId;
                                sp.AudioOutputStream = audio;
                            }
                        }
                    }
                    catch { }
                }
                sp.Speak(text, 0);
            }
            catch { }
        }, ct);
    }

    private static int FindCommunicationsWaveOutDeviceId()
    {
        try
        {
            // Heuristic: choose waveOut device whose name contains 'communication' or 'comm'
            int count = NAudio.Wave.WaveOut.DeviceCount;
            for (int i = 0; i < count; i++)
            {
                var caps = NAudio.Wave.WaveOut.GetCapabilities(i);
                var name = caps.ProductName?.ToLowerInvariant() ?? string.Empty;
                if (name.Contains("communication")) return i;
                if (name.Contains("comm")) return i;
            }
        }
        catch { }
        return -1;
    }

    private static string GetCommunicationsDeviceFriendlyName()
    {
        try
        {
            var mm = new NAudio.CoreAudioApi.MMDeviceEnumerator();
            var dev = mm.GetDefaultAudioEndpoint(NAudio.CoreAudioApi.DataFlow.Render, NAudio.CoreAudioApi.Role.Communications);
            return dev.FriendlyName ?? string.Empty;
        }
        catch { }
        return string.Empty;
    }

    private static int FindWaveOutDeviceIdByName(string friendlyName)
    {
        if (string.IsNullOrWhiteSpace(friendlyName)) return -1;
        try
        {
            int count = NAudio.Wave.WaveOut.DeviceCount;
            for (int i = 0; i < count; i++)
            {
                var caps = NAudio.Wave.WaveOut.GetCapabilities(i);
                var name = caps.ProductName ?? string.Empty;
                if (friendlyName.Contains(name, StringComparison.OrdinalIgnoreCase) || name.Contains(friendlyName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
        }
        catch { }
        return -1;
    }
}
