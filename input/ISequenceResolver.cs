namespace keyboardmouse.input;

/// <summary>
/// Strategy for resolving buffered input sequences into concrete actions.
/// </summary>
internal interface ISequenceResolver<TInput, TOutput>
{
    /// <summary>
    /// Determines the output action for a resolved input sequence.
    /// </summary>
    /// <param name="input">The input that was buffered.</param>
    /// <param name="tapCount">The number of times the same input was pressed (1, 2, 3+).</param>
    /// <returns>The resolved output action, or null if disambiguation is still needed.</returns>
    TOutput? Resolve(TInput input, int tapCount);

    /// <summary>
    /// Determines if this tap count should be flushed immediately without waiting for the disambiguation window.
    /// </summary>
    /// <param name="tapCount">The number of consecutive taps of the same input.</param>
    /// <returns>True if this sequence should be flushed immediately; false to continue buffering.</returns>
    bool ShouldFlushImmediately(TInput input, int tapCount);
}
