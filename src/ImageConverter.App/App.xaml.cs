using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ImageConverter.Core.Models;
using ImageConverter.Core.Services;
using Microsoft.UI.Xaml;

namespace ImageConverter.App;

public partial class App : Application
{
    private Window? _window;

    public static AppPaths Paths { get; private set; } = null!;

    public static LoggingService Logger { get; private set; } = null!;

    public static DiagnosticService Diagnostics { get; private set; } = null!;

    public static SettingsService Settings { get; private set; } = null!;

    public App()
    {
        InitializeComponent();
        base.UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        Paths = new AppPaths();
        Logger = new LoggingService(Paths);
        Diagnostics = new DiagnosticService(Paths);
        Settings = new SettingsService(Paths, Logger);
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        _ = 1;
        try
        {
            AppSettings appSettings = await Settings.LoadAsync();
            string[] source = Environment.GetCommandLineArgs().Skip(1).ToArray();
            string? text = source.FirstOrDefault(value => value.StartsWith("--theme=", StringComparison.OrdinalIgnoreCase));
            if (text != null)
            {
                string text2 = text;
                int length = "--theme=".Length;
                string text3 = text2.Substring(length, text2.Length - length);
                bool flag;
                switch (text3)
                {
                    case "Light":
                    case "Dark":
                    case "System":
                        flag = true;
                        break;
                    default:
                        flag = false;
                        break;
                }
                if (flag)
                {
                    appSettings = appSettings with
                    {
                        Theme = text3
                    };
                }
            }
            MainWindow mainWindow = (MainWindow)(_window = new MainWindow(appSettings));
            _window.Activate();
            string[] array = source.Where((string path) => File.Exists(path) || Directory.Exists(path)).ToArray();
            if (array.Length != 0)
            {
                await mainWindow.ImportExternalPathsAsync(array);
            }
            Logger.Info("App.Start", "Application window activated.");
        }
        catch (Exception exception)
        {
            Logger.Error("App.Launch", exception);
            Diagnostics.Write("App.Launch", exception);
            throw;
        }
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Logger.Error("App.UnhandledException", e.Exception);
        Diagnostics.Write("App.UnhandledException", e.Exception);
    }

    private void OnDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs e)
    {
        Exception exception = (e.ExceptionObject as Exception) ?? new Exception(e.ExceptionObject?.ToString() ?? "Unknown fatal error");
        Logger.Error("AppDomain.UnhandledException", exception);
        Diagnostics.Write("AppDomain.UnhandledException", exception);
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.Error("TaskScheduler.UnobservedTaskException", e.Exception);
        Diagnostics.Write("TaskScheduler.UnobservedTaskException", e.Exception);
        e.SetObserved();
    }

}
