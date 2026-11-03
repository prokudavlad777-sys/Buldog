using System.IO;
using System.Text.Json;

namespace BuldogBoost.UI.Services;

/// <summary>Ігровий профіль: коли запускається GameExe — закриваємо CloseProcesses і вмикаємо високу продуктивність.</summary>
public sealed class GameProfile
{
    public string Name { get; set; } = "";
    public string GameExe { get; set; } = "";
    public List<string> CloseProcesses { get; set; } = new();

    public override string ToString() =>
        $"{Name} — {GameExe}   (закрити: {(CloseProcesses.Count == 0 ? "нічого" : string.Join(", ", CloseProcesses))})";
}

/// <summary>Налаштування користувача у %APPDATA%\BuldogBoost\settings.json.</summary>
public sealed class Settings
{
    public bool MinimizeToTray { get; set; } = true;

    /// <summary>0 — Звичайний, 1 — Сильний, 2 — Максимум.</summary>
    public int BoostLevel { get; set; } = 1;

    /// <summary>Програми, які Boost ніколи не приглушує (запис, голосовий чат тощо). Редагується в settings.json.</summary>
    public List<string> KeepProcesses { get; set; } = new() { "obs64.exe", "obs32.exe", "discord.exe" };
    public List<GameProfile> Profiles { get; set; } = new();

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BuldogBoost", "settings.json");

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings();
        }
        catch { /* пошкоджений файл — стартуємо з типових */ }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
        }
        catch { /* немає прав — не критично */ }
    }
}
