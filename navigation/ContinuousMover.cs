using Windows.Win32;
using Windows.Win32.Foundation;

namespace keyboardmouse.navigation;

/// <summary>
/// Drives continuous mouse movement using a Win32 <c>SetTimer</c>/<c>WM_TIMER</c> tick.
/// Fires on the message-loop thread so no cross-thread coordination is needed.
///
/// Call <see cref="Start"/> with a directional delta (each component -1, 0, or 1) to begin
/// movement; call <see cref="Stop"/> (or release will arrive via <see cref="Stop"/>) to halt.
/// The owner window must forward <c>WM_TIMER</c> events with the matching ID to <see cref="Tick"/>.
/// </summary>
internal sealed class ContinuousMover : IDisposable
{
    /// <summary>Tick interval in milliseconds (~60fps).</summary>
    internal const int TickMs = 16;

    /// <summary>Pixels moved per tick in each axis direction.</summary>
    internal const int StepPx = 8;

    private static readonly nuint TimerId = 1;

    private readonly HWND _hwnd;
    private readonly Action<int, int> _onTick; // (deltaX, deltaY)
    private int _dx;
    private int _dy;
    private bool _running;

    internal ContinuousMover(HWND hwnd, Action<int, int> onTick)
    {
        _hwnd = hwnd;
        _onTick = onTick;
    }

    /// <summary>
    /// Starts (or redirects) continuous movement in the given direction.
    /// <paramref name="dx"/> and <paramref name="dy"/> are each -1, 0, or 1.
    /// Safe to call while already running — updates direction without restarting the timer.
    /// </summary>
    internal void Start(int dx, int dy)
    {
        _dx = dx;
        _dy = dy;

        if (!_running)
        {
            PInvoke.SetTimer(_hwnd, TimerId, TickMs, null);
            _running = true;
        }
    }

    /// <summary>Stops movement. No-op if not currently running.</summary>
    internal void Stop()
    {
        if (!_running) return;

        PInvoke.KillTimer(_hwnd, TimerId);
        _running = false;
    }

    /// <summary>
    /// Advance one tick. Called by the owner window's <c>WM_TIMER</c> handler when
    /// the timer ID matches <see cref="TimerId"/>.
    /// </summary>
    internal void Tick()
    {
        if (!_running) return;
        _onTick(_dx * StepPx, _dy * StepPx);
    }

    /// <summary>Exposes the timer ID so the owner window can route WM_TIMER correctly.</summary>
    internal static nuint Id => TimerId;

    public void Dispose() => Stop();
}
