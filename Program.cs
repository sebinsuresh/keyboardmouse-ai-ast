using keyboardmouse.forms;
using keyboardmouse.lib;

namespace keyboardmouse;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        
        // The 'using' and 'try' blocks ensure that even if the code 
        // crashes, the finally block attempts to restore your cursor.
        try
        {
            Application.Run(new ToggleForm());
        }
        finally
        {
            // Safety net: Restore default cursors on exit or crash
            CursorManager.RestoreDefaultCursors();
        }
    }
}
