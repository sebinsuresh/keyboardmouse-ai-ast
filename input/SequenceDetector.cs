namespace keyboardmouse.input;

/// <summary>
/// Event args for when a buffered input sequence is disambiguated and completed.
/// </summary>
internal sealed class SequenceCompletedEventArgs<TInput, TOutput> : EventArgs
{
    public required TInput Input { get; init; }
    public required int TapCount { get; init; }
    public required TOutput Action { get; init; }
}

/// <summary>
/// Detects and disambiguates input sequences by buffering ambiguous inputs until
/// a timer expires or a different input is pressed.
/// </summary>
internal sealed class SequenceDetector<TInput, TOutput> : IDisposable
    where TInput : notnull
    where TOutput : notnull
{
    private readonly ISequenceResolver<TInput, TOutput> _resolver;
    private readonly int _disambiguationWindowMs;
    private Timer? _timer;

    private TInput? _currentInput;
    private int _sequenceCount;
    private bool _hasPendingInput;

    public event EventHandler<SequenceCompletedEventArgs<TInput, TOutput>>? SequenceCompleted;

    public SequenceDetector(
        ISequenceResolver<TInput, TOutput> resolver,
        int disambiguationWindowMs = 300)
    {
        if (disambiguationWindowMs <= 0)
        {
            throw new ArgumentException("Must be positive", nameof(disambiguationWindowMs));
        }

        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _disambiguationWindowMs = disambiguationWindowMs;
    }

    /// <summary>
    /// Registers an input, either continuing the current sequence or flushing the previous one.
    /// </summary>
    public void RegisterInput(TInput input)
    {
        if (!_hasPendingInput || !_currentInput!.Equals(input))
        {
            FlushPending();
            _currentInput = input;
            _sequenceCount = 1;
            _hasPendingInput = true;
        }
        else
        {
            _sequenceCount++;
        }

        if (_resolver.ShouldFlushImmediately(input, _sequenceCount))
        {
            FlushPending();
        }
        else
        {
            RestartTimer();
        }
    }

    /// <summary>Cancels any pending sequence without emitting it.</summary>
    public void Reset()
    {
        _timer?.Dispose();
        _timer = null;
        _hasPendingInput = false;
        _currentInput = default;
        _sequenceCount = 0;
    }

    private void OnTimerElapsed(object? _) => FlushPending();

    private void FlushPending()
    {
        _timer?.Dispose();
        _timer = null;

        if (!_hasPendingInput) return;

        var input = _currentInput!;
        var count = _sequenceCount;
        _hasPendingInput = false;
        _currentInput = default;
        _sequenceCount = 0;

        var action = _resolver.Resolve(input, count);
        if (action != null)
        {
            SequenceCompleted?.Invoke(this, new()
            {
                Input = input,
                TapCount = count,
                Action = action,
            });
        }
    }

    private void RestartTimer()
    {
        if (_timer == null)
        {
            _timer = new Timer(OnTimerElapsed, null, _disambiguationWindowMs, Timeout.Infinite);
        }
        else
        {
            _timer.Change(_disambiguationWindowMs, Timeout.Infinite);
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _timer = null;
    }
}
