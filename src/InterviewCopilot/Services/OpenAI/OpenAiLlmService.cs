using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace InterviewCopilot.Services.OpenAI;

public sealed class OpenAiLlmService : ILlmService
{
    private readonly System.Net.Http.HttpClient _http;
    private readonly string _model;

    public OpenAiLlmService(string apiKey, string model = "gpt-4o-mini")
    {
        _model = model;
        _http = new System.Net.Http.HttpClient
        {
            BaseAddress = new Uri("https://api.openai.com/")
        };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async IAsyncEnumerable<string> StreamAnswerAsync(string question, string context, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "v1/chat/completions");
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
        req.Content = new System.Net.Http.StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        res.EnsureSuccessStatusCode();
        using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var reader = new System.IO.StreamReader(stream);
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            ct.ThrowIfCancellationRequested();
            if (line.StartsWith(":")) continue; // comment
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data:")) continue;
            var payload = line.Substring(5).Trim();
            if (payload == "[DONE]") yield break;
            string? tokenToYield = null;
            try
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;
                var delta = root.GetProperty("choices")[0].GetProperty("delta");
                if (delta.TryGetProperty("content", out var content))
                {
                    var t = content.GetString();
                    if (!string.IsNullOrEmpty(t)) tokenToYield = t;
                }
            }
            catch { /* swallow parse errors for robustness */ }
            if (!string.IsNullOrEmpty(tokenToYield)) yield return tokenToYield;
        }
    }

    public async Task<IReadOnlyList<string>> GenerateFollowUpsAsync(string question, string context, CancellationToken ct)
    {
        var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "v1/chat/completions");
        var prompt = "Return 3-5 concise interview follow-up questions as a JSON array of strings. No commentary.";
        var body = new
        {
            model = _model,
            stream = false,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = prompt },
                new { role = "user", content = $"Context:\n{context}\n\nQuestion:\n{question}" }
            }
        };
        req.Content = new System.Net.Http.StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var res = await _http.SendAsync(req, ct);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync(ct);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrEmpty(content)) return Array.Empty<string>();
            using var inner = JsonDocument.Parse(content);
            if (inner.RootElement.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                var list = new List<string>();
                foreach (var el in arr.EnumerateArray())
                {
                    if (el.ValueKind == JsonValueKind.String) list.Add(el.GetString()!);
                }
                if (list.Count > 0) return list;
            }
            // fallback: try parsing as array directly
            if (inner.RootElement.ValueKind == JsonValueKind.Array)
            {
                return inner.RootElement.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList();
            }
        }
        catch { }
        return Array.Empty<string>();
    }
}
