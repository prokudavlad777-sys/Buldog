using System.Runtime.InteropServices;

namespace BuldogBoost.UI.Native;

/// <summary>P/Invoke до bb_safe.dll (Rust). Коди помилок — як у safe-rust/src/lib.rs.</summary>
internal static class SafeApi
{
    private const string Dll = "bb_safe";

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int bb_safe_is_protected([MarshalAs(UnmanagedType.LPStr)] string name);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int bb_safe_terminate(uint pid);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    private static extern int bb_safe_is_admin();

    public static bool IsProtected(string name) => bb_safe_is_protected(name) != 0;
    public static bool IsAdmin() => bb_safe_is_admin() != 0;

    public static (bool Ok, string Message) Terminate(uint pid) => bb_safe_terminate(pid) switch
    {
        0 => (true, "закрито"),
        -1 => (false, "некоректний PID"),
        -2 => (false, "немає доступу (потрібні права адміністратора?)"),
        -3 => (false, "процес захищений"),
        -4 => (false, "не вдалося закрити"),
        -5 => (false, "платформа не підтримується"),
        var c => (false, $"помилка {c}")
    };
}
