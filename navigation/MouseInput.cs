using System;
using System.Runtime.InteropServices;
using keyboardmouse.display;
using keyboardmouse.input;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace keyboardmouse.navigation;

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
        var virtualScreen = DisplayInfo.GetVirtualScreenRect();
        // Guard against a degenerate virtual screen (e.g., no monitor reported) to prevent division by zero.
        int virtualScreenWidth = Math.Max(1, virtualScreen.right - virtualScreen.left);
        int virtualScreenHeight = Math.Max(1, virtualScreen.bottom - virtualScreen.top);

        int normalizedX = (int)Math.Round((double)(x - virtualScreen.left) * 65535.0 / virtualScreenWidth);
        int normalizedY = (int)Math.Round((double)(y - virtualScreen.top) * 65535.0 / virtualScreenHeight);

        INPUT input = default;
        input.type = INPUT_TYPE.INPUT_MOUSE;
        input.Anonymous.mi.dx = normalizedX;
        input.Anonymous.mi.dy = normalizedY;
        input.Anonymous.mi.dwFlags =
            MOUSE_EVENT_FLAGS.MOUSEEVENTF_MOVE |
            MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE |
            MOUSE_EVENT_FLAGS.MOUSEEVENTF_VIRTUALDESK;

        PInvoke.SendInput(new ReadOnlySpan<INPUT>(ref input), s_inputSize);
    }

    /// <summary>
    /// Sends a mouse button click (down + up) at the current cursor position.
    /// </summary>
    internal static void Click(MouseButton button)
    {
        Span<INPUT> inputs = stackalloc INPUT[2];

        bool isLeft = button == MouseButton.Left;
        inputs[0].type = INPUT_TYPE.INPUT_MOUSE;
        inputs[0].Anonymous.mi.dwFlags = isLeft ?
            MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN :
            MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTDOWN;
        inputs[1].type = INPUT_TYPE.INPUT_MOUSE;
        inputs[1].Anonymous.mi.dwFlags = isLeft ?
            MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP :
            MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTUP;

        PInvoke.SendInput(inputs, s_inputSize);
    }
}
