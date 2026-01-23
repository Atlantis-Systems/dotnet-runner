namespace Rot.Logging;

public class ConsoleLogger : ITaskLogger
{
    private readonly LogLevel _minimumLevel;
    private readonly bool _useColors;

    public ConsoleLogger(LogLevel minimumLevel = LogLevel.Info, bool useColors = true)
    {
        _minimumLevel = minimumLevel;
        _useColors = useColors;
    }

    public void Log(LogLevel level, string message, params object[] args)
    {
        if (level < _minimumLevel) return;

        var formattedMessage = args.Length > 0 ? FormatMessage(message, args) : message;
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var levelStr = GetLevelString(level);

        if (_useColors)
        {
            var color = GetLevelColor(level);
            Console.ForegroundColor = color;
            Console.WriteLine($"[{timestamp}] [{levelStr}] {formattedMessage}");
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine($"[{timestamp}] [{levelStr}] {formattedMessage}");
        }
    }

    public void Debug(string message, params object[] args) => Log(LogLevel.Debug, message, args);
    public void Info(string message, params object[] args) => Log(LogLevel.Info, message, args);
    public void Warning(string message, params object[] args) => Log(LogLevel.Warning, message, args);
    public void Error(string message, params object[] args) => Log(LogLevel.Error, message, args);

    private static string FormatMessage(string message, object[] args)
    {
        // Simple named parameter replacement: {ParamName} -> value
        var result = message;
        for (int i = 0; i < args.Length; i++)
        {
            var placeholder = $"{{{i}}}";
            if (result.Contains(placeholder))
            {
                result = result.Replace(placeholder, args[i]?.ToString() ?? "null");
            }
        }

        // Also handle named placeholders like {TaskName}
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

    private static string GetLevelString(LogLevel level)
    {
        return level switch
        {
            LogLevel.Debug => "DBG",
            LogLevel.Info => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            _ => "???"
        };
    }

    private static ConsoleColor GetLevelColor(LogLevel level)
    {
        return level switch
        {
            LogLevel.Debug => ConsoleColor.Gray,
            LogLevel.Info => ConsoleColor.White,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            _ => ConsoleColor.White
        };
    }
}
