using System.Text.Json;

namespace Rot.Logging;

public class FileLogger : ITaskLogger, IDisposable
{
    private readonly string _filePath;
    private readonly LogLevel _minimumLevel;
    private readonly bool _asJson;
    private readonly StreamWriter _writer;
    private readonly object _lock = new();
    private bool _disposed;

    public FileLogger(string filePath, LogLevel minimumLevel = LogLevel.Info, bool asJson = false)
    {
        _filePath = filePath;
        _minimumLevel = minimumLevel;
        _asJson = asJson;
        _writer = new StreamWriter(filePath, append: true) { AutoFlush = true };
    }

    public void Log(LogLevel level, string message, params object[] args)
    {
        if (level < _minimumLevel) return;

        var formattedMessage = args.Length > 0 ? FormatMessage(message, args) : message;
        var timestamp = DateTime.UtcNow;

        string line;
        if (_asJson)
        {
            var logEntry = new
            {
                timestamp = timestamp.ToString("o"),
                level = level.ToString().ToLower(),
                message = formattedMessage
            };
            line = JsonSerializer.Serialize(logEntry);
        }
        else
        {
            line = $"[{timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {formattedMessage}";
        }

        lock (_lock)
        {
            _writer.WriteLine(line);
        }
    }

    public void Debug(string message, params object[] args) => Log(LogLevel.Debug, message, args);
    public void Info(string message, params object[] args) => Log(LogLevel.Info, message, args);
    public void Warning(string message, params object[] args) => Log(LogLevel.Warning, message, args);
    public void Error(string message, params object[] args) => Log(LogLevel.Error, message, args);

    private static string FormatMessage(string message, object[] args)
    {
        var result = message;
        var index = 0;
        while (result.Contains('{') && result.Contains('}') && index < args.Length)
        {
            var start = result.IndexOf('{');
            var end = result.IndexOf('}', start);
            if (start >= 0 && end > start)
            {
                var placeholder = result.Substring(start, end - start + 1);
                result = result.Replace(placeholder, args[index]?.ToString() ?? "null");
                index++;
            }
            else
            {
                break;
            }
        }
        return result;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _writer.Dispose();
            _disposed = true;
        }
    }
}

public class NullLogger : ITaskLogger
{
    public static readonly NullLogger Instance = new();

    public void Log(LogLevel level, string message, params object[] args) { }
    public void Debug(string message, params object[] args) { }
    public void Info(string message, params object[] args) { }
    public void Warning(string message, params object[] args) { }
    public void Error(string message, params object[] args) { }
}
