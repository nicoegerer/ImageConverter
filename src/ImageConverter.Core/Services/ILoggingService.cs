using System;

namespace ImageConverter.Core.Services;

public interface ILoggingService
{
    void Info(string operation, string message, string? filePath = null);

    void Error(string operation, Exception exception, string? filePath = null, string? message = null);
}
