namespace keyboardmouse.input;

/// <summary>
/// Translates Windows virtual key codes to semantic input actions.
/// Separates Windows API concerns from sequence detection logic.
/// </summary>
internal static class InputTranslator
{
    private const int VK_H = 0x48;      // Back
    private const int VK_U = 0x55;      // Top-left
    private const int VK_I = 0x49;      // Top-center
    private const int VK_O = 0x4F;      // Top-right
    private const int VK_J = 0x4A;      // Mid-left
    private const int VK_K = 0x4B;      // Center
    private const int VK_L = 0x4C;      // Mid-right
    private const int VK_M = 0x4D;      // Bot-left
    private const int VK_COMMA = 0xBC;  // Bot-center (,)
    private const int VK_PERIOD = 0xBE; // Bot-right (.)

    private static readonly Dictionary<int, InputAction> s_keyToActionMap = new()
    {
        [VK_H] = InputAction.Back,
        [VK_U] = InputAction.GridTopLeft,
        [VK_I] = InputAction.GridTopCenter,
        [VK_O] = InputAction.GridTopRight,
        [VK_J] = InputAction.GridMiddleLeft,
        [VK_K] = InputAction.GridCenter,
        [VK_L] = InputAction.GridMiddleRight,
        [VK_M] = InputAction.GridBottomLeft,
        [VK_COMMA] = InputAction.GridBottomCenter,
        [VK_PERIOD] = InputAction.GridBottomRight,
    };

    /// <summary>
    /// Attempts to translate a Windows virtual key code to a semantic input action.
    /// Returns null if the key is not a recognized navigation key.
    /// </summary>
    public static InputAction? TryTranslateKey(int virtualKey)
    {
        return s_keyToActionMap.TryGetValue(virtualKey, out var action) ? action : null;
    }
}
