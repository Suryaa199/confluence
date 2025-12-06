using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;

namespace InterviewCopilot.Services;

public static class LogService
{
    private static readonly ConcurrentQueue<string> _buffer = new();
    private static readonly object _fileGate = new();
    private static string? _logPath;
    public static string LogPath => _logPath ?? string.Empty;

    public static event Action<string>? OnLog;

    public static void Initialize()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InterviewCopilot");
        Directory.CreateDirectory(dir);
        _logPath = Path.Combine(dir, "logs.txt");
        try
        {
            File.WriteAllText(_logPath, $"=== Session {DateTimeOffset.Now:u} ==={Environment.NewLine}");
        }
        catch { }
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:u} [{level}] {message}";
        _buffer.Enqueue(line);
        OnLog?.Invoke(line);
        if (_logPath is null) return;
        lock (_fileGate)
        {
            try
            {
                File.AppendAllText(_logPath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }
    }

    public static string[] GetRecent(int max = 200)
    {
        return _buffer.Reverse().Take(max).Reverse().ToArray();
    }
}
