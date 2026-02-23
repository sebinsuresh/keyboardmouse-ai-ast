using keyboardmouse.display;
using keyboardmouse.input;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace keyboardmouse.navigation;

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
    private readonly Stack<RECT> _history = new(); // navigation history for back navigation

    internal bool IsActive => _isActive;
    internal Action<RECT>? OnBoundsChanged { get; set; }

    internal void Activate()
    {
        _monitorRect = GetMonitorAtCursorPosition();
        _currentBounds = _monitorRect;
        _history.Clear();
        _isActive = true;
        OnBoundsChanged?.Invoke(_currentBounds);
    }

    internal void Deactivate()
    {
        _isActive = false;
        _monitorRect = default;
        _currentBounds = default;
        _history.Clear();
        OnBoundsChanged?.Invoke(default);
    }

    /// <summary>Acts on a resolved command from <see cref="GridInputHandler"/>.</summary>
    internal void Execute(GridCommand command)
    {
        if (!_isActive) return;

        switch (command)
        {
            case DrillCommand drill:
                DrillDown(drill.Col, drill.Row);
                break;
            case ResetCommand reset:
                _history.Clear();
                JumpToHalf(reset.Col, reset.Row);
                break;
            case BackCommand:
                GoBack();
                break;
        }
    }

    private void DrillDown(int col, int row)
    {
        int w = _currentBounds.right - _currentBounds.left;
        int h = _currentBounds.bottom - _currentBounds.top;

        if (w / GridDivisions < MinCellPx || h / GridDivisions < MinCellPx) return;

        _history.Push(_currentBounds);

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
        OnBoundsChanged?.Invoke(_currentBounds);
    }

    /// <summary>Navigate back to the previous grid level. No-op if history is empty.</summary>
    private void GoBack()
    {
        if (_history.Count == 0) return;

        _currentBounds = _history.Pop();
        MoveToCenterOf(_currentBounds);
        OnBoundsChanged?.Invoke(_currentBounds);
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
        OnBoundsChanged?.Invoke(_currentBounds);
    }

    private static void MoveToCenterOf(RECT r)
    {
        int targetX = (r.left + r.right) / 2;
        int targetY = (r.top + r.bottom) / 2;

        // capture previous cursor position, perform the instant move, then show a one-shot trail line
        PInvoke.GetCursorPos(out System.Drawing.Point prev);
        MouseInput.MoveTo(targetX, targetY);
        TrailOverlay.Instance?.ShowLine(prev, new System.Drawing.Point(targetX, targetY));
    }

    private static RECT GetMonitorAtCursorPosition()
    {
        PInvoke.GetCursorPos(out System.Drawing.Point cursor);
        return FindMonitorContaining(cursor.X, cursor.Y);
    }

    /// <summary>
    /// Returns the monitor rectangle that contains the given point.
    /// Falls back to the first known monitor, or the virtual screen if no monitors are found.
    /// </summary>
    private static RECT FindMonitorContaining(int x, int y)
    {
        var monitors = DisplayInfo.GetMonitorRects();

        foreach (RECT monitor in monitors)
        {
            if (x >= monitor.left && x < monitor.right &&
                y >= monitor.top && y < monitor.bottom)
                return monitor;
        }

        return monitors[0];
    }
}
