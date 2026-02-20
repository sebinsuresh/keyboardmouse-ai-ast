using System;
using System.Collections.Generic;
using System.Threading;

namespace keyboardmouse;

/// <summary>
/// Translates key presses into resolved navigation commands and fires a callback.
///
/// Key layout (mirrors numpad positions):
///   u  i  o      top-left    top-center    top-right
///   j  k  l      mid-left    center        mid-right
///   m  ,  .      bot-left    bot-center    bot-right
///
/// Tap rules:
///   Single tap → drill down one level into the current region
///   Triple tap → jump to the corresponding monitor half and reset the region
/// </summary>
internal sealed class GridInputHandler : IDisposable
{
    private const int TapWindowMs = 300;

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

    // Callback invoked when a tap sequence resolves: (col, row, tapCount)
    private readonly Action<int, int, int> _onCommand;

    private int _tapCount;
    private int _bufferedCol = -1;
    private int _bufferedRow = -1;
    private long _lastTapTime;
    private Timer? _tapTimer;

    internal GridInputHandler(Action<int, int, int> onCommand)
    {
        _onCommand = onCommand;
    }

    /// <summary>
    /// Processes a key press. Returns <c>true</c> if the key is a recognized navigation key
    /// (caller should swallow it); <c>false</c> otherwise.
    /// </summary>
    internal bool HandleKey(int vk)
    {
        if (!s_keyMap.TryGetValue(vk, out var cell)) return false;
        Buffer(cell.Col, cell.Row);
        return true;
    }

    /// <summary>Clears any pending tap sequence without executing it.</summary>
    internal void Reset()
    {
        _tapTimer?.Dispose();
        _tapTimer = null;
        _tapCount = 0;
        _bufferedCol = -1;
        _bufferedRow = -1;
        _lastTapTime = 0;
    }

    public void Dispose() => Reset();

    // -------------------------------------------------------------------------

    private void Buffer(int col, int row)
    {
        long now = Environment.TickCount64;
        bool sameKey = _tapCount > 0 && col == _bufferedCol && row == _bufferedRow;
        bool withinWindow = (now - _lastTapTime) <= TapWindowMs;

        if (_tapCount > 0 && (!sameKey || !withinWindow))
            Flush();

        if (_tapCount == 0 || !sameKey || !withinWindow)
        {
            _bufferedCol = col;
            _bufferedRow = row;
            _tapCount = 1;
        }
        else
        {
            _tapCount++;
        }

        _lastTapTime = now;

        if (_tapCount >= 3)
        {
            _tapTimer?.Dispose();
            _tapTimer = null;
            Emit(_bufferedCol, _bufferedRow, 3);
            _tapCount = 0;
            _bufferedCol = -1;
            _bufferedRow = -1;
        }
        else
        {
            if (_tapTimer == null)
                _tapTimer = new Timer(OnTimerElapsed, null, TapWindowMs, Timeout.Infinite);
            else
                _tapTimer.Change(TapWindowMs, Timeout.Infinite);
        }
    }

    private void Flush()
    {
        _tapTimer?.Dispose();
        _tapTimer = null;

        if (_tapCount > 0)
            Emit(_bufferedCol, _bufferedRow, _tapCount);

        _tapCount = 0;
        _bufferedCol = -1;
        _bufferedRow = -1;
    }

    // Fires on a thread-pool thread. A tap arriving at exactly the timeout boundary
    // may race with Buffer(), but the outcome is benign (one tap executes, next starts fresh).
    private void OnTimerElapsed(object? _)
    {
        int taps = _tapCount;
        int col = _bufferedCol;
        int row = _bufferedRow;

        if (taps <= 0) return;

        _tapCount = 0;
        _bufferedCol = -1;
        _bufferedRow = -1;
        _tapTimer?.Dispose();
        _tapTimer = null;

        Emit(col, row, taps);
    }

    private void Emit(int col, int row, int taps) => _onCommand(col, row, taps);
}
