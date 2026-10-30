using System.Text;

namespace HocoKeepAlive.Diagnostics;

public enum LogSeverity
{
    Info,
    Warning,
    Error
}

public sealed record LogEntry(DateTime Timestamp, LogSeverity Severity, string EventType, string Message, string? Details = null);

public static class LogService
{
    private static readonly object _lock = new();
    private static readonly List<LogEntry> _entries = new();
    private static string? _logFilePath;

    public static event Action<LogEntry>? EntryAdded;

    static LogService()
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "KeepAlive", "Logs");
            Directory.CreateDirectory(folder);
            _logFilePath = Path.Combine(folder, $"session_{DateTime.UtcNow:yyyyMMdd}.log");
        }
        catch { }

        Log(LogSeverity.Info, "Application", "Keep Alive diagnostic session initialized.");
    }

    public static string LogDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KeepAlive", "Logs");

    public static void Log(LogSeverity severity, string eventType, string message, string? details = null)
    {
        var entry = new LogEntry(DateTime.UtcNow, severity, eventType, message, details);

        lock (_lock)
        {
            _entries.Add(entry);
            if (_entries.Count > 1000)
            {
                _entries.RemoveAt(0);
            }

            try
            {
                if (!string.IsNullOrEmpty(_logFilePath))
                {
                    string line = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Severity}] [{entry.EventType}] {entry.Message}";
                    if (!string.IsNullOrEmpty(entry.Details))
                    {
                        line += $" | Details: {entry.Details}";
                    }
                    File.AppendAllLines(_logFilePath, new[] { line });
                }
            }
            catch { }
        }

        EntryAdded?.Invoke(entry);
    }

    public static IReadOnlyList<LogEntry> GetEntries()
    {
        lock (_lock)
        {
            return _entries.ToList();
        }
    }

    public static void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
            try
            {
                if (!string.IsNullOrEmpty(_logFilePath) && File.Exists(_logFilePath))
                {
                    File.WriteAllText(_logFilePath, string.Empty);
                }
            }
            catch { }
        }
        Log(LogSeverity.Info, "Diagnostics", "Logs cleared by user.");
    }

    public static string ExportReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Keep Alive Diagnostic Report ===");
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"OS Version: {Environment.OSVersion}");
        sb.AppendLine($"64-bit OS: {Environment.Is64BitOperatingSystem}");
        sb.AppendLine($"Processor Count: {Environment.ProcessorCount}");
        sb.AppendLine("=========================================\n");

        lock (_lock)
        {
            foreach (var e in _entries)
            {
                sb.AppendLine($"[{e.Timestamp:yyyy-MM-dd HH:mm:ss}] [{e.Severity}] [{e.EventType}] {e.Message}");
                if (!string.IsNullOrEmpty(e.Details))
                {
                    sb.AppendLine($"    {e.Details}");
                }
            }
        }

        return sb.ToString();
    }
}
