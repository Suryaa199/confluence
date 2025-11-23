using System.Text.Json;

namespace InterviewCopilot.Services;

public sealed class FileStoryRepository : IStoryRepository
{
    private static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InterviewCopilot");
    private static readonly string FilePath = Path.Combine(Dir, "stories.jsonl");

    public async Task SaveAsync(string question, string answer, DateTimeOffset at)
    {
        Directory.CreateDirectory(Dir);
        var entry = new { at = at, q = question, a = answer };
        await File.AppendAllTextAsync(FilePath, JsonSerializer.Serialize(entry) + "\n");
    }

    public async Task<IReadOnlyList<(DateTimeOffset At, string Question, string Answer)>> SearchAsync(string query)
    {
        var results = new List<(DateTimeOffset, string, string)>();
        if (!File.Exists(FilePath)) return results;
        await foreach (var line in ReadLinesAsync(FilePath))
        {
            try
            {
                var doc = JsonDocument.Parse(line);
                var at = doc.RootElement.GetProperty("at").GetDateTimeOffset();
                var q = doc.RootElement.GetProperty("q").GetString() ?? string.Empty;
                var a = doc.RootElement.GetProperty("a").GetString() ?? string.Empty;
                if (q.Contains(query, StringComparison.OrdinalIgnoreCase) || a.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add((at, q, a));
                }
            }
            catch { }
        }
        return results;
    }

    private static async IAsyncEnumerable<string> ReadLinesAsync(string path)
    {
        using var fs = File.OpenRead(path);
        using var sr = new StreamReader(fs);
        string? line;
        while ((line = await sr.ReadLineAsync()) is not null) yield return line;
    }
}

public sealed class DiskOfflineSpooler : IOfflineSpooler
{
    private static readonly string SpoolDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InterviewCopilot", "spool");

    public void Enqueue(byte[] wavBytes)
    {
        Directory.CreateDirectory(SpoolDir);
        var name = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + ".wav";
        File.WriteAllBytes(Path.Combine(SpoolDir, name), wavBytes);
    }

    public async Task FlushAsync(CancellationToken ct)
    {
        if (!Directory.Exists(SpoolDir)) return;
        var files = Directory.GetFiles(SpoolDir, "*.wav").OrderBy(f => f).ToList();
        foreach (var f in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var bytes = await File.ReadAllBytesAsync(f, ct);
                // In a complete pipeline, route to ASR service
                // For now, just delete after read to avoid buildup
                File.Delete(f);
            }
            catch { }
        }
    }
}

