using System;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace keyboardmouse;

/// <summary>
/// Installs a low-level keyboard hook (WH_KEYBOARD_LL) that intercepts key-down events.
/// Invoke <see cref="Install"/> to begin intercepting; the provided handler receives the
/// virtual-key code and returns <c>true</c> to swallow the key or <c>false</c> to pass it on.
/// Invoke <see cref="Uninstall"/> (or <see cref="Dispose"/>) to remove the hook.
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    // WH_KEYBOARD_LL is not emitted as a constant by CsWin32; defined manually.
    private const int WH_KEYBOARD_LL = 13;

    // WM_KEYDOWN / WM_SYSKEYDOWN are not emitted by CsWin32; defined manually.
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    // AOT constraint: unmanaged delegates cannot capture closures.
    // A static field routes the native callback to the active KeyboardHook instance.
    private static KeyboardHook? s_active;

    // Hold a reference to the delegate so the GC does not collect it while the hook is installed.
    private static readonly HOOKPROC s_proc = HookCallback;

    private UnhookWindowsHookExSafeHandle? _hookHandle;
    private Func<int, bool>? _handler;

    /// <summary>
    /// Installs the hook and starts routing key-down events to <paramref name="handler"/>.
    /// Calling this when the hook is already installed is a no-op.
    /// </summary>
    public void Install(Func<int, bool> handler)
    {
        if (_hookHandle is { IsInvalid: false }) return;

        _handler = handler;
        s_active = this;

        // For WH_KEYBOARD_LL hooks, hMod is unused (can be 0/null) when the hook is in the
        // current process. Pass default(HINSTANCE) to stay AOT-clean without GetModuleHandle.
        _hookHandle = PInvoke.SetWindowsHookEx((WINDOWS_HOOK_ID)WH_KEYBOARD_LL, s_proc, default, 0);
    }

    /// <summary>
    /// Removes the hook. Safe to call multiple times or when not installed.
    /// </summary>
    public void Uninstall()
    {
        if (_hookHandle == null || _hookHandle.IsInvalid) return;

        _hookHandle.Dispose(); // internally calls UnhookWindowsHookEx
        _hookHandle = null;
        _handler = null;

        if (s_active == this)
            s_active = null;
    }

    public void Dispose() => Uninstall();

    // The actual native callback. Must be static to be AOT-safe.
    private static unsafe LRESULT HookCallback(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode >= 0 &&
            ((uint)wParam.Value == WM_KEYDOWN || (uint)wParam.Value == WM_SYSKEYDOWN))
        {
            var kbd = (KBDLLHOOKSTRUCT*)lParam.Value;
            if (kbd != null && s_active?._handler?.Invoke((int)kbd->vkCode) == true)
                return (LRESULT)1; // swallow the key
        }

        return PInvoke.CallNextHookEx(default, nCode, wParam, lParam);
    }
}
