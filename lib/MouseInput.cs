using System;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace keyboardmouse;

internal static class MouseInput
{
    // INPUT size is fixed at compile time; cache it to avoid repeated reflection in Marshal.SizeOf.
    private static readonly int s_inputSize = Marshal.SizeOf<INPUT>();

    /// <summary>
    /// Moves the mouse to absolute screen coordinates via SendInput.
    /// Coordinates are normalized to the 0–65535 range relative to the virtual screen.
    /// </summary>
    internal static void MoveTo(int x, int y)
    {
        var vr = DisplayInfo.GetVirtualScreenRect();
        // Guard against a degenerate virtual screen (e.g., no monitor reported) to prevent division by zero.
        int vsWidth = Math.Max(1, vr.right - vr.left);
        int vsHeight = Math.Max(1, vr.bottom - vr.top);

        int normX = (int)Math.Round((double)(x - vr.left) * 65535.0 / vsWidth);
        int normY = (int)Math.Round((double)(y - vr.top) * 65535.0 / vsHeight);

        INPUT input = default;
        input.type = INPUT_TYPE.INPUT_MOUSE;
        input.Anonymous.mi.dx = normX;
        input.Anonymous.mi.dy = normY;
        input.Anonymous.mi.dwFlags =
            MOUSE_EVENT_FLAGS.MOUSEEVENTF_MOVE |
            MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE |
            MOUSE_EVENT_FLAGS.MOUSEEVENTF_VIRTUALDESK;

        PInvoke.SendInput(new ReadOnlySpan<INPUT>(ref input), s_inputSize);
    }
}
