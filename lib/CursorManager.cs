using System;
using System.Runtime.InteropServices;

namespace keyboardmouse.lib;

public class CursorManager
{
    // Sets the system cursor for a specified cursor ID
    [DllImport("user32.dll")]
    public static extern bool SetSystemCursor(IntPtr hcur, uint id);

    // Duplicates a cursor or icon handle
    [DllImport("user32.dll")]
    public static extern IntPtr CopyIcon(IntPtr hIcon);

    // Performs a system-wide parameter operation (used here to reset cursors)
    [DllImport("user32.dll", EntryPoint = "SystemParametersInfo")]
    public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    // Loads a cursor from a file
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr LoadCursorFromFile(string path);


    // Cursor ID for the standard arrow
    public const uint OCR_NORMAL = 32512;
    public const uint SPI_SETCURSORS = 0x0057;

    public const string dark_cursor_path = @"C:\Windows\Cursors\arrow_rm.cur";


    public static void SetGlobalCursor(IntPtr newCursorHandle)
    {
        // We use CopyIcon because SetSystemCursor "consumes" the handle 
        // and manages its memory/destruction.
        IntPtr cursorCopy = CopyIcon(newCursorHandle);
        SetSystemCursor(cursorCopy, OCR_NORMAL);
    }

    public static void RestoreDefaultCursors()
    {
        SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
    }
}
