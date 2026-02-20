using System.Runtime.InteropServices;
using keyboardmouse.lib;

namespace keyboardmouse.forms;

public class ToggleForm : Form
{
    private bool _isCustom = false;

    private const int HOTKEY_ID = 1;
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const int WM_HOTKEY = 0x0312;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    public ToggleForm()
    {
        Text = "Cursor Swap Overlay";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;

        // Span the current monitor where the cursor is located
        var screen = Screen.FromPoint(Cursor.Position);
        Bounds = screen.Bounds;

        // Make the window fully transparent
        BackColor = Color.Lime;
        TransparencyKey = Color.Lime;
        Opacity = 0;

        // Register hotkey after handle is created
        Load += (s, e) =>
        {
            // TODO: Make this configurable.
            // 'Ctrl+Alt+K' toggles the cursor
            RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT, (uint)Keys.K);
        };

        FormClosing += (s, e) =>
        {
            CursorStyleManager.RestoreDefaultCursors();
            UnregisterHotKey(Handle, HOTKEY_ID);
        };
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
        {
            ToggleCursor();
        }
        base.WndProc(ref m);
    }

    private void ToggleCursor()
    {
        if (!_isCustom)
        {
            IntPtr darkCursor = CursorStyleManager.LoadCursorFromFile(CursorStyleManager.dark_cursor_path);

            if (darkCursor != IntPtr.Zero)
            {
                CursorStyleManager.SetGlobalCursor(darkCursor);
            }
            else
            {
                MessageBox.Show("Could not load cursor file at " + CursorStyleManager.dark_cursor_path);
                return;
            }
        }
        else
        {
            CursorStyleManager.RestoreDefaultCursors();
        }
        _isCustom = !_isCustom;
    }
}
