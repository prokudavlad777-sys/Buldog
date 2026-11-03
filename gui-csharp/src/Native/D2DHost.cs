using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace BuldogBoost.UI.Native;

/// <summary>
/// Розміщує дочірнє Win32-вікно, у яке Direct2D (bb_render.dll) малює графік.
/// Примітка: HwndHost малюється поверх WPF-контенту («airspace»), тому не накладайте на нього інші елементи.
/// </summary>
public sealed class D2DHost : HwndHost
{
    private const int WsChild = 0x40000000;
    private const int WsVisible = 0x10000000;
    private const int WsClipSiblings = 0x04000000;

    private IntPtr _hwnd;
    private IntPtr _render;

    public bool IsAvailable => _render != IntPtr.Zero;

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        _hwnd = CreateWindowExW(0, "static", "", WsChild | WsVisible | WsClipSiblings,
                                0, 0, 100, 100, hwndParent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        try { _render = RenderApi.bb_render_create(_hwnd); }
        catch (DllNotFoundException) { _render = IntPtr.Zero; }   // працюємо без графіка
        catch (EntryPointNotFoundException) { _render = IntPtr.Zero; }
        return new HandleRef(this, _hwnd);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        if (_render != IntPtr.Zero)
        {
            RenderApi.bb_render_destroy(_render);
            _render = IntPtr.Zero;
        }
        DestroyWindow(hwnd.Handle);
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (_render == IntPtr.Zero) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        uint w = (uint)Math.Max(1, sizeInfo.NewSize.Width * dpi.DpiScaleX);
        uint h = (uint)Math.Max(1, sizeInfo.NewSize.Height * dpi.DpiScaleY);
        RenderApi.bb_render_resize(_render, w, h);
    }

    /// <summary>Додає новий вимір навантаження (0..1).</summary>
    public void PushSample(float value01)
    {
        if (_render != IntPtr.Zero) RenderApi.bb_render_push_sample(_render, value01);
    }

    /// <summary>Малює кадр. Викликається з CompositionTarget.Rendering (~60 FPS).</summary>
    public void Draw()
    {
        if (_render != IntPtr.Zero) RenderApi.bb_render_draw(_render);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowExW(int exStyle, string className, string windowName, int style,
        int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hwnd);
}
