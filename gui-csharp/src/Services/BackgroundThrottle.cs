using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BuldogBoost.UI.Native;

namespace BuldogBoost.UI.Services;

/// <summary>
/// Знижує пріоритет фонових програм (BelowNormal), щоб гра/активне вікно отримало більше CPU.
/// Нічого не закриває; усе повертається методом <see cref="Restore"/> (або само, коли програма завершиться).
/// </summary>
internal sealed class BackgroundThrottle
{
    private sealed record Saved(int Pid, string Name, DateTime Start, ProcessPriorityClass Original);

    private readonly List<Saved> _saved = new();
    private readonly object _lock = new();

    public int Count { get { lock (_lock) return _saved.Count; } }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);

    private static uint ForegroundPid()
    {
        var h = GetForegroundWindow();
        return h == IntPtr.Zero ? 0 : (GetWindowThreadProcessId(h, out var pid) == 0 ? 0 : pid);
    }

    /// <summary>Повертає кількість приглушених процесів.</summary>
    public int Apply(IEnumerable<string> keepNames, Func<string, bool> isProtected, int scan = 60)
    {
        var keep = new HashSet<string>(
            keepNames.Select(n => n.Trim().ToLowerInvariant()).Where(n => n.Length > 0));
        var fg = ForegroundPid();
        var self = (uint)Environment.ProcessId;
        int count = 0;

        foreach (var info in EngineApi.ListProcesses(scan))
        {
            if (string.IsNullOrEmpty(info.Name) || info.Pid <= 4 || info.Pid == self || info.Pid == fg) continue;
            if (keep.Contains(info.Name.ToLowerInvariant()) || isProtected(info.Name)) continue;

            lock (_lock) if (_saved.Any(s => s.Pid == info.Pid)) continue;

            try
            {
                using var p = Process.GetProcessById((int)info.Pid);
                var original = p.PriorityClass;
                if (original != ProcessPriorityClass.Normal) continue;   // чужі налаштування не чіпаємо
                var start = p.StartTime;
                p.PriorityClass = ProcessPriorityClass.BelowNormal;
                lock (_lock) _saved.Add(new Saved(p.Id, info.Name, start, original));
                count++;
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ArgumentException or NotSupportedException)
            {
                // немає доступу або процес уже завершився — пропускаємо
            }
        }
        return count;
    }

    /// <summary>Повертає початкові пріоритети. Повертає кількість відновлених.</summary>
    public int Restore()
    {
        List<Saved> copy;
        lock (_lock) { copy = _saved.ToList(); _saved.Clear(); }

        int restored = 0;
        foreach (var s in copy)
        {
            try
            {
                using var p = Process.GetProcessById(s.Pid);
                if (p.StartTime != s.Start) continue;       // PID віддали іншому процесу
                p.PriorityClass = s.Original;
                restored++;
            }
            catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ArgumentException or NotSupportedException)
            {
                // процес уже закрито — відновлювати нічого
            }
        }
        return restored;
    }

    /// <summary>Підняти пріоритет гри (скидається сам, коли гра завершиться).</summary>
    public static int RaiseGame(string exeName)
    {
        var stem = System.IO.Path.GetFileNameWithoutExtension(exeName.Trim());
        if (stem.Length == 0) return 0;
        int n = 0;
        foreach (var p in Process.GetProcessesByName(stem))
        {
            using (p)
            {
                try { p.PriorityClass = ProcessPriorityClass.High; n++; }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { }
            }
        }
        return n;
    }
}
