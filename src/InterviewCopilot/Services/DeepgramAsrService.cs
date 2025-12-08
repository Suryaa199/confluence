using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace InterviewCopilot.Services;

public sealed class DeepgramAsrService : IAsrService, IDisposable
{
    private readonly HttpClient _http;
    private readonly string _model;

    public DeepgramAsrService(string apiKey, string model = "nova-2-general", string? baseUrl = null)
    {
        _model = string.IsNullOrWhiteSpace(model) ? "nova-2-general" : model;
        var url = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.deepgram.com" : baseUrl;
        if (!url.EndsWith("/")) url += "/";
        _http = new HttpClient { BaseAddress = new Uri(url) };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", apiKey);
    }

    public async Task<string> TranscribeChunkAsync(byte[] wavBytes, CancellationToken ct)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            using var content = new ByteArrayContent(wavBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            var url = $"v1/listen?model={_model}&language=en&punctuate=true&smart_format=false";
            using var res = await _http.PostAsync(url, content, ct);
            if ((int)res.StatusCode == 429 || (int)res.StatusCode >= 500)
            {
                await Task.Delay(300 * (int)Math.Pow(2, attempt), ct);
                continue;
            }
            res.EnsureSuccessStatusCode();
            var json = await res.Content.ReadAsStringAsync(ct);
            try
            {
                using var doc = JsonDocument.Parse(json);
                var results = doc.RootElement.GetProperty("results");
                var alt = results.GetProperty("channels")[0]
                    .GetProperty("alternatives")[0];
                return alt.GetProperty("transcript").GetString() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
        return string.Empty;
    }

    public void Dispose() => _http.Dispose();
}
