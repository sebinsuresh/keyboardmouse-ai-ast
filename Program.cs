using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace keyboardmouse;

static class Program
{
    [STAThread]
    static void Main()
    {
        while (PInvoke.GetMessage(out MSG msg, HWND.Null, 0, 0))
        {
            PInvoke.TranslateMessage(in msg);
            PInvoke.DispatchMessage(in msg);
        }
    }
}
