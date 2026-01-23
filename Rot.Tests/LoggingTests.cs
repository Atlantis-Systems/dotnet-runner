using Rot.Logging;
using Xunit;

namespace Rot.Tests;

public class LoggingTests
{
    [Fact]
    public void ConsoleLogger_WithDebugLevel_LogsDebugMessages()
    {
        var logger = new ConsoleLogger(LogLevel.Debug, useColors: false);

        // Should not throw
        logger.Debug("Debug message");
        logger.Info("Info message");
        logger.Warning("Warning message");
        logger.Error("Error message");
    }

    [Fact]
    public void ConsoleLogger_WithErrorLevel_OnlyLogsErrors()
    {
        var logger = new ConsoleLogger(LogLevel.Error, useColors: false);

        // Should not throw
        logger.Debug("Debug message");
        logger.Info("Info message");
        logger.Warning("Warning message");
        logger.Error("Error message");
    }

    [Fact]
    public void NullLogger_DoesNothing()
    {
        var logger = NullLogger.Instance;

        // Should not throw
        logger.Debug("Debug message");
        logger.Info("Info message");
        logger.Warning("Warning message");
        logger.Error("Error message");
    }

    [Fact]
    public void FileLogger_WritesToFile()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            using (var logger = new FileLogger(tempFile, LogLevel.Debug))
            {
                logger.Info("Test message");
            }

            var content = File.ReadAllText(tempFile);
            Assert.Contains("Test message", content);
            Assert.Contains("[Info]", content);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void FileLogger_WithJsonFormat_WritesJson()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            using (var logger = new FileLogger(tempFile, LogLevel.Debug, asJson: true))
            {
                logger.Info("Test message");
            }

            var content = File.ReadAllText(tempFile);
            Assert.Contains("\"message\":\"Test message\"", content);
            Assert.Contains("\"level\":\"info\"", content);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData(LogLevel.Debug, "debug")]
    [InlineData(LogLevel.Info, "info")]
    [InlineData(LogLevel.Warning, "warning")]
    [InlineData(LogLevel.Error, "error")]
    public void FileLogger_JsonFormat_CorrectLevelString(LogLevel level, string expectedLevel)
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            using (var logger = new FileLogger(tempFile, LogLevel.Debug, asJson: true))
            {
                logger.Log(level, "Test");
            }

            var content = File.ReadAllText(tempFile);
            Assert.Contains($"\"level\":\"{expectedLevel}\"", content);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
