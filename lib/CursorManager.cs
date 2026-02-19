using System;
using System.Runtime.InteropServices;

namespace keyboardmouse.lib;

public class CursorManager
{
    // Imports the SetSystemCursor function
    [DllImport("user32.dll")]
    public static extern bool SetSystemCursor(IntPtr hcur, uint id);

    // Imports CopyIcon to ensure we don't destroy the original handle immediately
    [DllImport("user32.dll")]
    public static extern IntPtr CopyIcon(IntPtr hIcon);

    // Imports LoadCursor to get a handle to a standard cursor
    [DllImport("user32.dll")]
    public static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfo")]
    public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr LoadCursorFromFile(string path);


    // Cursor ID for the standard arrow
    public const uint OCR_NORMAL = 32512;
    public const uint SPI_SETCURSORS = 0x0057;


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
