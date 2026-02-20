using System;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace keyboardmouse.forms;

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
                // Fill entire window with color-key color to establish transparency
                // and erase any previous content.
                RECT clientRect = default;
                PInvoke.GetClientRect(hwnd, out clientRect);
                HBRUSH keyBrush = PInvoke.CreateSolidBrush(new COLORREF(0x010000));
                HGDIOBJ oldBrush = PInvoke.SelectObject(hdc, keyBrush);
                try
                {
                    PInvoke.PatBlt(hdc, clientRect.left, clientRect.top,
                        clientRect.right - clientRect.left,
                        clientRect.bottom - clientRect.top,
                        (ROP_CODE)0x00F00021);
                }
                finally
                {
                    PInvoke.SelectObject(hdc, oldBrush);
                    PInvoke.DeleteObject(keyBrush);
                }

                if (_currentBounds.left == 0 && _currentBounds.right == 0 &&
                    _currentBounds.top == 0 && _currentBounds.bottom == 0)
                {
                    return default(LRESULT);
                }

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
        // Create a cyan pen (RGB 0, 255, 255 in Windows BGR format: 0xFFFF00).
        HPEN cyanPen = PInvoke.CreatePen(PEN_STYLE.PS_SOLID, 2, new COLORREF(0xFFFF00));
        HGDIOBJ oldPen = PInvoke.SelectObject(hdc, cyanPen);

        try
        {
            // Convert screen coordinates to client coordinates.
            int clientLeft = _currentBounds.left - _virtualOriginX;
            int clientTop = _currentBounds.top - _virtualOriginY;
            int clientRight = _currentBounds.right - _virtualOriginX;
            int clientBottom = _currentBounds.bottom - _virtualOriginY;

            int width = clientRight - clientLeft;
            int height = clientBottom - clientTop;
            int cellW = width / 3;
            int cellH = height / 3;

            unsafe
            {
                // Outer border (4 lines).
                PInvoke.MoveToEx(hdc, clientLeft, clientTop, null);
                PInvoke.LineTo(hdc, clientRight, clientTop);

                PInvoke.MoveToEx(hdc, clientRight, clientTop, null);
                PInvoke.LineTo(hdc, clientRight, clientBottom);

                PInvoke.MoveToEx(hdc, clientRight, clientBottom, null);
                PInvoke.LineTo(hdc, clientLeft, clientBottom);

                PInvoke.MoveToEx(hdc, clientLeft, clientBottom, null);
                PInvoke.LineTo(hdc, clientLeft, clientTop);

                // Vertical dividers.
                int x1 = clientLeft + cellW;
                int x2 = clientLeft + 2 * cellW;

                PInvoke.MoveToEx(hdc, x1, clientTop, null);
                PInvoke.LineTo(hdc, x1, clientBottom);

                PInvoke.MoveToEx(hdc, x2, clientTop, null);
                PInvoke.LineTo(hdc, x2, clientBottom);

                // Horizontal dividers.
                int y1 = clientTop + cellH;
                int y2 = clientTop + 2 * cellH;

                PInvoke.MoveToEx(hdc, clientLeft, y1, null);
                PInvoke.LineTo(hdc, clientRight, y1);

                PInvoke.MoveToEx(hdc, clientLeft, y2, null);
                PInvoke.LineTo(hdc, clientRight, y2);
            }
        }
        finally
        {
            PInvoke.SelectObject(hdc, oldPen);
            PInvoke.DeleteObject(cyanPen);
        }
    }
}
