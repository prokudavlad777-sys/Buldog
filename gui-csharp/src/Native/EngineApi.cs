using System.Runtime.InteropServices;
using System.Text;

namespace BuldogBoost.UI.Native;

/// <summary>P/Invoke до bb_engine.dll (C++ Optimization Engine). Структури — як у bb_api.h.</summary>
internal static class EngineApi
{
    private const string Dll = "bb_engine";

    [StructLayout(LayoutKind.Sequential)]
    public struct MemoryInfo
    {
        public ulong TotalBytes;
        public ulong AvailableBytes;
        public uint LoadPercent;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CleanResult
    {
        public uint RemovedItems;
        public ulong FreedBytes;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TrimResult
    {
        public uint TrimmedProcesses;
        public ulong FreedBytes;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct ProcessInfo
    {
        public uint Pid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string Name;
        public ulong WorkingSetBytes;
    }

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr bb_engine_version();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern double bb_cpu_usage_percent();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int bb_memory_info(out MemoryInfo info);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int bb_clean_temp(out CleanResult result);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int bb_clean_deep(int includeBrowsers, out CleanResult result);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int bb_trim_memory(out TrimResult result);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int bb_flush_dns();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int bb_set_high_performance();

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int bb_power_get_active(StringBuilder buffer, int capacity);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int bb_power_set_active(string guid);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int bb_list_processes([In, Out] ProcessInfo[] buffer, int capacity);

    /// <summary>GUID активної схеми живлення або null.</summary>
    public static string? GetActivePowerScheme()
    {
        var sb = new StringBuilder(64);
        return bb_power_get_active(sb, sb.Capacity) == 1 ? sb.ToString() : null;
    }

    public static bool SetActivePowerScheme(string guid) => bb_power_set_active(guid) == 1;

    public static string Version() => Marshal.PtrToStringAnsi(bb_engine_version()) ?? "?";

    public static ProcessInfo[] ListProcesses(int max = 30)
    {
        var buf = new ProcessInfo[max];
        int n = bb_list_processes(buf, max);
        return n <= 0 ? Array.Empty<ProcessInfo>() : buf.AsSpan(0, n).ToArray();
    }
}
