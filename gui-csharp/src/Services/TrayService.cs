namespace BuldogBoost.UI.Services;

/// <summary>Іконка в системному треї з меню (WinForms NotifyIcon).</summary>
internal sealed class TrayService : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _icon;

    public TrayService(Action show, Action boost, Action exit)
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Відкрити", null, (_, _) => show());
        menu.Items.Add("⚡ BOOST", null, (_, _) => boost());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Вихід", null, (_, _) => exit());

        _icon = new System.Windows.Forms.NotifyIcon
        {
            Text = "Buldog Boost",
            Icon = LoadIcon(),
            Visible = true,
            ContextMenuStrip = menu
        };
        _icon.DoubleClick += (_, _) => show();
    }

    private static System.Drawing.Icon LoadIcon()
    {
        try
        {
            if (Environment.ProcessPath is { } p)
                return System.Drawing.Icon.ExtractAssociatedIcon(p) ?? System.Drawing.SystemIcons.Application;
        }
        catch { }
        return System.Drawing.SystemIcons.Application;
    }

    public void Notify(string text) =>
        _icon.ShowBalloonTip(2500, "Buldog Boost", text, System.Windows.Forms.ToolTipIcon.Info);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
