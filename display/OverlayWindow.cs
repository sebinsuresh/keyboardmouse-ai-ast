using System;
using keyboardmouse.display;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace keyboardmouse.display;

/// <summary>
/// Topmost click-through layered overlay window displaying a cyan 3×3 grid.
/// </summary>
internal sealed class OverlayWindow
{
    private static OverlayWindow? s_instance;

    private HWND _hwnd = HWND.Null;
    private RECT _currentBounds = default;
    private int _virtualOriginX;
    private int _virtualOriginY;

    // Hand-defined constants not emitted by CsWin32.
    private const uint WS_POPUP = 0x80000000;
    private const uint WS_EX_LAYERED = 0x00080000;
    private const uint WS_EX_TRANSPARENT = 0x00000020;
    private const uint WS_EX_TOPMOST = 0x00000008;
    private const uint WS_EX_NOACTIVATE = 0x08000000;
    private const uint LWA_COLORKEY = 0x00000001;
    private const uint WM_PAINT = 0x000F;

    internal static unsafe void RegisterWindowClass()
    {
        const string className = "GridOverlay";
        fixed (char* classPtr = className)
        {
            WNDCLASSW wndClass = new()
            {
                lpszClassName = (PCWSTR)classPtr,
                lpfnWndProc = WndProc,
            };

            ushort atom = PInvoke.RegisterClass(wndClass);
            if (atom == 0)
                throw new InvalidOperationException("Failed to register overlay window class.");
        }
    }

    public unsafe void Create()
    {
        if (_hwnd != HWND.Null) return;

        s_instance = this;

        // Get the full virtual screen to size the overlay window.
        RECT virtualScreen = DisplayInfo.GetVirtualScreenRect();
        _virtualOriginX = virtualScreen.left;
        _virtualOriginY = virtualScreen.top;

        WINDOW_EX_STYLE exStyle = (WINDOW_EX_STYLE)(WS_EX_LAYERED
                                                   | WS_EX_TRANSPARENT
                                                   | WS_EX_TOPMOST
                                                   | WS_EX_NOACTIVATE);
        WINDOW_STYLE style = (WINDOW_STYLE)WS_POPUP;

        _hwnd = PInvoke.CreateWindowEx(
            exStyle,
            "GridOverlay",
            "Grid Overlay",
            style,
            virtualScreen.left,
            virtualScreen.top,
            virtualScreen.right - virtualScreen.left,
            virtualScreen.bottom - virtualScreen.top,
            HWND.Null,
            null,
            PInvoke.GetModuleHandle((string?)null),
            null
        );

        if (_hwnd == HWND.Null)
            throw new InvalidOperationException("Failed to create overlay window.");

        // Set the color key for transparency: RGB(0, 0, 1) — the nearly-black pixel
        uint colorKey = 0x010000; // BGR format
        PInvoke.SetLayeredWindowAttributes(
            _hwnd,
            new COLORREF(colorKey),
            255,
            (LAYERED_WINDOW_ATTRIBUTES_FLAGS)LWA_COLORKEY
        );
    }

    public void Show()
    {
        if (_hwnd == HWND.Null) return;
        PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
    }

    public void Hide()
    {
        if (_hwnd == HWND.Null) return;
        PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_HIDE);
    }

    public void UpdateBounds(RECT bounds)
    {
        _currentBounds = bounds;
        if (_hwnd != HWND.Null)
            PInvoke.InvalidateRect(_hwnd, null, true);
    }

    public void Destroy()
    {
        if (_hwnd != HWND.Null)
        {
            PInvoke.DestroyWindow(_hwnd);
            _hwnd = HWND.Null;
        }
        s_instance = null;
    }

    // =========================================================================
    // Static WNDPROC dispatcher (AOT-safe, same pattern as KeyboardHook).
    // =========================================================================

    private static LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (s_instance == null)
            return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);

        if (msg == WM_PAINT)
            return s_instance.OnWmPaint(hwnd);

        if (msg == PInvoke.WM_DESTROY)
            return s_instance.OnWmDestroy();

        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private LRESULT OnWmPaint(HWND hwnd)
    {
        PAINTSTRUCT ps = default;

        unsafe
        {
            HDC hdc = PInvoke.BeginPaint(hwnd, &ps);

            try
            {
                PInvoke.GetClientRect(hwnd, out RECT clientRect);
                FillWithTransparencyColor(hdc, clientRect);

                // Only draw the grid when a valid region has been set.
                if (_currentBounds.right > _currentBounds.left && _currentBounds.bottom > _currentBounds.top)
                    PaintGrid(hdc);
            }
            finally
            {
                PInvoke.EndPaint(hwnd, &ps);
            }
        }

        return default(LRESULT);
    }

    private LRESULT OnWmDestroy()
    {
        PInvoke.PostQuitMessage(0);
        return default(LRESULT);
    }

    private void PaintGrid(HDC hdc)
    {
        // Create a cyan pen (RGB 0,255,255 — Windows BGR format: 0xFFFF00).
        HPEN cyanPen = PInvoke.CreatePen(PEN_STYLE.PS_SOLID, 2, new COLORREF(0xFFFF00));
        HGDIOBJ oldPen = PInvoke.SelectObject(hdc, cyanPen);

        try
        {
            // Convert from virtual-screen coordinates to window-client coordinates.
            int left = _currentBounds.left - _virtualOriginX;
            int top = _currentBounds.top - _virtualOriginY;
            int right = _currentBounds.right - _virtualOriginX;
            int bottom = _currentBounds.bottom - _virtualOriginY;

            int cellWidth = (right - left) / 3;
            int cellHeight = (bottom - top) / 3;

            unsafe
            {
                DrawOuterBorder(hdc, left, top, right, bottom);
                DrawInnerGridLines(hdc, left, top, right, bottom, cellWidth, cellHeight);
            }
        }
        finally
        {
            PInvoke.SelectObject(hdc, oldPen);
            PInvoke.DeleteObject(cyanPen);
        }
    }

    /// <summary>Fills the entire client area with the transparency color key, erasing previous content.</summary>
    private static void FillWithTransparencyColor(HDC hdc, RECT clientRect)
    {
        // BGR 0x010000 is the nearly-black color treated as fully transparent by SetLayeredWindowAttributes.
        HBRUSH keyBrush = PInvoke.CreateSolidBrush(new COLORREF(0x010000));
        HGDIOBJ oldBrush = PInvoke.SelectObject(hdc, keyBrush);
        try
        {
            unsafe
            {
                PInvoke.PatBlt(hdc, clientRect.left, clientRect.top,
                    clientRect.right - clientRect.left,
                    clientRect.bottom - clientRect.top,
                    (ROP_CODE)0x00F00021); // PATCOPY
            }
        }
        finally
        {
            PInvoke.SelectObject(hdc, oldBrush);
            PInvoke.DeleteObject(keyBrush);
        }
    }

    private static unsafe void DrawOuterBorder(HDC hdc, int left, int top, int right, int bottom)
    {
        PInvoke.MoveToEx(hdc, left, top, null); PInvoke.LineTo(hdc, right, top);
        PInvoke.MoveToEx(hdc, right, top, null); PInvoke.LineTo(hdc, right, bottom);
        PInvoke.MoveToEx(hdc, right, bottom, null); PInvoke.LineTo(hdc, left, bottom);
        PInvoke.MoveToEx(hdc, left, bottom, null); PInvoke.LineTo(hdc, left, top);
    }

    private static unsafe void DrawInnerGridLines(HDC hdc, int left, int top, int right, int bottom, int cellWidth, int cellHeight)
    {
        // Two vertical dividers at 1/3 and 2/3 of the width.
        PInvoke.MoveToEx(hdc, left + cellWidth, top, null); PInvoke.LineTo(hdc, left + cellWidth, bottom);
        PInvoke.MoveToEx(hdc, left + 2 * cellWidth, top, null); PInvoke.LineTo(hdc, left + 2 * cellWidth, bottom);

        // Two horizontal dividers at 1/3 and 2/3 of the height.
        PInvoke.MoveToEx(hdc, left, top + cellHeight, null); PInvoke.LineTo(hdc, right, top + cellHeight);
        PInvoke.MoveToEx(hdc, left, top + 2 * cellHeight, null); PInvoke.LineTo(hdc, right, top + 2 * cellHeight);
    }
}
