using keyboardmouse.lib;

namespace keyboardmouse.forms;

public class ToggleForm : Form
{
    private bool _isCustom = false;
    private Button _toggleBtn;

    public ToggleForm()
    {
        this.Text = "Cursor Swap";
        this.Size = new Size(250, 150);

        _toggleBtn = new Button()
        {
            Text = "Switch to Dark Cursor",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 12)
        };

        _toggleBtn.Click += (s, e) => ToggleCursor();
        this.Controls.Add(_toggleBtn);
    }

    private void ToggleCursor()
    {
        if (!_isCustom)
        {
            IntPtr darkCursor = CursorManager.LoadCursorFromFile(CursorManager.dark_cursor_path);

            if (darkCursor != IntPtr.Zero)
            {
                CursorManager.SetGlobalCursor(darkCursor);
                _toggleBtn.Text = "Restore Default";
            }
            else
            {
                MessageBox.Show("Could not load cursor file at " + CursorManager.dark_cursor_path);
                return;
            }
        }
        else
        {
            CursorManager.RestoreDefaultCursors();
            _toggleBtn.Text = "Switch to Dark Cursor";
        }
        _isCustom = !_isCustom;
    }
}
