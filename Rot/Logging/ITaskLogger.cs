namespace Rot.Logging;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public interface ITaskLogger
{
    void Log(LogLevel level, string message, params object[] args);
    void Debug(string message, params object[] args);
    void Info(string message, params object[] args);
    void Warning(string message, params object[] args);
    void Error(string message, params object[] args);
}
