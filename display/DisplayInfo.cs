using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace keyboardmouse.display;

internal static class DisplayInfo
{
    private static readonly List<RECT> s_enumRects = [];

    /// <summary>
    /// Enumerates monitor rectangles. Falls back to the virtual screen rect when no monitors are found.
    /// </summary>
    internal static IList<RECT> GetMonitorRects()
    {
        if (s_enumRects.Count > 0) return s_enumRects;

        unsafe
        {
            PInvoke.EnumDisplayMonitors(default, default, MonitorEnumProc, default);
        }

        if (s_enumRects.Count == 0)
            s_enumRects.Add(GetVirtualScreenRect());

        return s_enumRects;
    }

    // unsafe is required because the CsWin32-generated delegate signature exposes RECT as a native pointer.
    // C# requires code that uses pointers to be marked unsafe.
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

        return new RECT
        {
            left = left,
            top = top,
            right = left + width,
            bottom = top + height,
        };
    }
}
