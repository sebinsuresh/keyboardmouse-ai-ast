using keyboardmouse.forms;
using keyboardmouse.lib;

namespace keyboardmouse;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        try
        {
            Application.Run(new ToggleForm());
        }
        finally
        {
            CursorManager.RestoreDefaultCursors();
        }
    }
}
