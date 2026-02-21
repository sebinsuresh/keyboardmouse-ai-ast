using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

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
    private Func<int, bool>? _keyboardEventHandler;

    private KeyboardHook() { }

    public static KeyboardHook Create()
    {
        if (instance != null) return instance;

        instance = new KeyboardHook();
        return instance;
    }

    /// <summary>
    /// Installs the hook and starts routing key-down events to <paramref name="handler"/>.
    /// Calling this when the hook is already installed is a no-op.
    /// </summary>
    public void Install(Func<int, bool> handler)
    {
        if (_keyboardHookHandle is { IsInvalid: false }) return;

        _keyboardEventHandler = handler;
        _keyboardHookHandle = PInvoke.SetWindowsHookEx(
            WINDOWS_HOOK_ID.WH_KEYBOARD_LL,
            HookCallback,
            hmod: default,
            dwThreadId: 0);
    }

    // This needs to be static to avoid GC issues with the unmanaged callback
    private static LRESULT HookCallback(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (
            instance?._keyboardEventHandler != null &&
            nCode >= 0 &&
            IsKeyDown((uint)wParam.Value))
        {
            // unsafe due to KBDLLHOOKSTRUCT pointer dereference
            unsafe
            {
                var kbd = (KBDLLHOOKSTRUCT*)lParam.Value;

                if (
                    kbd != default &&
                    kbd->vkCode != 0 &&
                    instance._keyboardEventHandler.Invoke((int)kbd->vkCode) == true)
                {
                    return (LRESULT)1;
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
    }

    private static bool IsKeyDown(uint msg) => msg is PInvoke.WM_KEYDOWN or PInvoke.WM_SYSKEYDOWN;

    public void Dispose() => Uninstall();
}
