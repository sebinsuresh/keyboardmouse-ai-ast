using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace keyboardmouse.input;

/// <summary>
/// Installs a low-level keyboard hook (WH_KEYBOARD_LL) that intercepts key-down events.
/// Invoke <see cref="Install"/> to begin intercepting; the provided handler receives the
/// virtual-key code and returns <c>true</c> to swallow the key or <c>false</c> to pass it on.
/// Invoke <see cref="Uninstall"/> (or <see cref="Dispose"/>) to remove the hook.
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    // There is some memory management complexity here due to unmanaged code being mixed with managed code.
    // The hook callback must be a static method to avoid GC issues, but it needs to reference instance state
    // to call the user-provided handler. To solve this, we use a singleton pattern where the static callback
    // references a single shared instance of the hook class, which in turn holds the user-provided handler.

    private static KeyboardHook? instance;
    private UnhookWindowsHookExSafeHandle? _keyboardHookHandle;
    private Func<int, ModifierKeys, bool>? _keyboardEventHandler;
    private Action<int, ModifierKeys>? _keyUpHandler;

    private KeyboardHook() { }

    public static KeyboardHook Create()
    {
        if (instance != null) return instance;

        instance = new KeyboardHook();
        return instance;
    }

    /// <summary>
    /// Installs the hook and starts routing key-down events to <paramref name="handler"/>.
    /// Optionally routes key-up events to <paramref name="keyUpHandler"/>.
    /// Calling this when the hook is already installed is a no-op.
    /// </summary>
    public void Install(Func<int, ModifierKeys, bool> handler, Action<int, ModifierKeys>? keyUpHandler = null)
    {
        if (_keyboardHookHandle is { IsInvalid: false }) return;

        _keyboardEventHandler = handler;
        _keyUpHandler = keyUpHandler;
        _keyboardHookHandle = PInvoke.SetWindowsHookEx(
            WINDOWS_HOOK_ID.WH_KEYBOARD_LL,
            HookCallback,
            hmod: default,
            dwThreadId: 0);
    }

    // This needs to be static to avoid GC issues with the unmanaged callback
    private static LRESULT HookCallback(int nCode, WPARAM wParam, LPARAM lParam)
    {
        uint msg = (uint)wParam.Value;
        bool isKeyDown = IsKeyDown(msg);
        bool isKeyUp = IsKeyUp(msg);

        if (nCode < 0 || instance == null) return PInvoke.CallNextHookEx(default, nCode, wParam, lParam);

        // Handle key-down: skip modifiers and call the key-down handler
        if (isKeyDown && instance._keyboardEventHandler != null)
        {
            unsafe
            {
                var kbd = (KBDLLHOOKSTRUCT*)lParam.Value;

                if (kbd != default && kbd->vkCode != 0)
                {
                    // Skip modifier keys themselves — they don't trigger commands
                    if ((int)kbd->vkCode is 0x10 or 0x11 or 0x12) // VK_SHIFT, VK_CONTROL, VK_MENU
                    {
                        return PInvoke.CallNextHookEx(default, nCode, wParam, lParam);
                    }

                    // Compute active modifiers
                    var modifiers = ModifierKeys.None;
                    if ((PInvoke.GetAsyncKeyState(0x10) & 0x8000) != 0)
                        modifiers |= ModifierKeys.Shift;
                    if ((PInvoke.GetAsyncKeyState(0x11) & 0x8000) != 0)
                        modifiers |= ModifierKeys.Control;
                    if ((PInvoke.GetAsyncKeyState(0x12) & 0x8000) != 0)
                        modifiers |= ModifierKeys.Alt;

                    if (instance._keyboardEventHandler.Invoke((int)kbd->vkCode, modifiers) == true)
                    {
                        return (LRESULT)1;
                    }
                }
            }
        }

        // Handle key-up: do NOT skip modifiers, call the key-up handler, but always pass on
        if (isKeyUp && instance._keyUpHandler != null)
        {
            unsafe
            {
                var kbd = (KBDLLHOOKSTRUCT*)lParam.Value;

                if (kbd != default && kbd->vkCode != 0)
                {
                    // Compute active modifiers
                    var modifiers = ModifierKeys.None;
                    if ((PInvoke.GetAsyncKeyState(0x10) & 0x8000) != 0)
                        modifiers |= ModifierKeys.Shift;
                    if ((PInvoke.GetAsyncKeyState(0x11) & 0x8000) != 0)
                        modifiers |= ModifierKeys.Control;
                    if ((PInvoke.GetAsyncKeyState(0x12) & 0x8000) != 0)
                        modifiers |= ModifierKeys.Alt;

                    instance._keyUpHandler.Invoke((int)kbd->vkCode, modifiers);
                    // Always pass on key-up events (never swallow)
                }
            }
        }

        return PInvoke.CallNextHookEx(default, nCode, wParam, lParam);
    }

    /// <summary>
    /// Removes the hook. Safe to call multiple times or when not installed.
    /// </summary>
    public void Uninstall()
    {
        if (_keyboardHookHandle == null || _keyboardHookHandle.IsInvalid) return;

        _keyboardHookHandle.Dispose(); // internally calls UnhookWindowsHookEx
        _keyboardHookHandle = null;
        _keyboardEventHandler = null;
        _keyUpHandler = null;
    }

    private static bool IsKeyDown(uint msg) => msg is PInvoke.WM_KEYDOWN or PInvoke.WM_SYSKEYDOWN;
    private static bool IsKeyUp(uint msg) => msg is PInvoke.WM_KEYUP or PInvoke.WM_SYSKEYUP;

    public void Dispose() => Uninstall();
}
