namespace keyboardmouse.input;

/// <summary>
/// Translates key presses into resolved navigation commands and fires a callback.
///
/// Grid key layout (mirrors numpad positions):
///   u  i  o      top-left    top-center    top-right
///   j  k  l      mid-left    center        mid-right
///   m  ,  .      bot-left    bot-center    bot-right
///
/// Tap rules:
///   Single tap → drill down one level into the current region
///   Triple tap → reset history and jump to the corresponding monitor half
///
/// Back key:
///   h → navigate to the previous grid level (no tap sequencing)
/// </summary>
internal sealed class GridInputHandler : IDisposable
{
    private const int TapDisambiguationWindowMs = 300;
    private readonly SequenceDetector<InputAction, GridCommand> _sequenceDetector;
    private readonly Action<GridCommand> _onCommand;

    /// <summary>
    /// Public, extendable mapping from grid cell (col,row) to the display label shown in the overlay.
    /// </summary>
    public static readonly Dictionary<(int Col, int Row), string> CellLabels = new()
    {
        [(0, 0)] = "U",
        [(1, 0)] = "I",
        [(2, 0)] = "O",
        [(0, 1)] = "J",
        [(1, 1)] = "K",
        [(2, 1)] = "L",
        [(0, 2)] = "M",
        [(1, 2)] = ",",
        [(2, 2)] = ".",
    };

    internal GridInputHandler(Action<GridCommand> onCommand)
    {
        _onCommand = onCommand;

        var resolver = new GridSequenceResolver();
        _sequenceDetector = new(resolver, TapDisambiguationWindowMs);
        _sequenceDetector.SequenceCompleted += OnSequenceCompleted;
    }

    /// <summary>
    /// Handles a completed input sequence by emitting the resolved command.
    /// </summary>
    private void OnSequenceCompleted(object? sender, SequenceCompletedEventArgs<InputAction, GridCommand> e)
    {
        _onCommand(e.Action);
    }

    /// <summary>
    /// Processes a key press. Returns <c>true</c> if the key is a recognized navigation key
    /// (caller should swallow it); <c>false</c> otherwise.
    /// </summary>
    internal bool HandleKey(int virtualKey)
    {
        var action = InputTranslator.TryTranslateKey(virtualKey);
        if (action == null) return false;

        _sequenceDetector.RegisterInput(action.Value);
        return true;
    }

    /// <summary>Clears any pending tap sequence without executing it.</summary>
    internal void Reset() => _sequenceDetector.Reset();

    public void Dispose() => _sequenceDetector.Dispose();
}
