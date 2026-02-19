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
            var cursorPath = @"C:\Windows\Cursors\arrow_rm.cur";

            IntPtr darkCursor = CursorManager.LoadCursorFromFile(cursorPath);

            if (darkCursor != IntPtr.Zero)
            {
                CursorManager.SetGlobalCursor(darkCursor);
                _toggleBtn.Text = "Restore Default";
            }
            else
            {
                MessageBox.Show("Could not load cursor file at " + cursorPath);
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
