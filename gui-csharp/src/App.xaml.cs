using System.Threading;
using System.Windows;

namespace BuldogBoost.UI;

public partial class App : Application
{
    private static Mutex? _singleInstance;

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
