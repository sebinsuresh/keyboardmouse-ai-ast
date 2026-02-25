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
internal sealed class GridNavigator : IDisposable
{
    private const int GridDivisions = 3;

    /// <summary>Minimum cell edge in pixels — subdivision stops when reached.</summary>
    private const int MinCellPx = 4;

    /// <summary>Wraps a RECT with a flag distinguishing grid-aligned positions from manual moves.</summary>
    private record HistoryEntry(RECT Bounds, bool IsManual = false);

    private bool _isActive;
    private RECT _monitorRect;   // full monitor rect captured at activation
    private RECT _currentBounds; // current sub-divided region
    private readonly Stack<HistoryEntry> _history = new(); // navigation history for back navigation
    private readonly ContinuousMover _mover;

    internal bool IsActive => _isActive;
    internal Action<RECT>? OnBoundsChanged { get; set; }

    internal GridNavigator(Windows.Win32.Foundation.HWND timerOwner)
    {
        _mover = new ContinuousMover(timerOwner, OnMoveTick);
    }

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
        _mover.Stop();
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
            case ManualMoveCommand move:
                _mover.Start(move.Col - 1, move.Row - 1);
                break;
            case StopManualMoveCommand:
                _mover.Stop();
                break;
        }
    }

    private void DrillDown(int col, int row)
    {
        _mover.Stop();

        int w = _currentBounds.right - _currentBounds.left;
        int h = _currentBounds.bottom - _currentBounds.top;

        if (w / GridDivisions < MinCellPx || h / GridDivisions < MinCellPx) return;

        _history.Push(new HistoryEntry(_currentBounds));

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
        // Discard the manual upsert entry at the top (if any) — it's not a real undo target
        if (_history.Count > 0 && _history.Peek().IsManual)
            _history.Pop();

        if (_history.Count == 0) return;

        _currentBounds = _history.Pop().Bounds;
        MoveToCenterOf(_currentBounds);
        OnBoundsChanged?.Invoke(_currentBounds);
    }

    // col 0 → left half,  col 2 → right half,  col 1 → full width
    // row 0 → top half,   row 2 → bottom half, row 1 → full height
    private void JumpToHalf(int col, int row)
    {
        _mover.Stop();
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

    private void OnMoveTick(int dx, int dy)
    {
        if (!_isActive) return;

        _currentBounds = new RECT
        {
            left = _currentBounds.left + dx,
            top = _currentBounds.top + dy,
            right = _currentBounds.right + dx,
            bottom = _currentBounds.bottom + dy,
        };

        int centerX = (_currentBounds.left + _currentBounds.right) / 2;
        int centerY = (_currentBounds.top + _currentBounds.bottom) / 2;
        MouseInput.MoveTo(centerX, centerY);

        // Upsert: replace the top manual entry or push a new one
        if (_history.Count > 0 && _history.Peek().IsManual)
            _history.Pop();
        _history.Push(new HistoryEntry(_currentBounds, IsManual: true));

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

    /// <summary>Forward WM_TIMER events from the owner window to the continuous mover.</summary>
    internal void TimerTick() => _mover.Tick();

    public void Dispose() => _mover.Dispose();
}
