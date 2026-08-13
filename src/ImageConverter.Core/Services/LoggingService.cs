using System;
using System.IO;
using System.Text;

namespace ImageConverter.Core.Services;

public sealed class LoggingService(AppPaths paths) : ILoggingService
{
    private readonly object _gate = new object();

    public void Info(string operation, string message, string? filePath = null)
    {
        Write("INFO", operation, message, filePath, null);
    }

    public void Error(string operation, Exception exception, string? filePath = null, string? message = null)
    {
        Write("ERROR", operation, message ?? exception.Message, filePath, exception);
    }

    private void Write(string level, string operation, string message, string? filePath, Exception? exception)
    {
        string path = Path.Combine(paths.Logs, $"ImageConverter-{DateTime.Now:yyyy-MM-dd}.log");
        StringBuilder stringBuilder = new StringBuilder().Append(DateTimeOffset.Now.ToString("O")).Append(" | ").Append(level)
            .Append(" | ")
            .Append(operation)
            .Append(" | ")
            .Append(message.ReplaceLineEndings(" "));
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            stringBuilder.Append(" | File=").Append(filePath);
        }
        if (exception != null)
        {
            stringBuilder.AppendLine().Append(exception);
        }
        lock (_gate)
        {
            File.AppendAllText(path, stringBuilder.AppendLine().ToString(), Encoding.UTF8);
        }
    }
}
