using System.Text;
using System.Text.Json;

namespace InterviewCopilot.Services.Local;

public sealed class OllamaLlmService : ILlmService
{
    private readonly System.Net.Http.HttpClient _http;
    private readonly string _model;

    public OllamaLlmService(string baseUrl, string model)
    {
        _model = model;
        _http = new System.Net.Http.HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
    }

    public async IAsyncEnumerable<string> StreamAnswerAsync(string question, string context, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "api/chat");
        var body = new
        {
            model = _model,
            stream = true,
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = "You are Surya's live DevSecOps interview copilot. Follow this exact structure:\n" +
                              "1) Provide 5-10 concise lines that answer the question using resume/JD context. Mention Azure, AKS/Kubernetes, Terraform, DevOps/DevSecOps, CI/CD, Docker, Keycloak, NGINX, ACR, Python automation, OpenAI/LLMs whenever relevant.\n" +
                              "2) Add a line starting with \"Mini Example:\" describing a concrete recent win with measurable impact.\n" +
                              "3) Add a line starting with \"CLI Example:\" followed by 1-3 relevant commands (az, kubectl, terraform, docker, trivy, etc.).\n" +
                              "Use first-person voice (\"I\"), highlight impact, keep answers interview-ready, no markdown bullets."
                },
                new { role = "user", content = $"Context:\n{context}\n\nQuestion:\n{question}" }
            }
        };
        req.Content = new System.Net.Http.StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var res = await _http.SendAsync(req, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, ct);
        res.EnsureSuccessStatusCode();
        using var stream = await res.Content.ReadAsStreamAsync(ct);
        using var reader = new System.IO.StreamReader(stream);
        string? line;
        while ((line = await reader.ReadLineAsync()) is not null)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("{")) continue; // ollama streams json objects per line
            string? tokenToYield = null;
            bool doneFlag = false;
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (root.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var content))
                {
                    var t = content.GetString();
                    if (!string.IsNullOrEmpty(t)) tokenToYield = t;
                }
                if (root.TryGetProperty("done", out var done) && done.GetBoolean()) doneFlag = true;
            }
            catch { }
            if (!string.IsNullOrEmpty(tokenToYield)) yield return tokenToYield;
            if (doneFlag) yield break;
        }
    }

    public async Task<IReadOnlyList<string>> GenerateFollowUpsAsync(string question, string context, CancellationToken ct)
    {
        var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, "api/chat");
        var prompt = "Return strictly JSON: {\"followups\":[\"...\"]} with 3-5 concise follow-up questions.";
        var body = new
        {
            model = _model,
            stream = false,
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
            var content = doc.RootElement.GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrEmpty(content)) return Array.Empty<string>();
            using var inner = JsonDocument.Parse(content);
            if (inner.RootElement.TryGetProperty("followups", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                return arr.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList();
            }
        }
        catch { }
        return Array.Empty<string>();
    }
}
