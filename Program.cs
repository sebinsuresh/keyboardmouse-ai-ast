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

        var navigator = new GridNavigator();
        var overlay = new OverlayWindow();

        try
        {
            overlay.Create();
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
                    hook.Install((vk, mods) => input.HandleKey(vk, mods));
                    overlay.Show();
                }
            });
        }
        finally
        {
            overlay.Destroy();
        }
    }
}
