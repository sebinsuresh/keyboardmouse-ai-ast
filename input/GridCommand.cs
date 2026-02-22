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
