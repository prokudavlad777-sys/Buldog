using Microsoft.Win32;

namespace BuldogBoost.UI.Services;

/// <summary>Автозапуск із Windows через HKCU\...\Run (права адміністратора не потрібні).</summary>
internal static class AutoStart
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "BuldogBoost";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(ValueName) is string;
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true)
                        ?? Registry.CurrentUser.CreateSubKey(KeyPath);
        if (enabled && Environment.ProcessPath is { } exe)
            key.SetValue(ValueName, $"\"{exe}\" --tray");   // --tray: стартувати згорнутим
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
