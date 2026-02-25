using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;
using keyboardmouse.input;
using System.Runtime.InteropServices;

namespace keyboardmouse.display;

/// <summary>
/// Topmost click-through layered overlay window displaying a 3×3 grid.
/// </summary>
internal sealed class GridOverlay
{
    private static GridOverlay? instance;
    private static DeleteObjectSafeHandle? _fontHandle;
    private static HFONT _hFont;

    private HWND _windowHandle = HWND.Null;
    private RECT _currentBounds = default;
    private int _virtualOriginX;
    private int _virtualOriginY;
    private GridLineCache? _lineCache;

    /// <summary>Maximum number of distinct grid sizes to cache. 0 means unlimited.</summary>
    public int CacheDepths { get; set; } = 0;

    private const WINDOW_EX_STYLE OverlayExStyle =
        WINDOW_EX_STYLE.WS_EX_LAYERED |
        WINDOW_EX_STYLE.WS_EX_TRANSPARENT |
        WINDOW_EX_STYLE.WS_EX_TOPMOST |
        WINDOW_EX_STYLE.WS_EX_NOACTIVATE;

    // Set the color key for transparency: RGB(0, 0, 1) — the nearly-black pixel
    private const int TransparentColor = 0x010000; // BGR format
    private const ROP_CODE ReplaceWPatternRasterOpCode = (ROP_CODE)0x00F00021; // PATCOPY
    private const byte alpha = 140; // range: 1 - 255

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

        _lineCache = new GridLineCache(CacheDepths);
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

    /// <summary>Exposes the window handle for use by timer-based components.</summary>
    internal HWND Handle => _windowHandle;

    public void Destroy()
    {
        if (_windowHandle != HWND.Null)
        {
            PInvoke.DestroyWindow(_windowHandle);
            _windowHandle = HWND.Null;
        }

        _lineCache?.Dispose();
        _lineCache = null;
        _fontHandle?.Dispose();
        _fontHandle = null;
        instance = null;
    }

    /// <summary>
    /// Called on each WM_TIMER tick — wire this to the navigator's <see cref="navigation.GridNavigator.TimerTick"/> method.
    /// </summary>
    internal Action? OnTimer { get; set; }

    private static LRESULT WindowProcedure(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (instance == null) return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);

        return msg switch
        {
            PInvoke.WM_PAINT => instance.OnWmPaint(hwnd),
            PInvoke.WM_DESTROY => OnWmDestroy(),
            PInvoke.WM_TIMER => instance.OnWmTimer(),
            _ => PInvoke.DefWindowProc(hwnd, msg, wParam, lParam)
        };
    }

    private LRESULT OnWmTimer()
    {
        OnTimer?.Invoke();
        return (LRESULT)0;
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

    private void PaintGrid(HDC hdc)
    {
        // Convert from virtual-screen coordinates to window-client coordinates.
        int left = _currentBounds.left - _virtualOriginX;
        int top = _currentBounds.top - _virtualOriginY;
        int right = _currentBounds.right - _virtualOriginX;
        int bottom = _currentBounds.bottom - _virtualOriginY;

        int cellWidth = (right - left) / 3;
        int cellHeight = (bottom - top) / 3;

        PaintGridLines(hdc, left, top, cellWidth, cellHeight);
        DrawCellLabels(hdc, left, top, cellWidth, cellHeight);
    }

    /// <summary>Paints grid lines using the cache: blits from cache on hit, renders and stores on miss.</summary>
    private void PaintGridLines(HDC hdc, int left, int top, int cellWidth, int cellHeight)
    {
        int w = cellWidth * 3;
        int h = cellHeight * 3;
        var key = (w, h);

        if (!_lineCache!.TryGet(key, out HBITMAP bitmap))
        {
            bitmap = RenderLinesToBitmap(hdc, w, h, cellWidth, cellHeight);
            _lineCache.Store(key, bitmap);
        }

        // Blit w+1 × h+1: the extra pixel accommodates the border drawn at coordinates (w, *) and (*, h).
        BlitBitmap(hdc, bitmap, left, top, w + 1, h + 1);
    }

    /// <summary>Renders grid lines into an off-screen bitmap. The returned HBITMAP is owned by the cache.</summary>
    private static HBITMAP RenderLinesToBitmap(HDC referenceDC, int w, int h, int cellWidth, int cellHeight)
    {
        // Allocate 1 extra pixel on each edge so border lines drawn at x=w and y=h are within bounds.
        HDC memDC = PInvoke.CreateCompatibleDC(referenceDC);
        HBITMAP bitmap = PInvoke.CreateCompatibleBitmap(referenceDC, w + 1, h + 1);
        HGDIOBJ oldBitmap = PInvoke.SelectObject(memDC, bitmap);

        try
        {
            RECT rect = new() { left = 0, top = 0, right = w + 1, bottom = h + 1 };
            FillWithTransparencyColor(memDC, rect);
            GridLineRenderer.DrawGridLines(memDC, cellWidth, cellHeight);
        }
        finally
        {
            PInvoke.SelectObject(memDC, oldBitmap);
            PInvoke.DeleteDC(memDC);
        }

        return bitmap;
    }

    /// <summary>Blits a standalone bitmap onto the destination DC at the given position.</summary>
    private static void BlitBitmap(HDC destDC, HBITMAP bitmap, int x, int y, int w, int h)
    {
        HDC memDC = PInvoke.CreateCompatibleDC(destDC);
        HGDIOBJ oldBitmap = PInvoke.SelectObject(memDC, bitmap);
        try
        {
            PInvoke.BitBlt(destDC, x, y, w, h, memDC, 0, 0, ROP_CODE.SRCCOPY);
        }
        finally
        {
            PInvoke.SelectObject(memDC, oldBitmap);
            PInvoke.DeleteDC(memDC);
        }
    }

    /// <summary>Draw key labels in the top-left of each cell.</summary>
    private static void DrawCellLabels(HDC deviceCtxHandle, int left, int top, int cellWidth, int cellHeight)
    {
        const int padding = 6;

        // Hide labels if grid cells are too small.
        if (cellHeight < 54)
        {
            return;
        }

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
