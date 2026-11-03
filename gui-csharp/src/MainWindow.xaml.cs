using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using BuldogBoost.UI.Native;
using BuldogBoost.UI.Services;

namespace BuldogBoost.UI;

public sealed class ProcItem
{
    public uint Pid { get; init; }
    public string Name { get; init; } = "";
    public string Label { get; init; } = "";
    public bool Selected { get; set; }
}

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ProcItem> _procs = new();
    private readonly ObservableCollection<GameProfile> _profiles = new();
    private readonly DispatcherTimer _statsTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Settings _settings = Settings.Load();
    private readonly bool _startHidden = Environment.GetCommandLineArgs().Contains("--tray");

    private TrayService? _tray;
    private GameWatcher? _watcher;
    private readonly BackgroundThrottle _throttle = new();
    private static readonly string[] LevelNames = { "Звичайний", "Сильний", "Максимум" };

    private string? _prevScheme;   // схема живлення до Boost — для відкату
    private bool _exiting;
    private bool _boosting;

    public MainWindow()
    {
        InitializeComponent();
        ProcList.ItemsSource = _procs;
        ProfilesList.ItemsSource = _profiles;
        foreach (var p in _settings.Profiles) _profiles.Add(p);

        try
        {
            Title = $"Buldog Boost — engine {EngineApi.Version()}";
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            MessageBox.Show("Не вдалося завантажити bb_engine.dll.\nЗапустіть scripts\\build.ps1 і покладіть DLL поруч із exe.\n\n" + ex.Message,
                            "Buldog Boost", MessageBoxButton.OK, MessageBoxImage.Error);
            _exiting = true;
            Application.Current.Shutdown();
            return;
        }

        TrayBox.IsChecked = _settings.MinimizeToTray;
        BoostLevelBox.SelectedIndex = Math.Clamp(_settings.BoostLevel, 0, 2);
        try { AutoStartBox.IsChecked = AutoStart.IsEnabled(); } catch { AutoStartBox.IsEnabled = false; }

        _tray = new TrayService(ShowFromTray, DoBoost, ExitApp);
        _watcher = new GameWatcher(() => _profiles.ToList());
        _watcher.GameStarted += OnGameStarted;
        _watcher.GameStopped += OnGameStopped;

        if (_startHidden)
        {
            ShowActivated = false;
            WindowState = WindowState.Minimized;
            ShowInTaskbar = false;
        }

        _statsTimer.Tick += (_, _) => UpdateStats();
        Loaded += (_, _) =>
        {
            _statsTimer.Start();
            CompositionTarget.Rendering += OnRendering;
            LoadProcesses();
            Log(AdminText());
            if (!Graph.IsAvailable) Log("Графік недоступний: bb_render.dll не завантажено.");
            if (_startHidden) Hide();
        };
        Closed += (_, _) =>
        {
            _statsTimer.Stop();
            CompositionTarget.Rendering -= OnRendering;
            _watcher?.Dispose();
            _throttle.Restore();      // не залишаємо програми приглушеними після виходу
            _tray?.Dispose();
        };
    }

    // ---------------- трей / вікно ----------------

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_exiting && _settings.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            _tray?.Notify("Працюю у фоні. Відкрити — подвійний клік по значку.");
            return;
        }
        base.OnClosing(e);
    }

    private void ShowFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApp()
    {
        _exiting = true;
        Close();
    }

    // ---------------- допоміжне ----------------

    private static string AdminText()
    {
        try { return SafeApi.IsAdmin() ? "Запущено з правами адміністратора." : "Без прав адміністратора — частину процесів закрити не вдасться."; }
        catch (DllNotFoundException) { return "bb_safe.dll не знайдено — закриття процесів вимкнено."; }
    }

    private static bool IsProtected(string name)
    {
        try { return SafeApi.IsProtected(name); }
        catch (DllNotFoundException) { return true; } // без Rust-модуля нічого не пропонуємо закривати
    }

    private void Log(string text) => LogBox.AppendText(text + Environment.NewLine);

    private void OnRendering(object? sender, EventArgs e) => Graph.Draw();

    private void UpdateStats()
    {
        double cpu = EngineApi.bb_cpu_usage_percent();
        CpuText.Text = $"CPU: {cpu:0}%";
        Graph.PushSample((float)(cpu / 100.0));

        if (EngineApi.bb_memory_info(out var mem) == 1)
        {
            double usedGb = (mem.TotalBytes - mem.AvailableBytes) / 1024.0 / 1024 / 1024;
            double totalGb = mem.TotalBytes / 1024.0 / 1024 / 1024;
            RamText.Text = $"RAM: {mem.LoadPercent}%  ({usedGb:0.0} / {totalGb:0.0} ГБ)";
        }
    }

    private void LoadProcesses()
    {
        _procs.Clear();
        foreach (var p in EngineApi.ListProcesses(30))
        {
            if (string.IsNullOrEmpty(p.Name) || IsProtected(p.Name)) continue; // системні не показуємо
            _procs.Add(new ProcItem
            {
                Pid = p.Pid,
                Name = p.Name,
                Label = $"{p.Name} — {p.WorkingSetBytes / 1024.0 / 1024:0} МБ (PID {p.Pid})"
            });
        }
    }

    // ---------------- живлення з відкатом ----------------

    private async Task<bool> ApplyHighPerformance()
    {
        _prevScheme ??= EngineApi.GetActivePowerScheme();
        bool ok = await Task.Run(() => EngineApi.bb_set_high_performance() == 1);   // powercfg може тривати секунди
        Log(ok
            ? "Увімкнено «Висока продуктивність»." + (_prevScheme != null ? " Відкат — кнопка «Відкат»." : "")
            : "Не вдалося змінити схему живлення.");
        return ok;
    }

    /// <summary>Повертає схему живлення та пріоритети фонових програм.</summary>
    private void RestoreAll()
    {
        if (_prevScheme is null) Log("Немає збереженої схеми живлення для відкату.");
        else if (EngineApi.SetActivePowerScheme(_prevScheme))
        {
            Log("Попередню схему живлення повернуто.");
            _prevScheme = null;
        }
        else Log("Не вдалося повернути схему живлення.");

        int n = _throttle.Restore();
        if (n > 0) Log($"Пріоритети повернуто: {n} програм.");
    }

    // ---------------- закриття процесів ----------------

    private void CloseByName(string name)
    {
        var stem = Path.GetFileNameWithoutExtension(name.Trim());
        if (stem.Length == 0) return;
        foreach (var proc in Process.GetProcessesByName(stem))
        {
            using (proc)
            {
                var (_, msg) = SafeApi.Terminate((uint)proc.Id);
                Log($"{stem} (PID {proc.Id}): {msg}");
            }
        }
    }

    // ---------------- ігрові профілі ----------------

    private async void OnGameStarted(GameProfile p)
    {
        Log($"Гру «{p.Name}» запущено — застосовую профіль.");
        await ApplyHighPerformance();
        foreach (var name in p.CloseProcesses) CloseByName(name);

        if (BackgroundThrottle.RaiseGame(p.GameExe) > 0) Log($"Пріоритет гри «{p.Name}» піднято.");
        var keep = KeepList();
        int n = await Task.Run(() => _throttle.Apply(keep, IsProtected));
        if (n > 0) Log($"Фонові програми приглушено: {n} (повернемо після гри).");

        _tray?.Notify($"Профіль «{p.Name}» застосовано.");
    }

    private void OnGameStopped(GameProfile p)
    {
        Log($"Гру «{p.Name}» закрито — повертаю налаштування.");
        RestoreAll();
    }

    /// <summary>Що не чіпати при приглушенні: список із налаштувань + усі ігри з профілів.</summary>
    private List<string> KeepList() =>
        _settings.KeepProcesses.Concat(_profiles.Select(x => x.GameExe)).ToList();

    private void SaveProfiles()
    {
        _settings.Profiles = _profiles.ToList();
        _settings.Save();
    }

    private void BtnAddProfile_Click(object sender, RoutedEventArgs e)
    {
        var exe = ProfileExeBox.Text.Trim();
        if (exe.Length == 0) { Log("Вкажіть файл гри (наприклад, cs2.exe)."); return; }

        var name = ProfileNameBox.Text.Trim();
        var close = ProfileCloseBox.Text
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(n => !IsProtected(n))     // системні в профіль не потрапляють
            .ToList();

        _profiles.Add(new GameProfile
        {
            Name = name.Length == 0 ? Path.GetFileNameWithoutExtension(exe) : name,
            GameExe = exe,
            CloseProcesses = close
        });
        SaveProfiles();
        ProfileNameBox.Clear(); ProfileExeBox.Clear(); ProfileCloseBox.Clear();
    }

    private void BtnRemoveProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ProfilesList.SelectedItem is GameProfile p)
        {
            _profiles.Remove(p);
            SaveProfiles();
        }
    }

    // ---------------- налаштування ----------------

    private void AutoStartBox_Click(object sender, RoutedEventArgs e)
    {
        try { AutoStart.Set(AutoStartBox.IsChecked == true); }
        catch (Exception ex) { Log("Автозапуск: " + ex.Message); AutoStartBox.IsChecked = AutoStart.IsEnabled(); }
    }

    private void TrayBox_Click(object sender, RoutedEventArgs e)
    {
        _settings.MinimizeToTray = TrayBox.IsChecked == true;
        _settings.Save();
    }

    // ---------------- кнопки ----------------

    private void BtnClean_Click(object sender, RoutedEventArgs e)
    {
        if (EngineApi.bb_clean_temp(out var r) == 1)
            Log($"Очищено елементів: {r.RemovedItems}, звільнено {r.FreedBytes / 1024.0 / 1024:0.0} МБ");
        else
            Log("Очищення не вдалося.");
    }

    private async void BtnPower_Click(object sender, RoutedEventArgs e) => await ApplyHighPerformance();
    private void BtnRestorePower_Click(object sender, RoutedEventArgs e) => RestoreAll();

    private void BoostLevelBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (!IsLoaded || BoostLevelBox.SelectedIndex < 0) return;
        _settings.BoostLevel = BoostLevelBox.SelectedIndex;
        _settings.Save();
    }
    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => LoadProcesses();

    private void BtnKill_Click(object sender, RoutedEventArgs e)
    {
        int closed = 0;
        foreach (var p in _procs.Where(x => x.Selected).ToList())
        {
            var (ok, msg) = SafeApi.Terminate(p.Pid);
            Log($"{p.Name}: {msg}");
            if (ok) closed++;
        }
        Log($"Закрито процесів: {closed}");
        LoadProcesses();
    }

    private void BtnBoost_Click(object sender, RoutedEventArgs e) => DoBoost();

    private sealed record BoostReport(
        ulong TempBytes, uint TempItems, ulong DeepBytes, uint DeepItems,
        bool Dns, uint Trimmed, uint RamBefore, uint RamAfter);

    /// <summary>
    /// Рівні: 0 — temp + висока продуктивність;
    /// 1 — + глибоке очищення, DNS, приглушення фонових програм;
    /// 2 — + кеш браузерів, скидання робочих наборів RAM, закриття відмічених у списку.
    /// Усе, окрім очищення файлів і закриття відмічених, відкочується кнопкою «Відкат».
    /// </summary>
    private async void DoBoost()
    {
        if (_boosting) return;
        _boosting = true;
        BoostBtn.IsEnabled = false;
        try
        {
            int level = Math.Clamp(_settings.BoostLevel, 0, 2);
            Log($"⚡ Boost «{LevelNames[level]}» — працюю…");

            // Важкі нативні виклики — поза UI-потоком, щоб вікно не зависало.
            var report = await Task.Run(() =>
            {
                EngineApi.bb_memory_info(out var m0);
                EngineApi.bb_clean_temp(out var temp);

                EngineApi.CleanResult deep = default;
                bool dns = false;
                EngineApi.TrimResult trim = default;

                if (level >= 1)
                {
                    EngineApi.bb_clean_deep(level >= 2 ? 1 : 0, out deep);
                    dns = EngineApi.bb_flush_dns() == 1;
                }
                if (level >= 2) EngineApi.bb_trim_memory(out trim);

                EngineApi.bb_memory_info(out var m1);
                return new BoostReport(temp.FreedBytes, temp.RemovedItems, deep.FreedBytes, deep.RemovedItems,
                                       dns, trim.TrimmedProcesses, m0.LoadPercent, m1.LoadPercent);
            });

            double mb = (report.TempBytes + report.DeepBytes) / 1024.0 / 1024;
            Log($"Очищено файлів: {report.TempItems + report.DeepItems}, звільнено {mb:0.0} МБ диска.");
            if (level >= 1) Log(report.Dns ? "DNS-кеш скинуто." : "DNS-кеш скинути не вдалося.");
            if (level >= 2) Log($"Робочі набори скинуто в {report.Trimmed} процесів: RAM {report.RamBefore}% → {report.RamAfter}% " +
                                "(ефект тимчасовий — Windows підтягне потрібне назад).");

            await ApplyHighPerformance();

            if (level >= 1)
            {
                var keep = KeepList();
                int n = await Task.Run(() => _throttle.Apply(keep, IsProtected));
                Log(n > 0 ? $"Фонові програми приглушено: {n} (пріоритет нижче звичайного). Відкат — кнопка «Відкат»."
                          : "Приглушувати нічого не довелося.");
            }

            if (level >= 2)
            {
                var selected = _procs.Where(x => x.Selected).ToList();
                int closed = 0;
                foreach (var p in selected)
                {
                    var (ok, msg) = SafeApi.Terminate(p.Pid);
                    Log($"{p.Name}: {msg}");
                    if (ok) closed++;
                }
                Log(selected.Count == 0
                    ? "Щоб «Максимум» закрив зайве — відмітьте програми у списку."
                    : $"Закрито відмічених програм: {closed} з {selected.Count}.");
                LoadProcesses();
            }

            Log("✅ Boost завершено.");
            _tray?.Notify($"Boost «{LevelNames[level]}» завершено.");
        }
        catch (Exception ex)
        {
            Log("Boost не вдався: " + ex.Message);
        }
        finally
        {
            _boosting = false;
            BoostBtn.IsEnabled = true;
        }
    }
}
