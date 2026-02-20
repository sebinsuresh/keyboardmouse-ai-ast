using System.Collections.Generic;

namespace keyboardmouse;

/// <summary>
/// Maps virtual-key codes to 3×3 grid cell coordinates.
///
/// Key layout (mirrors numpad positions, shifted left one column):
///   u  i  o      top-left    top-center    top-right
///   j  k  l      mid-left    center        mid-right
///   m  ,  .      bot-left    bot-center    bot-right
/// </summary>
internal static class GridInputHandler
{
    private static readonly Dictionary<int, (int Col, int Row)> s_keyMap = new()
    {
        [0x55] = (0, 0), // U  — top-left
        [0x49] = (1, 0), // I  — top-center
        [0x4F] = (2, 0), // O  — top-right
        [0x4A] = (0, 1), // J  — mid-left
        [0x4B] = (1, 1), // K  — center
        [0x4C] = (2, 1), // L  — mid-right
        [0x4D] = (0, 2), // M  — bot-left
        [0xBC] = (1, 2), // ,  — bot-center
        [0xBE] = (2, 2), // .  — bot-right
    };

    /// <summary>
    /// Returns <c>true</c> and populates <paramref name="cell"/> when
    /// <paramref name="vkCode"/> is a recognised navigation key;
    /// returns <c>false</c> otherwise.
    /// </summary>
    internal static bool TryGetCell(int vkCode, out (int Col, int Row) cell)
        => s_keyMap.TryGetValue(vkCode, out cell);
}
