namespace BirthdayBuddy.Core;

/// <summary>
/// When the celebration popups appear and retract: 1 s after start, then every 15 minutes until everyone due
/// is acknowledged. Waits while <see cref="IUserPresence"/> says so and shows the moment it clears.
/// Raises <see cref="StackChanged"/> with who should be on screen; drawing is the presenter's job.
/// </summary>
/// <param name="context">Where timer callbacks run (the UI thread in the app).</param>
public sealed class CelebrationLoop(
    Func<IReadOnlyList<Celebration>> due, IUserPresence presence, Func<int> idleMinutes,
    IFanfare fanfare, TimeProvider time, SynchronizationContext context) : IDisposable
{
    public static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan VisibleFor = TimeSpan.FromSeconds(4);
    public static readonly TimeSpan RepeatEvery = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan PauseRecheck = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan MaxFanfare = TimeSpan.FromSeconds(30);

    sealed class Slot { public ITimer? Timer; }

    readonly Slot _attempt = new(), _retract = new(), _stopFanfare = new();
    readonly HashSet<Guid> _announced = [];
    DateOnly _announcedOn;
    DateTimeOffset _nextAt = DateTimeOffset.MaxValue;
    bool _firstAttemptDone, _pointerInside, _disposed;

    /// <summary>Who should be on screen now; empty means retract all. Raised on <c>context</c>.</summary>
    public event EventHandler<IReadOnlyList<Celebration>>? StackChanged;

    public IReadOnlyList<Celebration> Visible { get; private set; } = [];

    DateOnly Today => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    public void Start()
    {
        _nextAt = time.GetUtcNow() + StartupDelay;
        Schedule(_attempt, StartupDelay, Attempt);
    }

    /// <summary>
    /// Call when people, the date or the clock may have changed. Shows at once for a newly due person or an
    /// overdue appearance (after sleep); otherwise only drops people who are no longer due.
    /// </summary>
    public void Recheck()
    {
        if (!_firstAttemptDone || _disposed) return; // the startup delay still applies
        Guard(() =>
        {
            ResetAnnouncedOnNewDay();
            var current = due();
            if (current.Any(c => !_announced.Contains(c.PersonId)) || time.GetUtcNow() >= _nextAt)
            {
                Attempt();
                return;
            }

            var stillDue = Visible.Where(v => current.Any(c => c.PersonId == v.PersonId)).ToList();
            if (stillDue.Count != Visible.Count) SetVisible(stillDue);
        });
    }

    public void SlideInFinished()
    {
        if (Visible.Count > 0 && !_pointerInside) Schedule(_retract, VisibleFor, Retract);
    }

    public void PointerEntered()
    {
        _pointerInside = true;
        Cancel(_retract);
    }

    public void PointerLeft()
    {
        _pointerInside = false;
        if (Visible.Count > 0) Schedule(_retract, VisibleFor, Retract);
    }

    /// <summary>The owner clicked "Okay I got it!". Save the marker before calling this.</summary>
    public void Acknowledge(Guid personId)
    {
        if (!Visible.Any(c => c.PersonId == personId)) return;
        SetVisible(Visible.Where(c => c.PersonId != personId).ToList());
        if (Visible.Count == 0) fanfare.Stop();
    }

    void Attempt()
    {
        _firstAttemptDone = true;
        ResetAnnouncedOnNewDay();
        var current = due();
        if (current.Count == 0)
        {
            Cancel(_attempt);
            _nextAt = DateTimeOffset.MaxValue;
            if (Visible.Count > 0) SetVisible([]);
            return;
        }

        if (presence.Check(idleMinutes()) == Presence.Wait)
        {
            _nextAt = time.GetUtcNow() + PauseRecheck;
            Schedule(_attempt, PauseRecheck, Attempt);
            return;
        }

        foreach (var c in current) _announced.Add(c.PersonId);
        Cancel(_retract);
        Visible = current;
        StackChanged?.Invoke(this, Visible);
        fanfare.Play();
        Schedule(_stopFanfare, MaxFanfare, fanfare.Stop);
        _nextAt = time.GetUtcNow() + RepeatEvery;
        Schedule(_attempt, RepeatEvery, Attempt);
    }

    void Retract()
    {
        if (Visible.Count > 0) SetVisible([]);
    }

    void SetVisible(IReadOnlyList<Celebration> visible)
    {
        Visible = visible;
        if (visible.Count == 0)
        {
            Cancel(_retract);
            _pointerInside = false;
        }
        StackChanged?.Invoke(this, Visible);
    }

    void ResetAnnouncedOnNewDay()
    {
        if (_announcedOn == Today) return;
        _announced.Clear();
        _announcedOn = Today;
    }

    void Schedule(Slot slot, TimeSpan delay, Action action)
    {
        slot.Timer?.Dispose();
        ITimer? timer = null;
        timer = time.CreateTimer(_ => context.Post(_ =>
        {
            // A timer replaced or cancelled after it fired must not run.
            if (_disposed || slot.Timer != timer) return;
            slot.Timer = null;
            timer!.Dispose();
            Guard(action);
        }, null), null, delay, Timeout.InfiniteTimeSpan);
        slot.Timer = timer;
    }

    static void Cancel(Slot slot)
    {
        slot.Timer?.Dispose();
        slot.Timer = null;
    }

    static void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Log.Error("Celebration loop failed.", ex);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        Cancel(_attempt);
        Cancel(_retract);
        Cancel(_stopFanfare);
    }
}
