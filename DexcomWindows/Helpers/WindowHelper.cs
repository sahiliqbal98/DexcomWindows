using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT.Interop;

namespace DexcomWindows.Helpers;

/// <summary>
/// Helper class for creating borderless popup windows with shadows and positioning
/// </summary>
public static class WindowHelper
{
    // Win32 constants
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const int WS_CAPTION = 0x00C00000;
    private const int WS_THICKFRAME = 0x00040000;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const int WS_MAXIMIZEBOX = 0x00010000;
    private const int WS_SYSMENU = 0x00080000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_LAYERED = 0x00080000;
    private const uint SWP_FRAMECHANGED = 0x0020;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int LWA_ALPHA = 0x00000002;

    // DWM corner preference values
    private const int DWMWCP_DEFAULT = 0;
    private const int DWMWCP_DONOTROUND = 1;
    private const int DWMWCP_ROUND = 2;
    private const int DWMWCP_ROUNDSMALL = 3;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    private const uint ABM_GETTASKBARPOS = 0x00000005;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public IntPtr lParam;
    }

    /// <summary>
    /// Configure a window to be borderless (no title bar, no chrome)
    /// </summary>
    public static void MakeBorderless(Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        // Use OverlappedPresenter to remove title bar
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.SetBorderAndTitleBar(false, false);
        }

        // Remove window styles for completely borderless look
        int style = GetWindowLong(hwnd, GWL_STYLE);
        style &= ~(WS_CAPTION | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX | WS_SYSMENU);
        SetWindowLong(hwnd, GWL_STYLE, style);

        // Make it a tool window (no taskbar button, no alt-tab)
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_TOOLWINDOW;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

        // Apply the style changes
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            SWP_FRAMECHANGED | SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER);

        // Set rounded corners (Windows 11)
        SetRoundedCorners(hwnd, true);
    }

    /// <summary>
    /// Set rounded corners on Windows 11
    /// </summary>
    public static void SetRoundedCorners(IntPtr hwnd, bool rounded)
    {
        int preference = rounded ? DWMWCP_ROUND : DWMWCP_DONOTROUND;
        DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
    }

    /// <summary>
    /// Position window directly above the system tray
    /// </summary>
    public static void PositionNearTray(Window window, int width, int height, int margin = 12)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        // Get cursor position to find the right monitor
        GetCursorPos(out POINT cursorPos);

        // Get the monitor where the cursor is
        IntPtr hMonitor = MonitorFromPoint(cursorPos, MONITOR_DEFAULTTONEAREST);
        MONITORINFO monitorInfo = new() { cbSize = Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(hMonitor, ref monitorInfo);

        var workArea = monitorInfo.rcWork;

        // Try to get taskbar position
        APPBARDATA appBarData = new() { cbSize = Marshal.SizeOf<APPBARDATA>() };
        SHAppBarMessage(ABM_GETTASKBARPOS, ref appBarData);

        int x, y;

        // Position based on taskbar location
        // Default: bottom-right corner, above taskbar
        if (appBarData.uEdge == 3) // ABE_BOTTOM - taskbar at bottom
        {
            x = workArea.Right - width - margin;
            y = workArea.Bottom - height - margin;
        }
        else if (appBarData.uEdge == 1) // ABE_TOP - taskbar at top
        {
            x = workArea.Right - width - margin;
            y = workArea.Top + margin;
        }
        else if (appBarData.uEdge == 0) // ABE_LEFT - taskbar at left
        {
            x = workArea.Left + margin;
            y = workArea.Bottom - height - margin;
        }
        else if (appBarData.uEdge == 2) // ABE_RIGHT - taskbar at right
        {
            x = workArea.Right - width - margin;
            y = workArea.Bottom - height - margin;
        }
        else
        {
            // Default position (bottom-right)
            x = workArea.Right - width - margin;
            y = workArea.Bottom - height - margin;
        }

        appWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }

    /// <summary>
    /// Resize window with animation-ready positioning
    /// </summary>
    public static void SetSize(Window window, int width, int height)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        // Get current position
        var currentPos = appWindow.Position;

        // Resize while maintaining position relative to bottom-right
        var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        var workArea = displayArea.WorkArea;

        // Keep the window anchored to bottom-right
        int x = workArea.X + workArea.Width - width - 12;
        int y = workArea.Y + workArea.Height - height - 12;

        appWindow.MoveAndResize(new RectInt32(x, y, width, height));
    }

    /// <summary>
    /// Hide window from taskbar and alt-tab
    /// </summary>
    public static void HideFromTaskbar(Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        appWindow.IsShownInSwitchers = false;
    }

    /// <summary>
    /// Check if a point is inside the window bounds
    /// </summary>
    public static bool IsPointInWindow(Window window, int screenX, int screenY)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        var pos = appWindow.Position;
        var size = appWindow.Size;

        return screenX >= pos.X && screenX <= pos.X + size.Width &&
               screenY >= pos.Y && screenY <= pos.Y + size.Height;
    }

    /// <summary>
    /// Get AppWindow from a Window instance
    /// </summary>
    public static AppWindow GetAppWindow(Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        return AppWindow.GetFromWindowId(windowId);
    }

    /// <summary>
    /// Get the window handle
    /// </summary>
    public static IntPtr GetHandle(Window window)
    {
        return WindowNative.GetWindowHandle(window);
    }

    /// <summary>
    /// Make window a layered window (required for SetWindowOpacity)
    /// </summary>
    public static void MakeLayered(Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_LAYERED;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }

    /// <summary>
    /// Set window opacity (0-255, where 255 is fully opaque)
    /// Window must be layered first (call MakeLayered)
    /// </summary>
    public static void SetWindowOpacity(Window window, byte opacity)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        SetLayeredWindowAttributes(hwnd, 0, opacity, LWA_ALPHA);
    }
}
