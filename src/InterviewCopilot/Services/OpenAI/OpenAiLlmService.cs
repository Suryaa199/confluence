using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace InterviewCopilot.Services.OpenAI;

public sealed class OpenAiLlmService : ILlmService
{
    private readonly HttpClient _http;
    private readonly string _model;

    public OpenAiLlmService(string apiKey, string model = "gpt-4o-mini")
    {
        _model = model;
        _http = new HttpClient
        {
            BaseAddress = new Uri("https://api.openai.com/")
        };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async IAsyncEnumerable<string> StreamAnswerAsync(string question, string context, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "v1/chat/completions");
        req.Headers.Accept.Clear();
        req.Headers.Accept.ParseAdd("text/event-stream");

        var body = new
        {
            model = _model,
            stream = true,
            messages = new object[]
            {
                new { role = "system", content = "You are an interview copilot. Be concise, structured, and include key points, concrete examples, and CLI commands when relevant." },
                new { role = "user", content = $"Context:\n{context}\n\nQuestion:\n{question}" }
            }
        };
        req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        res.EnsureSuccessStatusCode();
        using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            ct.ThrowIfCancellationRequested();
            if (line.StartsWith(":")) continue; // comment
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data:")) continue;
            var payload = line.Substring(5).Trim();
            if (payload == "[DONE]") yield break;
            try
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;
                var delta = root.GetProperty("choices")[0].GetProperty("delta");
                if (delta.TryGetProperty("content", out var content))
                {
                    var t = content.GetString();
                    if (!string.IsNullOrEmpty(t)) yield return t;
                }
            }
            catch { /* swallow parse errors for robustness */ }
        }
    }
}

