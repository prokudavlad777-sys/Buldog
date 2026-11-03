using System.Runtime.InteropServices;

namespace BuldogBoost.UI.Native;

/// <summary>P/Invoke до bb_render.dll (Direct2D).</summary>
internal static class RenderApi
{
    private const string Dll = "bb_render";

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr bb_render_create(IntPtr hwnd);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bb_render_destroy(IntPtr render);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bb_render_resize(IntPtr render, uint width, uint height);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bb_render_push_sample(IntPtr render, float value01);

    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
    public static extern int bb_render_draw(IntPtr render);
}
