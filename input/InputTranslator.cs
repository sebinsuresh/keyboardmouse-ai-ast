namespace keyboardmouse.input;

/// <summary>
/// Translates Windows virtual key codes and modifiers to grid commands.
/// Maps keys to drill commands (navigate grid), shift+keys to reset commands (jump to screen region),
/// and h to back command (exit to parent grid).
/// </summary>
internal static class InputTranslator
{
    private const int VK_SHIFT = 0x10;  // Shift modifier
    private const int VK_H = 0x48;      // Back
    private const int VK_P = 0x50;      // Move to next monitor
    private const int VK_U = 0x55;      // Top-left
    private const int VK_I = 0x49;      // Top-center
    private const int VK_O = 0x4F;      // Top-right
    private const int VK_J = 0x4A;      // Mid-left
    private const int VK_K = 0x4B;      // Center
    private const int VK_L = 0x4C;      // Mid-right
    private const int VK_M = 0x4D;      // Bot-left
    private const int VK_COMMA = 0xBC;  // Bot-center (,)
    private const int VK_PERIOD = 0xBE; // Bot-right (.)
    private const int VK_Y = 0x59;      // Left click
    private const int VK_N = 0x4E;      // Right click

    private static readonly Dictionary<int, (int col, int row)> s_keyToGridPosition = new()
    {
        [VK_U] = (0, 0),       // Top-left
        [VK_I] = (1, 0),       // Top-center
        [VK_O] = (2, 0),       // Top-right
        [VK_J] = (0, 1),       // Mid-left
        [VK_K] = (1, 1),       // Center
        [VK_L] = (2, 1),       // Mid-right
        [VK_M] = (0, 2),       // Bot-left
        [VK_COMMA] = (1, 2),   // Bot-center
        [VK_PERIOD] = (2, 2),  // Bot-right
    };

    /// <summary>
    /// Translates a virtual key code and modifiers to a grid command.
    /// H key → BackCommand (modifiers ignored)
    /// Grid key without shift → DrillCommand (navigate into grid section)
    /// Grid key with shift (center K) → ResetCommand (jump to screen region and reset)
    /// Grid key with shift (non-center) → ManualMoveCommand (continuous mouse movement)
    /// Returns null if the key is not recognized.
    /// </summary>
    public static GridCommand? TryGetCommand(int virtualKey, ModifierKeys modifiers)
    {
        if (virtualKey == VK_H)
        {
            return new BackCommand();
        }

        if (virtualKey == VK_Y)
        {
            return new LeftClickCommand();
        }

        if (virtualKey == VK_N)
        {
            return new RightClickCommand();
        }

        if (virtualKey == VK_P && modifiers.HasFlag(ModifierKeys.Shift))
        {
            return new MoveToNextMonitorCommand();
        }

        if (s_keyToGridPosition.TryGetValue(virtualKey, out var pos))
        {
            if (modifiers.HasFlag(ModifierKeys.Shift))
            {
                // Center (1,1) gets ResetCommand; all other directions get ManualMoveCommand
                return pos == (1, 1)
                    ? new ResetCommand(pos.col, pos.row)
                    : new ManualMoveCommand(pos.col, pos.row);
            }

            return new DrillCommand(pos.col, pos.row);
        }

        return null;
    }

    /// <summary>
    /// Translates a key-up event to a stop command.
    /// Shift key up OR any directional key up → StopManualMoveCommand (stop continuous movement)
    /// All other keys → null (no command)
    /// </summary>
    public static GridCommand? TryGetKeyUpCommand(int virtualKey)
    {
        // Stop on Shift key up
        if (virtualKey == VK_SHIFT)
        {
            return new StopManualMoveCommand();
        }

        // Stop on any directional key up
        if (s_keyToGridPosition.ContainsKey(virtualKey))
        {
            return new StopManualMoveCommand();
        }

        return null;
    }
}

