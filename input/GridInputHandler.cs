using System;
using System.Collections.Generic;
using System.Threading;

namespace keyboardmouse.input;

/// <summary>
/// Translates key presses into resolved navigation commands and fires a callback.
///
/// Grid key layout (mirrors numpad positions):
///   u  i  o      top-left    top-center    top-right
///   j  k  l      mid-left    center        mid-right
///   m  ,  .      bot-left    bot-center    bot-right
///
/// Tap rules:
///   Single tap → drill down one level into the current region
///   Triple tap → reset history and jump to the corresponding monitor half
///
/// Back key:
///   h → navigate to the previous grid level (no tap sequencing)
/// </summary>
internal sealed class GridInputHandler : IDisposable
{
    private const int TapWindowMs = 300;
    private const int VK_H = 0x48;

    private static readonly Dictionary<int, (int Col, int Row)> s_keyMap = new()
    {
        [0x55] = (0, 0), // U — top-left
        [0x49] = (1, 0), // I — top-center
        [0x4F] = (2, 0), // O — top-right
        [0x4A] = (0, 1), // J — mid-left
        [0x4B] = (1, 1), // K — center
        [0x4C] = (2, 1), // L — mid-right
        [0x4D] = (0, 2), // M — bot-left
        [0xBC] = (1, 2), // , — bot-center
        [0xBE] = (2, 2), // . — bot-right
    };

    /// <summary>
    /// Public, extendable mapping from grid cell (col,row) to the display label shown in the overlay.
    /// </summary>
    public static readonly Dictionary<(int Col, int Row), string> CellLabels = new()
    {
        [(0, 0)] = "U",
        [(1, 0)] = "I",
        [(2, 0)] = "O",
        [(0, 1)] = "J",
        [(1, 1)] = "K",
        [(2, 1)] = "L",
        [(0, 2)] = "M",
        [(1, 2)] = ",",
        [(2, 2)] = ".",
    };

    // Callback invoked when a command resolves
    private readonly Action<GridCommand> _onCommand;

    private int _tapCount;
    private int _pendingCol = -1;
    private int _pendingRow = -1;
    private long _lastTapTime;
    private Timer? _tapTimer;

    internal GridInputHandler(Action<GridCommand> onCommand)
    {
        _onCommand = onCommand;
    }

    /// <summary>
    /// Processes a key press. Returns <c>true</c> if the key is a recognized navigation key
    /// (caller should swallow it); <c>false</c> otherwise.
    /// </summary>
    internal bool HandleKey(int virtualKey)
    {
        // H key for back navigation — fire immediately, no tap sequencing
        if (virtualKey == VK_H)
        {
            _onCommand(new BackCommand());
            return true;
        }

        if (!s_keyMap.TryGetValue(virtualKey, out var cell)) return false;
        RecordTap(cell.Col, cell.Row);
        return true;
    }

    /// <summary>Clears any pending tap sequence without executing it.</summary>
    internal void Reset()
    {
        _tapTimer?.Dispose();
        _tapTimer = null;
        ClearPending();
        _lastTapTime = 0;
    }

    public void Dispose() => Reset();

    private void RecordTap(int col, int row)
    {
        long now = Environment.TickCount64;

        if (IsNewSequence(col, row, now))
        {
            FlushPending();
            StartNewSequence(col, row, now);
        }
        else
        {
            _tapCount++;
            _lastTapTime = now;
        }

        if (_tapCount >= 3)
            CommitTripleTap();
        else
            ScheduleOrResetFlushTimer();
    }

    /// <summary>
    /// Returns true when the incoming tap belongs to a different sequence than the one in progress.
    /// A new sequence starts when the key changes, the tap window expires, or nothing is pending.
    /// </summary>
    private bool IsNewSequence(int col, int row, long now) =>
        _tapCount == 0
        || col != _pendingCol
        || row != _pendingRow
        || (now - _lastTapTime) > TapWindowMs;

    private void StartNewSequence(int col, int row, long now)
    {
        _pendingCol = col;
        _pendingRow = row;
        _tapCount = 1;
        _lastTapTime = now;
    }

    /// <summary>Immediately emits the reset/jump command and clears the pending sequence.</summary>
    private void CommitTripleTap()
    {
        _tapTimer?.Dispose();
        _tapTimer = null;
        Emit(_pendingCol, _pendingRow, _tapCount);
        ClearPending();
    }

    private void ScheduleOrResetFlushTimer()
    {
        if (_tapTimer == null)
            _tapTimer = new Timer(OnTimerElapsed, null, TapWindowMs, Timeout.Infinite);
        else
            _tapTimer.Change(TapWindowMs, Timeout.Infinite);
    }

    /// <summary>Emits any pending tap sequence and clears state. Called before starting a new sequence.</summary>
    private void FlushPending()
    {
        _tapTimer?.Dispose();
        _tapTimer = null;

        if (_tapCount > 0)
            Emit(_pendingCol, _pendingRow, _tapCount);

        ClearPending();
    }

    /// Fires on a thread-pool thread. A tap arriving at exactly the timeout boundary
    /// may race with RecordTap(), but the outcome is benign: one tap executes normally
    /// and the next tap starts a fresh sequence.
    private void OnTimerElapsed(object? _)
    {
        int taps = _tapCount;
        int col = _pendingCol;
        int row = _pendingRow;

        if (taps <= 0) return;

        ClearPending();
        _tapTimer?.Dispose();
        _tapTimer = null;

        Emit(col, row, taps);
    }

    private void ClearPending()
    {
        _tapCount = 0;
        _pendingCol = -1;
        _pendingRow = -1;
    }

    private void Emit(int col, int row, int taps)
    {
        GridCommand cmd = taps >= 3
            ? new ResetCommand(col, row)
            : new DrillCommand(col, row);
        _onCommand(cmd);
    }
}
