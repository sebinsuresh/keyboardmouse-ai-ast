using System;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace keyboardmouse;

/// <summary>
/// Grid-based mouse navigation.
///
/// While active, the current monitor is divided into a <see cref="GridDivisions"/>×<see cref="GridDivisions"/>
/// grid. Each <see cref="NavigateTo"/> call moves the mouse to the center of the selected cell and
/// narrows the active region to that cell. A follow-up call within <see cref="SubdivideWindowMs"/> ms
/// sub-divides the narrowed region; after the window expires the grid resets to the full monitor.
/// Sub-division halts when cells reach <see cref="MinCellPx"/> pixels in either dimension.
/// </summary>
internal sealed class GridNavigator
{
    // -------------------------------------------------------------------------
    // Configuration constants — easy to expose as settings in future
    // -------------------------------------------------------------------------

    /// <summary>Number of columns/rows in the grid.</summary>
    private const int GridDivisions = 3;

    /// <summary>Minimum cell edge in pixels. Sub-division stops when reached.</summary>
    private const int MinCellPx = 4;

    /// <summary>Window (ms) within which a follow-up key press triggers sub-division.</summary>
    private const int SubdivideWindowMs = 500;

    // -------------------------------------------------------------------------
    // Instance state
    // -------------------------------------------------------------------------

    private bool _isActive;
    private RECT _monitorRect;    // full monitor rect captured at activation
    private RECT _currentBounds;  // current sub-divided region
    private long _lastMoveTickMs; // Environment.TickCount64 at last move; 0 = none

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Whether grid navigation mode is currently active.</summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// Activates grid mode. Detects the monitor that contains the cursor and
    /// uses it as the initial grid region.
    /// </summary>
    public void Activate()
    {
        _monitorRect = DetectCurrentMonitorRect();
        _currentBounds = _monitorRect;
        _lastMoveTickMs = 0; // no previous move; first key always resets to monitor bounds
        _isActive = true;
    }

    /// <summary>Deactivates grid mode and resets all navigation state.</summary>
    public void Deactivate()
    {
        _isActive = false;
        _monitorRect = default;
        _currentBounds = default;
        _lastMoveTickMs = 0;
    }

    /// <summary>
    /// Moves the mouse to the center of grid cell (<paramref name="col"/>, <paramref name="row"/>)
    /// within the current region, then narrows the active region to that cell for sub-division.
    /// No-op when inactive.
    /// </summary>
    public void NavigateTo(int col, int row)
    {
        if (!_isActive) return;

        long now = Environment.TickCount64;
        if (_lastMoveTickMs == 0 || (now - _lastMoveTickMs) > SubdivideWindowMs)
            _currentBounds = _monitorRect;

        int boundsW = _currentBounds.right - _currentBounds.left;
        int boundsH = _currentBounds.bottom - _currentBounds.top;

        // Once cells are at or below the minimum size, stop subdividing.
        // Still move to center so the user keeps control.
        if (boundsW / GridDivisions < MinCellPx || boundsH / GridDivisions < MinCellPx)
        {
            MouseInput.MoveTo(
                (_currentBounds.left + _currentBounds.right) / 2,
                (_currentBounds.top + _currentBounds.bottom) / 2);
            _lastMoveTickMs = now;
            return;
        }

        int cellW = boundsW / GridDivisions;
        int cellH = boundsH / GridDivisions;

        RECT selected = new()
        {
            left = _currentBounds.left + col * cellW,
            top = _currentBounds.top + row * cellH,
            right = _currentBounds.left + (col + 1) * cellW,
            bottom = _currentBounds.top + (row + 1) * cellH,
        };

        MouseInput.MoveTo(
            (selected.left + selected.right) / 2,
            (selected.top + selected.bottom) / 2);

        _currentBounds = selected;
        _lastMoveTickMs = now;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static RECT DetectCurrentMonitorRect()
    {
        // GetCursorPos uses System.Drawing.Point as directed by CsWin32 (POINT alias).
        System.Drawing.Point cursor = default;
        PInvoke.GetCursorPos(out cursor);

        foreach (var rect in DisplayInfo.GetMonitorRects())
        {
            if (cursor.X >= rect.left && cursor.X < rect.right &&
                cursor.Y >= rect.top && cursor.Y < rect.bottom)
                return rect;
        }

        // Fallback: first monitor, or virtual screen if none reported.
        var rects = DisplayInfo.GetMonitorRects();
        return rects.Length > 0 ? rects[0] : DisplayInfo.GetVirtualScreenRect();
    }
}
