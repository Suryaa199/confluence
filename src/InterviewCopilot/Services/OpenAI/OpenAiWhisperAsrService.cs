using System.Net.Http;
using System.Net.Http.Headers;

namespace InterviewCopilot.Services.OpenAI;

public sealed class OpenAiWhisperAsrService : IAsrService
{
    private readonly System.Net.Http.HttpClient _http;

    public OpenAiWhisperAsrService(string apiKey)
    {
        _http = new System.Net.Http.HttpClient { BaseAddress = new Uri("https://api.openai.com/") };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<string> TranscribeChunkAsync(byte[] wavBytes, CancellationToken ct)
    {
        using var content = new System.Net.Http.MultipartFormDataContent();
        var file = new System.Net.Http.ByteArrayContent(wavBytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
        content.Add(file, "file", "chunk.wav");
        content.Add(new System.Net.Http.StringContent("whisper-1"), "model");
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
