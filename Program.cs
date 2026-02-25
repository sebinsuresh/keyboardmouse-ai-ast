using keyboardmouse.display;
using keyboardmouse.input;
using keyboardmouse.navigation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace keyboardmouse;

static class Program
{
    const int HOTKEY_ID = 1;

    [STAThread]
    static void Main()
    {
        OverlayWindow.RegisterWindowClass();
        TrailOverlay.RegisterWindowClass();

        var overlay = new OverlayWindow();
        var trail = new TrailOverlay();

        try
        {
            overlay.Create();
            // Create and show the simple trail overlay used for one-shot white lines.
            trail.Create();
            trail.Show();

            using var navigator = new GridNavigator(overlay.Handle);

            overlay.OnTimer = navigator.TimerTick;
            navigator.OnBoundsChanged = overlay.UpdateBounds;

            using var input = new GridInputHandler(navigator.Execute);
            using var hook = KeyboardHook.Create();
            using var hotkey = new HotKeyListener(HOTKEY_ID, (uint)VIRTUAL_KEY.VK_F8);

            hotkey.RunMessageLoop(() =>
            {
                if (navigator.IsActive)
                {
                    navigator.Deactivate();
                    hook.Uninstall();
                    overlay.Hide();
                }
                else
                {
                    navigator.Activate();
                    hook.Install(
                        (vk, mods) => input.HandleKey(vk, mods),
                        keyUpHandler: (vk, mods) => input.HandleKeyUp(vk, mods));
                    overlay.Show();
                }
            });
        }
        finally
        {
            overlay.Destroy();
            trail.Dispose();
        }
    }
}
