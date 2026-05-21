using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace XboxMetroLauncher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => LogException(args.ExceptionObject as Exception, "AppDomain");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            LogException(args.Exception, "TaskScheduler");
            args.SetObserved();
        };

        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogException(e.Exception, "Dispatcher");
        e.Handled = true;
    }

    internal static void LogException(Exception? exception, string source)
    {
        if (exception is null)
        {
            return;
        }

        try
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "XboxMetroLauncher",
                "Logs");
            Directory.CreateDirectory(root);

            var logPath = Path.Combine(root, "crash.log");
            File.AppendAllText(logPath, $"[{DateTimeOffset.Now:u}] {source}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
