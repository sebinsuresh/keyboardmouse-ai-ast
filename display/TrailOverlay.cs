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
        WINDOW_EX_STYLE.WS_EX_NOACTIVATE;

    // Use the same nearly-black color key as the other overlay so we can erase by filling with this color.
    private const int TransparentColor = 0x010000; // BGR format

    // Animation constants
    private const double AnimationDurationMs = 1000.0;
    private const double DrawPhaseEndFraction = 0.6;  // draw-on phase ends at 60% of animation
    private const double FadePhaseStartFraction = 0.5; // fade starts at 50%, overlapping slightly
    private const int TailHalfWidth = 2;  // pixels from center to edge at tail
    private const int TipHalfWidth = 10;  // pixels from center to edge at tip

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

        // Start (or restart) the ~60fps animation timer.
        PInvoke.SetTimer(_hwnd, AnimTimerId, 16, null);
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
            PInvoke.InvalidateRect(_hwnd, null, true);
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

        PInvoke.InvalidateRect(_hwnd, null, true);
        return default;
    }

    private LRESULT OnWmPaint(HWND hwnd)
    {
        HDC hdc = PInvoke.BeginPaint(hwnd, out PAINTSTRUCT ps);

        try
        {
            PInvoke.GetClientRect(hwnd, out RECT clientRect);
            FillWithTransparencyColor(hdc, clientRect);

            if (_animating)
            {
                // Animated trail will be rendered here in a future step.
                // For now, just show the static line to avoid errors.
                int x1 = _from.X - _virtualOriginX;
                int y1 = _from.Y - _virtualOriginY;
                int x2 = _to.X - _virtualOriginX;
                int y2 = _to.Y - _virtualOriginY;

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
