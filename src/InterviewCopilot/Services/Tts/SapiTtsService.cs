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
                sp.Speak(text, 0);
            }
            catch { }
        }, ct);
    }
}
