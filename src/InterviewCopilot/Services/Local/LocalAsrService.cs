using System.Text.Json;

namespace InterviewCopilot.Services.Local;

public sealed class LocalAsrService : IAsrService
{
    private readonly System.Net.Http.HttpClient _http;
    private readonly string _model;
    public LocalAsrService(string baseUrl, string model)
    {
        _model = model;
        _http = new System.Net.Http.HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
    }

    public async Task<string> TranscribeChunkAsync(byte[] wavBytes, CancellationToken ct)
    {
        using var content = new System.Net.Http.MultipartFormDataContent();
        var file = new System.Net.Http.ByteArrayContent(wavBytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
        content.Add(file, "file", "chunk.wav");
        content.Add(new System.Net.Http.StringContent(_model), "model");
        content.Add(new System.Net.Http.StringContent("en"), "language");
        using var res = await _http.PostAsync("transcribe", content, ct);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync(ct);
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("text", out var t)) return t.GetString() ?? string.Empty;
        }
        catch { }
        return string.Empty;
    }
}
