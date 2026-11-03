using System.IO;
using System.Threading;
using System.Windows;

namespace BuldogBoost.UI;

public partial class App : Application
{
    private static Mutex? _singleInstance;

    private static void ReportCrash(string where, Exception? ex)
    {
        var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {where}\n{ex}\n\n";
        string? logPath = null;
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BuldogBoost");
            Directory.CreateDirectory(dir);
            logPath = Path.Combine(dir, "crash.log");
            File.AppendAllText(logPath, text);
        }
        catch { }
        try
        {
            MessageBox.Show("Buldog Boost не зміг запуститися:\n\n" + ex?.Message +
                            (logPath != null ? "\n\nДеталі: " + logPath : ""),
                            "Buldog Boost", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch { }
    }

    public App()
    {
        DispatcherUnhandledException += (_, e) => { ReportCrash("UI", e.Exception); e.Handled = true; Shutdown(1); };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportCrash("Domain", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => { ReportCrash("Task", e.Exception); e.SetObserved(); };
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstance = new Mutex(true, "BuldogBoost.SingleInstance", out bool created);
        if (!created)
        {
            MessageBox.Show("Buldog Boost уже запущено (перевірте значок у треї).", "Buldog Boost",
                            MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }
        base.OnStartup(e);
    }
}
