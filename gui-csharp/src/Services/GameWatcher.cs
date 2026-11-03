using System.Diagnostics;
using System.IO;
using System.Windows.Threading;

namespace BuldogBoost.UI.Services;

/// <summary>Стежить, чи запущена гра з профілю; повідомляє про старт і завершення.</summary>
internal sealed class GameWatcher : IDisposable
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(3) };
    private readonly Func<IReadOnlyList<GameProfile>> _profiles;
    private readonly HashSet<GameProfile> _active = new();

    public event Action<GameProfile>? GameStarted;
    public event Action<GameProfile>? GameStopped;

    public GameWatcher(Func<IReadOnlyList<GameProfile>> profiles)
    {
        _profiles = profiles;
        _timer.Tick += (_, _) => Check();
        _timer.Start();
    }

    private void Check()
    {
        var list = _profiles();
        foreach (var p in list)
        {
            bool running = IsRunning(p.GameExe);
            if (running && _active.Add(p)) GameStarted?.Invoke(p);
            else if (!running && _active.Remove(p)) GameStopped?.Invoke(p);
        }
        _active.RemoveWhere(a => !list.Contains(a)); // профіль видалили
    }

    public static bool IsRunning(string exeName)
    {
        var stem = Path.GetFileNameWithoutExtension(exeName.Trim());
        if (stem.Length == 0) return false;
        var found = Process.GetProcessesByName(stem);
        bool any = found.Length > 0;
        foreach (var p in found) p.Dispose();
        return any;
    }

    public void Dispose() => _timer.Stop();
}
