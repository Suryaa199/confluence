using System.Net.Http;
using System.Net.Http.Headers;

namespace InterviewCopilot.Services.OpenAI;

public sealed class OpenAiWhisperAsrService : IAsrService
{
    private readonly System.Net.Http.HttpClient _http;
    public string Model { get; set; } = "whisper-1";

    public OpenAiWhisperAsrService(string apiKey)
    {
        _http = new System.Net.Http.HttpClient { BaseAddress = new Uri("https://api.openai.com/") };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<string> TranscribeChunkAsync(byte[] wavBytes, CancellationToken ct)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            using var content = new System.Net.Http.MultipartFormDataContent();
            var file = new System.Net.Http.ByteArrayContent(wavBytes);
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
            content.Add(file, "file", "chunk.wav");
            content.Add(new System.Net.Http.StringContent(Model), "model");
            using var res = await _http.PostAsync("v1/audio/transcriptions", content, ct);
            if ((int)res.StatusCode == 429 || (int)res.StatusCode >= 500)
            {
                await Task.Delay(300 * (int)Math.Pow(2, attempt), ct);
                continue;
            }
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync(ct);
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("text", out var t))
                    return t.GetString() ?? string.Empty;
            }
            catch { }
            return string.Empty;
        }
        return string.Empty;
    }
}
