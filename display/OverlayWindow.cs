using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using keyboardmouse.input;
using System.Runtime.InteropServices;

namespace keyboardmouse.display;

/// <summary>
/// Topmost click-through layered overlay window displaying a cyan 3×3 grid.
/// </summary>
internal sealed class OverlayWindow
{
    private static OverlayWindow? instance;
    private static DeleteObjectSafeHandle? _fontHandle;
    private static HFONT _hFont;

    private HWND _windowHandle = HWND.Null;
    private RECT _currentBounds = default;
    private int _virtualOriginX;
    private int _virtualOriginY;

    private const WINDOW_EX_STYLE OverlayExStyle =
        WINDOW_EX_STYLE.WS_EX_LAYERED |
        WINDOW_EX_STYLE.WS_EX_TRANSPARENT |
        WINDOW_EX_STYLE.WS_EX_TOPMOST |
        WINDOW_EX_STYLE.WS_EX_NOACTIVATE;

    // Set the color key for transparency: RGB(0, 0, 1) — the nearly-black pixel
    private const int TransparentColor = 0x010000; // BGR format
    private const ROP_CODE ReplaceWPatternRasterOpCode = (ROP_CODE)0x00F00021; // PATCOPY
    private const byte alpha = 191; // ~75%

    // Need unsafe for 
    internal static unsafe void RegisterWindowClass()
    {
        const string className = "GridOverlay";
        fixed (char* classPtr = className)
        {
            WNDCLASSW wndClass = new()
            {
                lpszClassName = (PCWSTR)classPtr,
                lpfnWndProc = WindowProcedure,
            };

            if (PInvoke.RegisterClass(wndClass) == 0)
            {
                throw new InvalidOperationException("Failed to register overlay window class.");
            }
        }
    }

    public void Create()
    {
        if (_windowHandle != HWND.Null) return;

        instance = this;

        // Get the full virtual screen to size the overlay window.
        RECT virtualScreen = DisplayInfo.GetVirtualScreenRect();
        _virtualOriginX = virtualScreen.left;
        _virtualOriginY = virtualScreen.top;

        unsafe
        {
            _windowHandle = PInvoke.CreateWindowEx(
                OverlayExStyle,
                "GridOverlay",
                "Grid Overlay",
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

        if (_windowHandle == HWND.Null)
        {
            throw new InvalidOperationException("Failed to create overlay window.");
        }

        PInvoke.SetLayeredWindowAttributes(
            _windowHandle,
            new COLORREF(TransparentColor),
            alpha,
            LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_COLORKEY | LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA
        );
    }

    public void Show()
    {
        if (_windowHandle == HWND.Null) return;
        PInvoke.ShowWindow(_windowHandle, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
    }

    public void Hide()
    {
        if (_windowHandle == HWND.Null) return;
        PInvoke.ShowWindow(_windowHandle, SHOW_WINDOW_CMD.SW_HIDE);
    }

    public void UpdateBounds(RECT bounds)
    {
        _currentBounds = bounds;
        if (_windowHandle != HWND.Null)
        {
            PInvoke.InvalidateRect(_windowHandle, null, true);
        }
    }

    public void Destroy()
    {
        if (_windowHandle != HWND.Null)
        {
            PInvoke.DestroyWindow(_windowHandle);
            _windowHandle = HWND.Null;
        }

        _fontHandle?.Dispose();
        _fontHandle = null;
        instance = null;
    }

    private static LRESULT WindowProcedure(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (instance == null) return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);

        return msg switch
        {
            PInvoke.WM_PAINT => instance.OnWmPaint(hwnd),
            PInvoke.WM_DESTROY => OnWmDestroy(),
            _ => PInvoke.DefWindowProc(hwnd, msg, wParam, lParam)
        };
    }

    private LRESULT OnWmPaint(HWND hwnd)
    {
        HDC deviceCtxHandle = PInvoke.BeginPaint(hwnd, out PAINTSTRUCT ps);

        try
        {
            PInvoke.GetClientRect(hwnd, out RECT clientRect);
            FillWithTransparencyColor(deviceCtxHandle, clientRect);

            // Only draw the grid when a valid region has been set.
            if (_currentBounds.right > _currentBounds.left && _currentBounds.bottom > _currentBounds.top)
            {
                PaintGrid(deviceCtxHandle);
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

    /// <summary>Fills the entire client area with the transparency color key, erasing previous content.</summary>
    private static void FillWithTransparencyColor(HDC deviceCtxHandle, RECT clientRect)
    {
        HBRUSH keyBrush = PInvoke.CreateSolidBrush(new COLORREF(TransparentColor));
        HGDIOBJ oldBrush = PInvoke.SelectObject(deviceCtxHandle, keyBrush);
        try
        {
            PInvoke.PatBlt(deviceCtxHandle, clientRect.left, clientRect.top,
                clientRect.right - clientRect.left,
                clientRect.bottom - clientRect.top,
                ReplaceWPatternRasterOpCode);
        }
        finally
        {
            // Restore the previous Windows drawing API GDI object and clean up the brush we created.
            PInvoke.SelectObject(deviceCtxHandle, oldBrush);
            PInvoke.DeleteObject(keyBrush);
        }
    }

    private void PaintGrid(HDC deviceCtxHandle)
    {
        // Create a cyan pen (RGB 0,255,255 — Windows BGR format: 0xFFFF00).
        HPEN cyanPen = PInvoke.CreatePen(PEN_STYLE.PS_SOLID, 1, new COLORREF(0xFFFF00));
        HGDIOBJ oldPen = PInvoke.SelectObject(deviceCtxHandle, cyanPen);

        try
        {
            // Convert from virtual-screen coordinates to window-client coordinates.
            int left = _currentBounds.left - _virtualOriginX;
            int top = _currentBounds.top - _virtualOriginY;
            int right = _currentBounds.right - _virtualOriginX;
            int bottom = _currentBounds.bottom - _virtualOriginY;

            int cellWidth = (right - left) / 3;
            int cellHeight = (bottom - top) / 3;

            DrawOuterBorder(deviceCtxHandle, left, top, right, bottom);
            DrawInnerGridLines(deviceCtxHandle, left, top, right, bottom, cellWidth, cellHeight);
            DrawCellLabels(deviceCtxHandle, left, top, cellWidth, cellHeight);
        }
        finally
        {
            // Restore the previous Windows drawing API GDI object and clean up the pen we created.
            PInvoke.SelectObject(deviceCtxHandle, oldPen);
            PInvoke.DeleteObject(cyanPen);
        }
    }

    private static void DrawLine(HDC deviceCtxHandle, int x1, int y1, int x2, int y2)
    {
        // The null parameter expects a pointer, but it is optional.
        // Need to mark the method as unsafe to pass null here without a pointer safety error.
        unsafe
        {
            PInvoke.MoveToEx(deviceCtxHandle, x1, y1, null);
        }
        PInvoke.LineTo(deviceCtxHandle, x2, y2);
    }

    private static void DrawOuterBorder(HDC deviceCtxHandle, int left, int top, int right, int bottom)
    {
        DrawLine(deviceCtxHandle, left, top, right, top);
        DrawLine(deviceCtxHandle, right, top, right, bottom);
        DrawLine(deviceCtxHandle, right, bottom, left, bottom);
        DrawLine(deviceCtxHandle, left, bottom, left, top);
    }

    private static void DrawInnerGridLines(
        HDC deviceCtxHandle,
        int left,
        int top,
        int right,
        int bottom,
        int cellWidth,
        int cellHeight)
    {
        // Two vertical dividers at 1/3 and 2/3 of the width.
        DrawLine(deviceCtxHandle, left + cellWidth, top, left + cellWidth, bottom);
        DrawLine(deviceCtxHandle, left + 2 * cellWidth, top, left + 2 * cellWidth, bottom);

        // Two horizontal dividers at 1/3 and 2/3 of the height.
        DrawLine(deviceCtxHandle, left, top + cellHeight, right, top + cellHeight);
        DrawLine(deviceCtxHandle, left, top + 2 * cellHeight, right, top + 2 * cellHeight);
    }

    /// <summary>Draw key labels in the top-left of each cell.</summary>
    private static void DrawCellLabels(HDC deviceCtxHandle, int left, int top, int cellWidth, int cellHeight)
    {
        const int padding = 6;
        SetBackgroundTransparent(deviceCtxHandle);

        HGDIOBJ oldFont = PInvoke.SelectObject(deviceCtxHandle, GetOrCreateFont());

        try
        {
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    if (!GridInputHandler.CellLabels.TryGetValue((col, row), out var label))
                    {
                        continue;
                    }

                    int cellLeft = left + col * cellWidth;
                    int cellTop = top + row * cellHeight;
                    int x = cellLeft + padding;
                    int y = cellTop + padding;

                    DrawOutlinedText(deviceCtxHandle, x, y, label);
                }
            }
        }
        finally
        {
            PInvoke.SelectObject(deviceCtxHandle, oldFont);
        }
    }

    private static HFONT GetOrCreateFont()
    {
        if (_fontHandle is null || _fontHandle.IsInvalid)
        {
            LOGFONTW lf = new()
            {
                lfHeight = -32,  // 24pt at 96 DPI
                lfWeight = 400,  // FW_NORMAL
                lfCharSet = FONT_CHARSET.DEFAULT_CHARSET,
                lfQuality = FONT_QUALITY.CLEARTYPE_QUALITY,
            };
            "Segoe UI".AsSpan().CopyTo(MemoryMarshal.CreateSpan(ref lf.lfFaceName[0], 32));
            _fontHandle = PInvoke.CreateFontIndirect(lf);
            _hFont = new HFONT(_fontHandle.DangerousGetHandle());
        }
        return _hFont;
    }

    private static void SetBackgroundTransparent(HDC hdc)
    {
        PInvoke.SetBkMode(hdc, BACKGROUND_MODE.TRANSPARENT);
    }

    private static void DrawOutlinedText(HDC hdc, int x, int y, string text)
    {
        // Draw a simple black outline by black text in 4 directions.
        PInvoke.SetTextColor(hdc, new COLORREF(0x000000));
        PInvoke.TextOut(hdc, x - 1, y, text, text.Length);
        PInvoke.TextOut(hdc, x + 1, y, text, text.Length);
        PInvoke.TextOut(hdc, x, y - 1, text, text.Length);
        PInvoke.TextOut(hdc, x, y + 1, text, text.Length);

        PInvoke.SetTextColor(hdc, new COLORREF(0xFFFFFF));
        PInvoke.TextOut(hdc, x, y, text, text.Length);
    }
}
