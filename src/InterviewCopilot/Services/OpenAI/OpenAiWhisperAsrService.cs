using System.Net.Http.Headers;

namespace InterviewCopilot.Services.OpenAI;

public sealed class OpenAiWhisperAsrService : IAsrService
{
    private readonly HttpClient _http;

    public OpenAiWhisperAsrService(string apiKey)
    {
        _http = new HttpClient { BaseAddress = new Uri("https://api.openai.com/") };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<string> TranscribeChunkAsync(byte[] wavBytes, CancellationToken ct)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(wavBytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(file, "file", "chunk.wav");
        content.Add(new StringContent("whisper-1"), "model");
        using var res = await _http.PostAsync("v1/audio/transcriptions", content, ct);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync(ct);
        // Response shape: { text: "..." }
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("text", out var t))
            {
                return t.GetString() ?? string.Empty;
            }
        }
        catch { }
        return string.Empty;
    }
}

