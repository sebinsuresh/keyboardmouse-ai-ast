namespace keyboardmouse.input;

/// <summary>
/// Translates key presses and modifiers into navigation commands and fires a callback.
///
/// Grid key layout (mirrors numpad positions):
///   u  i  o      top-left    top-center    top-right
///   j  k  l      mid-left    center        mid-right
///   m  ,  .      bot-left    bot-center    bot-right
///
/// Navigation rules:
///   Grid key (unmodified) → drill down one level into the grid
///   Shift + grid key → reset history and jump to the corresponding screen region
///
/// Back key:
///   h → navigate to the previous grid level (modifiers ignored)
/// </summary>
internal sealed class GridInputHandler
{
    private readonly Action<GridCommand> _onCommand;
    private readonly HashSet<int> _pressedKeys = [];

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

    internal GridInputHandler(Action<GridCommand> onCommand)
    {
        _onCommand = onCommand;
    }

    /// <summary>
    /// Processes a key press with modifiers. Returns <c>true</c> if the key is a recognized navigation key
    /// (caller should swallow it); <c>false</c> otherwise.
    /// Click commands (Y/N) are guarded to prevent firing repeatedly on key hold.
    /// </summary>
    internal bool HandleKey(int virtualKey, ModifierKeys modifiers)
    {
        var command = InputTranslator.TryGetCommand(virtualKey, modifiers);
        if (command == null) return false;

        // Guard click commands: don't fire if the key is already pressed (Windows sends repeat WM_KEYDOWN)
        if (command is MouseClickCommand && _pressedKeys.Contains(virtualKey))
        {
            return true; // Swallow the repeat, don't dispatch
        }

        _pressedKeys.Add(virtualKey);
        _onCommand(command);
        return true;
    }

    /// <summary>Processes a key release, firing a stop command if applicable and clearing the pressed-key tracker.</summary>
    internal void HandleKeyUp(int virtualKey, ModifierKeys modifiers)
    {
        _pressedKeys.Remove(virtualKey);
        var command = InputTranslator.TryGetKeyUpCommand(virtualKey);
        if (command != null) _onCommand(command);
    }
}
