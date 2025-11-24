using System.Speech.Synthesis;
using System.IO;
using NAudio.CoreAudioApi;
using NAudio.Wave;

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
            using var synth = new SpeechSynthesizer();
            if (!_useCommunications)
            {
                synth.Speak(text);
                return;
            }
            using var ms = new MemoryStream();
            synth.SetOutputToWaveStream(ms);
            synth.Speak(text);
            ms.Position = 0;
            using var reader = new WaveFileReader(ms);
            var enumerator = new MMDeviceEnumerator();
            var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Communications);
            using var wasapi = new WasapiOut(device, AudioClientShareMode.Shared, false, 100);
            using var source = new WaveChannel32(reader);
            wasapi.Init(source);
            wasapi.Play();
            while (wasapi.PlaybackState == PlaybackState.Playing)
            {
                Thread.Sleep(50);
            }
        }, ct);
    }
}
