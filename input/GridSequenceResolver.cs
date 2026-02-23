namespace keyboardmouse.input;

/// <summary>
/// Resolves input action sequences into grid navigation commands.
/// Single/double taps produce DrillCommand, triple+ taps produce ResetCommand.
/// </summary>
internal sealed class GridSequenceResolver : ISequenceResolver<InputAction, GridCommand>
{
    private static readonly Dictionary<InputAction, (int col, int row)> s_actionToGridPosition = new()
    {
        { InputAction.GridTopLeft,     (0, 0) },
        { InputAction.GridTopCenter,   (1, 0) },
        { InputAction.GridTopRight,    (2, 0) },
        { InputAction.GridMiddleLeft,  (0, 1) },
        { InputAction.GridCenter,      (1, 1) },
        { InputAction.GridMiddleRight, (2, 1) },
        { InputAction.GridBottomLeft,  (0, 2) },
        { InputAction.GridBottomCenter,(1, 2) },
        { InputAction.GridBottomRight, (2, 2) },
    };

    public GridCommand? Resolve(InputAction input, int tapCount)
    {
        if (input == InputAction.Back) return new BackCommand();

        if (!s_actionToGridPosition.TryGetValue(input, out var tuple)) return null;

        var (col, row) = tuple;

        if (tapCount <= 2) return new DrillCommand(col, row);

        return new ResetCommand(col, row);
    }

    public bool ShouldFlushImmediately(InputAction input, int tapCount)
    {
        if (input == InputAction.Back) return true;

        return tapCount >= 3;
    }
}
