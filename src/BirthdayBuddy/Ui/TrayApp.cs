using BirthdayBuddy.Core;

namespace BirthdayBuddy.Ui;

/// <summary>The app's lifetime: tray icon, menu, reminders, and the (lazily created) window.</summary>
public sealed class TrayApp : ApplicationContext
{
    readonly Library _library;
    readonly NotifyIcon _tray;
    readonly ContextMenuStrip _menu = new();
    readonly Font _boldMenuFont;
    readonly Notifier _notifier;
    readonly ReminderService _reminders;
    readonly StartupRegistration _startup = new();
    MainWindow? _window;

    static string ExePath => Environment.ProcessPath ?? Application.ExecutablePath;

    public TrayApp(bool startInTray, SingleInstance instance)
    {
        var store = new BirthdayStore(BirthdayStore.DefaultDirectory);
        var loaded = store.Load();
        _library = new Library(store, loaded.Data, readOnly: loaded.Unavailable);

        _boldMenuFont = new Font(_menu.Font, FontStyle.Bold);
        _menu.Opening += (_, e) =>
        {
            BuildMenu();
            e.Cancel = false;
        };

        _tray = new NotifyIcon
        {
            Icon = AppIcon.Load(SystemInformation.SmallIconSize),
            Text = "Birthday Buddy",
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) ShowWindow();
        };

        _notifier = new Notifier(_tray);
        _notifier.Clicked += (_, _) => ShowWindow();

        RefreshStartupEntry();

        _reminders = new ReminderService(_library, _notifier.Show, TimeProvider.System);
        _reminders.Checked += (_, _) => RefreshTooltip();
        _library.Changed += (_, _) => RefreshTooltip();
        instance.OnActivationRequested(ShowWindow, SynchronizationContext.Current!);
        BuildMenu();
        // Launched at login: give the tray area a moment, or the first toast can be dropped
        // while its "sent" marker is still saved.
        _reminders.Start(startInTray ? TimeSpan.FromSeconds(15) : TimeSpan.Zero);
        RefreshTooltip();

        if (loaded.RecoveredFrom is not null)
            _notifier.ShowMessage("Birthday Buddy",
                $"Your birthday list couldn't be read, so a fresh one was started. The old file was kept as {Path.GetFileName(loaded.RecoveredFrom)}.");
        else if (loaded.Unavailable)
            _notifier.ShowMessage("Birthday Buddy",
                "Your birthday list couldn't be opened right now. Nothing was changed; restart Birthday Buddy to try again.");

        if (!startInTray) ShowWindow();
    }

    /// <summary>Turns start-at-login on or off. Returns false (and logs) if the registry refused.</summary>
    public bool TrySetStartWithWindows(bool enabled)
    {
        try
        {
            if (enabled) _startup.Register(ExePath);
            else _startup.Unregister();
        }
        catch (Exception ex)
        {
            Log.Error("Could not change the startup entry.", ex);
            return false;
        }
        _library.UpdateSettings(_library.Data.Settings with { StartWithWindows = enabled });
        return true;
    }

    /// <summary>On every launch, keep the startup entry pointing at wherever this exe now lives.</summary>
    void RefreshStartupEntry()
    {
        if (!_library.Data.Settings.StartWithWindows) return;
        try
        {
            _startup.Register(ExePath);
        }
        catch (Exception ex)
        {
            Log.Error("Could not register the startup entry.", ex);
        }
    }

    void BuildMenu()
    {
        _menu.Items.Clear();
        _menu.Items.Add(new ToolStripMenuItem("Open Birthday Buddy", null, (_, _) => ShowWindow()) { Font = _boldMenuFont });
        _menu.Items.Add(new ToolStripSeparator());

        var today = _reminders.Today;
        var upcoming = BirthdayCalendar.Upcoming(_library.Data.People, today).Take(3).ToList();
        if (upcoming.Count == 0)
            _menu.Items.Add(new ToolStripMenuItem("No birthdays yet") { Enabled = false });
        foreach (var person in upcoming)
        {
            var text = Wording.UpcomingLine(person, BirthdayCalendar.DaysUntil(person, today)).Replace("&", "&&");
            _menu.Items.Add(new ToolStripMenuItem(text) { Enabled = false });
        }

        _menu.Items.Add(new ToolStripSeparator());
        var startup = new ToolStripMenuItem("Start with Windows") { Checked = _library.Data.Settings.StartWithWindows };
        startup.Click += (_, _) =>
        {
            if (!TrySetStartWithWindows(!startup.Checked))
                _notifier.ShowMessage("Birthday Buddy", "Couldn't change the startup setting. Details are in log.txt.");
        };
        _menu.Items.Add(startup);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitThread()));
    }

    void RefreshTooltip()
    {
        var today = _reminders.Today;
        var names = _library.Data.People.Where(p => BirthdayCalendar.DaysUntil(p, today) == 0).Select(p => p.Name);
        _tray.Text = Wording.Tooltip(names);
    }

    void ShowWindow()
    {
        if (_window is null || _window.IsDisposed)
            _window = new MainWindow(_library, TrySetStartWithWindows, () => _reminders.Today);
        _window.ShowAndActivate();
    }

    protected override void ExitThreadCore()
    {
        _reminders.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _window?.Dispose();
        _menu.Dispose();
        _boldMenuFont.Dispose();
        base.ExitThreadCore();
    }
}
