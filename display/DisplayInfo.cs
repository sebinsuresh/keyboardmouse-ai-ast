using System.Collections.Generic;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace keyboardmouse.display;

internal static class DisplayInfo
{
    /// <summary>
    /// Enumerates monitor rectangles. Falls back to the virtual screen rect when no monitors are found.
    /// </summary>
    internal static unsafe RECT[] GetMonitorRects()
    {
        var rects = new List<RECT>();

        // Use a temporary static holder so the unmanaged callback can add to it.
        s_enumRects = rects;
        try
        {
            PInvoke.EnumDisplayMonitors(default, default, MonitorEnumProc, default);
        }
        finally
        {
            s_enumRects = null;
        }

        if (rects.Count == 0)
            rects.Add(GetVirtualScreenRect());

        return rects.ToArray();
    }

    // EnumDisplayMonitors requires an unmanaged delegate, which cannot capture a closure in AOT.
    // A static field is the only way to pass state into the callback without unsafe trampolines.
    private static List<RECT>? s_enumRects;

    // unsafe is required because the CsWin32-generated delegate signature exposes RECT as a native pointer.
    private static unsafe BOOL MonitorEnumProc(HMONITOR _hMonitor, HDC _hdcMonitor, RECT* lprcMonitor, LPARAM _dwData)
    {
        if (lprcMonitor != null && s_enumRects is not null)
        {
            s_enumRects.Add(*lprcMonitor);
        }
        return true;
    }

    /// <summary>
    /// Returns the virtual screen rectangle spanning all monitors.
    /// </summary>
    internal static RECT GetVirtualScreenRect()
    {
        int left = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN);
        int top = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN);
        int width = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN);
        int height = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN);
        return new RECT { left = left, top = top, right = left + width, bottom = top + height };
    }
}

