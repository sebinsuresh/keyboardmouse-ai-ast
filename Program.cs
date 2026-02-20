using System;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace keyboardmouse;

static class Program
{
    const int HOTKEY_ID = 1;
    const uint VK_F8 = 0x77;

    [STAThread]
    static void Main()
    {
        using var listener = new HotKeyListener(HOTKEY_ID, VK_F8);
        var mover = new RandomMouseMover();
        listener.RunMessageLoop(() => mover.MoveRandom());
    }
}
