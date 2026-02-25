namespace keyboardmouse.input;

/// <summary>
/// Base type for grid navigation commands resolved from keyboard input.
/// </summary>
internal abstract record GridCommand;

/// <summary>
/// Drill down into a grid cell (single tap).
/// </summary>
internal sealed record DrillCommand(int Col, int Row) : GridCommand;

/// <summary>
/// Jump to either the monitor full / half region and reset the navigation history (triple tap).
/// </summary>
internal sealed record ResetCommand(int Col, int Row) : GridCommand;

/// <summary>
/// Navigate back to the previous grid level (H key, no tap sequencing).
/// </summary>
internal sealed record BackCommand : GridCommand;

/// <summary>
/// Start continuous mouse movement in a direction (Shift + directional key down).
/// </summary>
internal sealed record ManualMoveCommand(int Col, int Row) : GridCommand;

/// <summary>
/// Stop continuous mouse movement (Shift + directional key up, or Shift key up).
/// No-op if no manual move is currently active.
/// </summary>
internal sealed record StopManualMoveCommand : GridCommand;

/// <summary>
/// Move the mouse to the center of the next monitor and reset the grid (Shift + P).
/// No-op if there is only one monitor.
/// </summary>
internal sealed record MoveToNextMonitorCommand : GridCommand;
