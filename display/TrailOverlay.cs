using System;
using System.Threading.Tasks;
using System.Drawing;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace keyboardmouse.display;

/// <summary>
/// Minimal topmost click-through overlay that shows a single white line
/// between two screen points and clears it after ~1 second.
/// Designed to be tiny and synchronous-friendly for this project.
/// </summary>
internal sealed class TrailOverlay : IDisposable
{
    private static TrailOverlay? s_instance;
    private bool _disposed;

    internal static TrailOverlay? Instance => s_instance;

    private HWND _hwnd = HWND.Null;
    private int _virtualOriginX;
    private int _virtualOriginY;

    private Point _from;
    private Point _to;
    private DateTime _shownAt = DateTime.MinValue;
    private bool _hasLine = false;

    private const WINDOW_EX_STYLE OverlayExStyle =
        WINDOW_EX_STYLE.WS_EX_LAYERED |
        WINDOW_EX_STYLE.WS_EX_TRANSPARENT |
        WINDOW_EX_STYLE.WS_EX_TOPMOST |
        WINDOW_EX_STYLE.WS_EX_NOACTIVATE;

    // Use the same nearly-black color key as the other overlay so we can erase by filling with this color.
    private const int TransparentColor = 0x010000; // BGR format

    internal static unsafe void RegisterWindowClass()
    {
        const string className = "TrailOverlay";
        fixed (char* classPtr = className)
        {
            WNDCLASSW wndClass = new()
            {
                lpszClassName = (PCWSTR)classPtr,
                lpfnWndProc = WindowProcedure,
            };

            if (PInvoke.RegisterClass(wndClass) == 0)
            {
                throw new InvalidOperationException("Failed to register trail overlay window class.");
            }
        }
    }

    public void Create()
    {
        if (_hwnd != HWND.Null) return;

        s_instance = this;

        RECT virtualScreen = DisplayInfo.GetVirtualScreenRect();
        _virtualOriginX = virtualScreen.left;
        _virtualOriginY = virtualScreen.top;

        unsafe
        {
            _hwnd = PInvoke.CreateWindowEx(
                OverlayExStyle,
                "TrailOverlay",
                "Trail Overlay",
                WINDOW_STYLE.WS_POPUP,
                virtualScreen.left,
                virtualScreen.top,
                virtualScreen.right - virtualScreen.left,
                virtualScreen.bottom - virtualScreen.top,
                HWND.Null,
                null,
                PInvoke.GetModuleHandle(null),
                null
            );
        }

        if (_hwnd == HWND.Null)
        {
            throw new InvalidOperationException("Failed to create trail overlay window.");
        }

        // Use color-key transparency so we can paint a nearly-black background to erase content.
        PInvoke.SetLayeredWindowAttributes(
            _hwnd,
            new COLORREF(TransparentColor),
            255,
            LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_COLORKEY | LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA
        );
    }

    public void Show()
    {
        if (_hwnd == HWND.Null) return;
        PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
    }

    public void Destroy()
    {
        if (_hwnd != HWND.Null)
        {
            PInvoke.DestroyWindow(_hwnd);
            _hwnd = HWND.Null;
        }
        s_instance = null;
        _disposed = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Destroy();
        GC.SuppressFinalize(this);
    }

    ~TrailOverlay()
    {
        Destroy();
    }

    /// <summary>
    /// Draws a single white line from screen coordinate <paramref name="from"/> to <paramref name="to"/>
    /// and schedules it to be cleared after ~1000ms.
    /// </summary>
    public void ShowLine(Point from, Point to)
    {
        _from = from;
        _to = to;
        _shownAt = DateTime.UtcNow;
        _hasLine = true;

        if (_hwnd != HWND.Null)
        {
            PInvoke.InvalidateRect(_hwnd, null, true);

            // Clear after ~1 second by invalidating again; keep it simple and fire-and-forget.
            _ = Task.Run(async () =>
            {
                await Task.Delay(1000).ConfigureAwait(false);
                if (_hwnd != HWND.Null)
                {
                    _hasLine = false;
                    PInvoke.InvalidateRect(_hwnd, null, true);
                }
            });
        }
    }

    private static LRESULT WindowProcedure(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (s_instance == null) return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);

        return msg switch
        {
            PInvoke.WM_PAINT => s_instance.OnWmPaint(hwnd),
            PInvoke.WM_DESTROY => OnWmDestroy(),
            _ => PInvoke.DefWindowProc(hwnd, msg, wParam, lParam)
        };
    }

    private LRESULT OnWmPaint(HWND hwnd)
    {
        HDC hdc = PInvoke.BeginPaint(hwnd, out PAINTSTRUCT ps);

        try
        {
            PInvoke.GetClientRect(hwnd, out RECT clientRect);
            FillWithTransparencyColor(hdc, clientRect);

            if (_hasLine)
            {
                // Convert to client coords by subtracting virtual origin.
                int x1 = _from.X - _virtualOriginX;
                int y1 = _from.Y - _virtualOriginY;
                int x2 = _to.X - _virtualOriginX;
                int y2 = _to.Y - _virtualOriginY;

                // Simple white pen (thicker for better visibility).
                HPEN whitePen = PInvoke.CreatePen(PEN_STYLE.PS_SOLID, 4, new COLORREF(0xFFFFFF));
                HGDIOBJ oldPen = PInvoke.SelectObject(hdc, whitePen);

                try
                {
                    unsafe { PInvoke.MoveToEx(hdc, x1, y1, null); }
                    PInvoke.LineTo(hdc, x2, y2);
                }
                finally
                {
                    PInvoke.SelectObject(hdc, oldPen);
                    PInvoke.DeleteObject(whitePen);
                }
            }
        }
        finally
        {
            PInvoke.EndPaint(hwnd, ps);
        }

        return default;
    }

    private static LRESULT OnWmDestroy()
    {
        PInvoke.PostQuitMessage(0);
        return default;
    }

    private static void FillWithTransparencyColor(HDC hdc, RECT clientRect)
    {
        HBRUSH keyBrush = PInvoke.CreateSolidBrush(new COLORREF(TransparentColor));
        HGDIOBJ oldBrush = PInvoke.SelectObject(hdc, keyBrush);
        try
        {
            const ROP_CODE PatCopy = (ROP_CODE)0x00F00021; // PATCOPY
            PInvoke.PatBlt(hdc, clientRect.left, clientRect.top,
                clientRect.right - clientRect.left,
                clientRect.bottom - clientRect.top,
                PatCopy);
        }
        finally
        {
            PInvoke.SelectObject(hdc, oldBrush);
            PInvoke.DeleteObject(keyBrush);
        }
    }
}
