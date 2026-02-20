using Windows.Win32;
using Windows.Win32.Foundation;

namespace keyboardmouse;

/// <summary>
/// Grid-based mouse navigation over the monitor containing the cursor.
///
/// On activation the monitor is captured as the initial region. Navigation
/// commands either drill the region down into a sub-cell or jump to a monitor
/// half and reset the region. Tap counting and sequencing are handled by
/// <see cref="GridInputHandler"/>; this class only acts on resolved commands.
/// </summary>
internal sealed class GridNavigator
{
    private const int GridDivisions = 3;

    /// <summary>Minimum cell edge in pixels — subdivision stops when reached.</summary>
    private const int MinCellPx = 4;

    private bool _isActive;
    private RECT _monitorRect;   // full monitor rect captured at activation
    private RECT _currentBounds; // current sub-divided region

    public bool IsActive => _isActive;

    internal void Activate()
    {
        _monitorRect = GetMonitorAtCursor();
        _currentBounds = _monitorRect;
        _isActive = true;
    }

    internal void Deactivate()
    {
        _isActive = false;
        _monitorRect = default;
        _currentBounds = default;
    }

    /// <summary>
    /// Acts on a resolved navigation command from <see cref="GridInputHandler"/>.
    ///   - Small sequence (< 3 taps): drill down one level into the current region.
    ///   - Triple tap: jump to a monitor half and reset the region.
    /// </summary>
    internal void Execute(int col, int row, int taps)
    {
        if (!_isActive) return;

        if (taps >= 3)
            JumpToHalf(col, row);
        else
            DrillDown(col, row);
    }

    // -------------------------------------------------------------------------

    private void DrillDown(int col, int row)
    {
        int w = _currentBounds.right - _currentBounds.left;
        int h = _currentBounds.bottom - _currentBounds.top;

        if (w / GridDivisions < MinCellPx || h / GridDivisions < MinCellPx) return;

        int cellW = w / GridDivisions;
        int cellH = h / GridDivisions;

        _currentBounds = new RECT
        {
            left = _currentBounds.left + col * cellW,
            top = _currentBounds.top + row * cellH,
            right = _currentBounds.left + (col + 1) * cellW,
            bottom = _currentBounds.top + (row + 1) * cellH,
        };

        MoveToCenterOf(_currentBounds);
    }

    // col 0 → left half,  col 2 → right half,  col 1 → full width
    // row 0 → top half,   row 2 → bottom half, row 1 → full height
    private void JumpToHalf(int col, int row)
    {
        int midX = _monitorRect.left + (_monitorRect.right - _monitorRect.left) / 2;
        int midY = _monitorRect.top + (_monitorRect.bottom - _monitorRect.top) / 2;

        RECT target = _monitorRect;

        if (col == 0) target.right = midX;
        else if (col == 2) target.left = midX;

        if (row == 0) target.bottom = midY;
        else if (row == 2) target.top = midY;

        _currentBounds = target;
        MoveToCenterOf(_currentBounds);
    }

    private static void MoveToCenterOf(RECT r) =>
        MouseInput.MoveTo((r.left + r.right) / 2, (r.top + r.bottom) / 2);

    private static RECT GetMonitorAtCursor()
    {
        PInvoke.GetCursorPos(out System.Drawing.Point cursor);

        foreach (RECT r in DisplayInfo.GetMonitorRects())
        {
            if (cursor.X >= r.left && cursor.X < r.right &&
                cursor.Y >= r.top && cursor.Y < r.bottom)
                return r;
        }

        RECT[] rects = DisplayInfo.GetMonitorRects();
        return rects.Length > 0 ? rects[0] : DisplayInfo.GetVirtualScreenRect();
    }
}
