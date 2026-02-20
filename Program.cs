namespace keyboardmouse;

static class Program
{
    const int HOTKEY_ID = 1;
    const uint VK_F8 = 0x77;

    [STAThread]
    static void Main()
    {
        var navigator = new GridNavigator();
        using var hook = new KeyboardHook();
        using var listener = new HotKeyListener(HOTKEY_ID, VK_F8);

        listener.RunMessageLoop(() =>
        {
            if (navigator.IsActive)
            {
                navigator.Deactivate();
                hook.Uninstall();
            }
            else
            {
                navigator.Activate();
                hook.Install(vk =>
                {
                    // Translate VK code to grid cell, then navigate.
                    if (!GridInputHandler.TryGetCell(vk, out var cell)) return false;
                    navigator.NavigateTo(cell.Col, cell.Row);
                    return true;
                });
            }
        });
    }
}
