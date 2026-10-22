namespace BirthdayBuddy.Core;

/// <summary>Runs <c>action</c> once, <c>delay</c> after the last <see cref="Trigger"/>, on <c>context</c>.</summary>
public sealed class Debouncer(TimeSpan delay, Action action, TimeProvider time, SynchronizationContext context) : IDisposable
{
    ITimer? _timer;
    int _generation;

    public bool Pending { get; private set; }

    public void Trigger()
    {
        _timer?.Dispose();
        Pending = true;
        int generation = ++_generation;
        _timer = time.CreateTimer(_ => context.Post(_ => RunIfCurrent(generation), null), null, delay, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Runs a pending action now, e.g. before the window hides.</summary>
    public void Flush()
    {
        if (Pending) RunIfCurrent(_generation);
    }

    // A timer that fired just before being replaced must not run a stale action.
    void RunIfCurrent(int generation)
    {
        if (!Pending || generation != _generation) return;
        Pending = false;
        _timer?.Dispose();
        _timer = null;
        action();
    }

    public void Dispose() => _timer?.Dispose();
}
