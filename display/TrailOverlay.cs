using System;
using System.Drawing;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace keyboardmouse.display;

/// <summary>
/// Animated trail overlay that shows a polygon ribbon transitioning from one point to another
/// over ~1 second with customizable easing.
/// </summary>
internal sealed class TrailOverlay : IDisposable
{
    /// <summary>
    /// Easing function for animation (t in [0,1] → eased t). Swap to change animation feel.
    /// Example: `TrailOverlay.EasingFunction = t => t` for linear.
    /// </summary>
    public static Func<double, double> EasingFunction = EaseInOutQuadratic;

    private static TrailOverlay? s_instance;
    private bool _disposed;

    internal static TrailOverlay? Instance => s_instance;

    private HWND _hwnd = HWND.Null;
    private int _virtualOriginX;
    private int _virtualOriginY;

    private Point _from;
    private Point _to;
    private bool _animating = false;
    private long _animStartMs = 0;

    private const nuint AnimTimerId = 42;

    private const WINDOW_EX_STYLE OverlayExStyle =
        WINDOW_EX_STYLE.WS_EX_LAYERED |
        WINDOW_EX_STYLE.WS_EX_TRANSPARENT |
        WINDOW_EX_STYLE.WS_EX_TOPMOST |
        WINDOW_EX_STYLE.WS_EX_TOOLWINDOW |
        WINDOW_EX_STYLE.WS_EX_NOACTIVATE;

    // Use the same nearly-black color key as the other overlay so we can erase by filling with this color.
    private const int TransparentColor = 0x010000; // BGR format

    // Animation constants
    private const double AnimationDurationMs = 250.0;
    private const double DrawPhaseEndFraction = 0.6;  // draw-on phase ends at 60% of animation
    private const double FadePhaseStartFraction = 0.5; // fade starts at 50%, overlapping slightly
    private const int TailHalfWidth = 2;  // pixels from center to edge at tail
    private const int TipHalfWidth = 10;  // pixels from center to edge at tip
    private const int TargetTimerFps = 125;  // Guarantees ≥1 invalidation per vblank at 60Hz and 100Hz monitors
    private const int TimerIntervalMs = 1000 / TargetTimerFps;  // 8ms

    /// <summary>Ease-in-out quadratic: 2t² for t<0.5, 1-(-2t+2)²/2 for t≥0.5.</summary>
    private static double EaseInOutQuadratic(double t)
    {
        if (t < 0.5)
            return 2 * t * t;
        return 1 - Math.Pow(-2 * t + 2, 2) / 2;
    }

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
            if (_animating)
            {
                PInvoke.KillTimer(_hwnd, AnimTimerId);
                _animating = false;
            }
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
    /// Starts an animated trail from <paramref name="from"/> to <paramref name="to"/>.
    /// If already animating, replaces the current animation in place.
    /// </summary>
    public void ShowLine(Point from, Point to)
    {
        _from = from;
        _to = to;
        _animStartMs = Environment.TickCount64;
        _animating = true;

        if (_hwnd == HWND.Null) return;

        // Reset alpha in case a prior fade left it reduced.
        PInvoke.SetLayeredWindowAttributes(
            _hwnd,
            new COLORREF(TransparentColor),
            255,
            LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_COLORKEY | LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA
        );

        // Fire at TargetTimerFps to ensure smooth animation across monitor refresh rates.
        PInvoke.SetTimer(_hwnd, AnimTimerId, TimerIntervalMs, null);
        PInvoke.InvalidateRect(_hwnd, null, true);
    }

    private static LRESULT WindowProcedure(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (s_instance == null) return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);

        return msg switch
        {
            PInvoke.WM_PAINT => s_instance.OnWmPaint(hwnd),
            PInvoke.WM_TIMER => s_instance.OnWmTimer(),
            PInvoke.WM_DESTROY => OnWmDestroy(),
            PInvoke.WM_SIZE => OnWmSize(hwnd, wParam),
            _ => PInvoke.DefWindowProc(hwnd, msg, wParam, lParam)
        };
    }

    private LRESULT OnWmTimer()
    {
        if (!_animating)
            return default;

        double elapsed = Environment.TickCount64 - _animStartMs;
        double t = Math.Clamp(elapsed / AnimationDurationMs, 0.0, 1.0);

        if (t >= 1.0)
        {
            _animating = false;
            PInvoke.KillTimer(_hwnd, AnimTimerId);

            // Restore full alpha and clear the window.
            PInvoke.SetLayeredWindowAttributes(
                _hwnd,
                new COLORREF(TransparentColor),
                255,
                LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_COLORKEY | LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA
            );
            InvalidateLine();
            return default;
        }

        // During fade phase, reduce window-level alpha.
        if (t > FadePhaseStartFraction)
        {
            double fadeFraction = (t - FadePhaseStartFraction) / (1.0 - FadePhaseStartFraction);
            double easedFade = EasingFunction(fadeFraction);
            byte alpha = (byte)(255 * (1.0 - easedFade));
            PInvoke.SetLayeredWindowAttributes(
                _hwnd,
                new COLORREF(TransparentColor),
                alpha,
                LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_COLORKEY | LAYERED_WINDOW_ATTRIBUTES_FLAGS.LWA_ALPHA
            );
        }

        InvalidateLine();
        return default;
    }

    /// <summary>
    /// Invalidates only the bounding corridor of the trail line,
    /// avoiding a full virtual-screen repaint on every tick.
    /// </summary>
    private unsafe void InvalidateLine()
    {
        RECT r = ComputeLineBoundingRect();
        PInvoke.InvalidateRect(_hwnd, &r, true);
    }

    private LRESULT OnWmPaint(HWND hwnd)
    {
        HDC hdc = PInvoke.BeginPaint(hwnd, out PAINTSTRUCT ps);
        try
        {
            if (!_animating)
            {
                FillWithTransparencyColor(hdc, ps.rcPaint);
                return default;
            }

            double elapsed = Environment.TickCount64 - _animStartMs;
            double t = Math.Clamp(elapsed / AnimationDurationMs, 0.0, 1.0);

            // Eased draw progress: phase 1 maps t=[0, DrawPhaseEndFraction] → progress=[0,1].
            double rawDrawT = Math.Clamp(t / DrawPhaseEndFraction, 0.0, 1.0);
            double drawProgress = EasingFunction(rawDrawT);

            Point[] verts = ComputePolygonVertices(drawProgress);

            int bmpX = ps.rcPaint.left;
            int bmpY = ps.rcPaint.top;
            int w = ps.rcPaint.right - bmpX;
            int h = ps.rcPaint.bottom - bmpY;
            if (w <= 0 || h <= 0) return default;

            HDC memDC = PInvoke.CreateCompatibleDC(hdc);
            HBITMAP memBmp = PInvoke.CreateCompatibleBitmap(hdc, w, h);
            HGDIOBJ oldBmp = PInvoke.SelectObject(memDC, memBmp);

            try
            {
                // Fill entirely with transparency key so the window composites correctly.
                FillWithTransparencyColor(memDC, new RECT { left = 0, top = 0, right = w, bottom = h });

                // Shift polygon vertices into bitmap-local coordinates.
                for (int i = 0; i < verts.Length; i++)
                    verts[i] = new Point(verts[i].X - bmpX, verts[i].Y - bmpY);

                // Draw filled polygon: null pen avoids border bleed, white brush fills shape.
                HPEN nullPen = PInvoke.CreatePen(PEN_STYLE.PS_NULL, 0, new COLORREF(0));
                HBRUSH whiteBrush = PInvoke.CreateSolidBrush(new COLORREF(0xFFFFFF));
                HGDIOBJ oldPen = PInvoke.SelectObject(memDC, nullPen);
                HGDIOBJ oldBrush = PInvoke.SelectObject(memDC, whiteBrush);
                try
                {
                    PInvoke.Polygon(memDC, verts.AsSpan());
                }
                finally
                {
                    PInvoke.SelectObject(memDC, oldPen);
                    PInvoke.SelectObject(memDC, oldBrush);
                    PInvoke.DeleteObject(nullPen);
                    PInvoke.DeleteObject(whiteBrush);
                }

                const ROP_CODE SrcCopy = (ROP_CODE)0x00CC0020;
                PInvoke.BitBlt(hdc, bmpX, bmpY, w, h, memDC, 0, 0, SrcCopy);
            }
            finally
            {
                // Restore the original bitmap and use the returned HGDIOBJ to delete memBmp.
                HGDIOBJ bmpAsGdiObj = PInvoke.SelectObject(memDC, oldBmp);
                PInvoke.DeleteObject(bmpAsGdiObj);
                PInvoke.DeleteDC(memDC);
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

    private static LRESULT OnWmSize(HWND hwnd, WPARAM wParam)
    {
        // Defend against external minimization (e.g. residual Win+D handling on older shell builds).
        const nuint SIZE_MINIMIZED = 1;
        if (wParam.Value == SIZE_MINIMIZED)
            PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
        return default;
    }

    /// <summary>
    /// Computes 4 client-coordinate vertices forming the trail polygon at the given draw progress.
    /// Vertex order: tail-left, tail-right, tip-right, tip-left (convex quad, suitable for Polygon()).
    /// </summary>
    /// <param name="drawProgress">0 = collapsed at tail, 1 = full extent to <c>_to</c>.</param>
    private Point[] ComputePolygonVertices(double drawProgress)
    {
        double dx = _to.X - _from.X;
        double dy = _to.Y - _from.Y;
        double len = Math.Sqrt(dx * dx + dy * dy);

        // Perpendicular unit vector (rotate direction 90 degrees).
        double px, py;
        if (len < 0.001)
        {
            px = 1.0;
            py = 0.0;
        }
        else
        {
            px = -dy / len;
            py = dx / len;
        }

        // Tip position lerped along the line at current progress.
        double tipX = _from.X + dx * drawProgress;
        double tipY = _from.Y + dy * drawProgress;

        int tailCX = _from.X - _virtualOriginX;
        int tailCY = _from.Y - _virtualOriginY;
        int tipCX = (int)Math.Round(tipX) - _virtualOriginX;
        int tipCY = (int)Math.Round(tipY) - _virtualOriginY;

        return
        [
            new Point((int)Math.Round(tailCX + px * TailHalfWidth), (int)Math.Round(tailCY + py * TailHalfWidth)),
            new Point((int)Math.Round(tailCX - px * TailHalfWidth), (int)Math.Round(tailCY - py * TailHalfWidth)),
            new Point((int)Math.Round(tipCX  - px * TipHalfWidth),  (int)Math.Round(tipCY  - py * TipHalfWidth)),
            new Point((int)Math.Round(tipCX  + px * TipHalfWidth),  (int)Math.Round(tipCY  + py * TipHalfWidth)),
        ];
    }

    /// <summary>
    /// Returns the bounding rect of the whole potential trail corridor (tail → _to),
    /// padded by TipHalfWidth + a few pixels. Used to restrict InvalidateRect to the
    /// trail area rather than repainting the full virtual screen on every tick.
    /// </summary>
    private RECT ComputeLineBoundingRect()
    {
        // Use the full-extent vertices (drawProgress = 1) so the rect always covers
        // every possible polygon position during the animation.
        Point[] verts = ComputePolygonVertices(1.0);

        int minX = int.MaxValue, minY = int.MaxValue;
        int maxX = int.MinValue, maxY = int.MinValue;
        foreach (Point v in verts)
        {
            if (v.X < minX) minX = v.X;
            if (v.Y < minY) minY = v.Y;
            if (v.X > maxX) maxX = v.X;
            if (v.Y > maxY) maxY = v.Y;
        }

        int pad = TipHalfWidth + 4;
        return new RECT { left = minX - pad, top = minY - pad, right = maxX + pad, bottom = maxY + pad };
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
