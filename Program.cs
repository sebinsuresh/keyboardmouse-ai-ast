namespace keyboardmouse;

static class Program
{
    const int HOTKEY_ID = 1;
    const uint VK_F8 = 0x77;

    [STAThread]
    static void Main()
    {
        var navigator = new GridNavigator();
        using var input = new GridInputHandler(navigator.Execute);
        using var hook = new KeyboardHook();
        using var hotkey = new HotKeyListener(HOTKEY_ID, VK_F8);

        hotkey.RunMessageLoop(() =>
        {
            if (navigator.IsActive)
            {
                navigator.Deactivate();
                input.Reset();
                hook.Uninstall();
            }
            else
            {
                navigator.Activate();
                hook.Install(input.HandleKey);
            }
        });
    }
}
