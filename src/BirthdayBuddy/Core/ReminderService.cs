using Microsoft.Win32;

namespace BirthdayBuddy.Core;

/// <summary>
/// Decides when to check for reminders: at start, just after local midnight (re-checked at least hourly),
/// on wake from sleep, on clock/time-zone changes, and whenever the library changes.
/// </summary>
public sealed class ReminderService(Library library, Action<IReadOnlyList<Reminder>> notify, TimeProvider time) : IDisposable
{
    static readonly TimeSpan MaxWait = TimeSpan.FromHours(1);

    System.Windows.Forms.Timer? _timer;
    SynchronizationContext? _ui;

    public DateOnly Today => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    /// <summary>Raised on the UI thread after each scheduled check.</summary>
    public event EventHandler? Checked;

    /// <summary>Call on the UI thread once the app is ready.</summary>
    public void Start()
    {
        _ui = SynchronizationContext.Current ?? throw new InvalidOperationException("Start must run on the UI thread.");
        _timer = new System.Windows.Forms.Timer();
        _timer.Tick += (_, _) => Run();
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.TimeChanged += OnTimeChanged;
        library.Changed += OnLibraryChanged;
        Run();
    }

    public IReadOnlyList<Reminder> CheckNow()
    {
        var plan = ReminderPlanner.Plan(library.Data.People, library.Data.Settings, Today);
        if (plan.Reminders.Count > 0)
        {
            library.RecordSent(plan.UpdatedPeople);
            notify(plan.Reminders);
        }
        return plan.Reminders;
    }

    public static TimeSpan NextCheckDelay(DateTimeOffset localNow)
    {
        var untilMidnight = localNow.Date.AddDays(1).AddSeconds(5) - localNow.DateTime;
        return untilMidnight < MaxWait ? untilMidnight : MaxWait;
    }

    void Run()
    {
        try
        {
            CheckNow();
        }
        catch (Exception ex)
        {
            Log.Error("Reminder check failed.", ex);
        }

        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Interval = Math.Max(1000, (int)NextCheckDelay(time.GetLocalNow()).TotalMilliseconds);
            _timer.Start();
        }

        Checked?.Invoke(this, EventArgs.Empty);
    }

    // SystemEvents fire on their own thread; hop back to the UI thread.
    void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) _ui?.Post(_ => Run(), null);
    }

    void OnTimeChanged(object? sender, EventArgs e) => _ui?.Post(_ =>
    {
        TimeZoneInfo.ClearCachedData();
        Run();
    }, null);

    void OnLibraryChanged(object? sender, EventArgs e) => Run();

    public void Dispose()
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.TimeChanged -= OnTimeChanged;
        library.Changed -= OnLibraryChanged;
        _timer?.Dispose();
    }
}
