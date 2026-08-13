using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace ImageConverter.Core.Services;

public sealed class DiagnosticService(AppPaths paths)
{
    public string Write(string operation, Exception exception, string? filePath = null, int? exitCode = null, string? installerVersion = null)
    {
        int effectiveExitCode = exitCode ?? exception.HResult;
        string appVersion = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly())
            .GetName().Version?.ToString() ?? "unknown";
        string diagnosticPath = Path.Combine(paths.Diagnostics, "ImageConverter-Diagnostic.txt");

        var report = new StringBuilder()
            .AppendLine("Image Converter Diagnostic")
            .AppendLine($"Timestamp: {DateTimeOffset.Now:O}")
            .AppendLine($"App Version: {appVersion}")
            .AppendLine($"Windows Version: {Environment.OSVersion}")
            .AppendLine($".NET Runtime: {RuntimeInformation.FrameworkDescription}")
            .AppendLine($"Process Architecture: {RuntimeInformation.ProcessArchitecture}")
            .AppendLine($"OS Architecture: {RuntimeInformation.OSArchitecture}")
            .AppendLine($"Installer Version: {installerVersion ?? "unknown"}")
            .AppendLine($"Operation: {operation}")
            .AppendLine($"File: {filePath ?? "n/a"}")
            .AppendLine($"Process Path: {Environment.ProcessPath ?? "unknown"}")
            .AppendLine($"Working Directory: {Environment.CurrentDirectory}")
            .AppendLine($"Exit Code: {effectiveExitCode}")
            .AppendLine($"Hex Exit Code: 0x{(uint)effectiveExitCode:X8}")
            .AppendLine($"Exception: {exception.GetType().FullName}: {exception.Message}")
            .AppendLine("Stack Trace:")
            .AppendLine(exception.ToString())
            .ToString();

        File.WriteAllText(diagnosticPath, report, Encoding.UTF8);
        return diagnosticPath;
    }
}
