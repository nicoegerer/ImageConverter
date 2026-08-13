using System;
using System.IO;

namespace ImageConverter.Core.Services;

public sealed class AppPaths
{
    public string Root { get; }

    public string Cache { get; }

    public string Logs { get; }

    public string Diagnostics { get; }

    public string SettingsFile { get; }

    public AppPaths(string? rootOverride = null)
    {
        Root = rootOverride ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Image Converter");
        Cache = Path.Combine(Root, "Cache");
        Logs = Path.Combine(Root, "Logs");
        Diagnostics = Path.Combine(Root, "Diagnostics");
        SettingsFile = Path.Combine(Root, "settings.json");
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Cache);
        Directory.CreateDirectory(Logs);
        Directory.CreateDirectory(Diagnostics);
    }
}
