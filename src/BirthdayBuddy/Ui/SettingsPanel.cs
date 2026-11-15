using System.Globalization;
using BirthdayBuddy.Core;

namespace BirthdayBuddy.Ui;

/// <summary>The settings view, shown over the list from the gear button. Changes save as they're made.</summary>
public sealed class SettingsPanel : UserControl
{
    readonly Library _library;
    readonly Func<bool, bool> _setStartWithWindows;
    readonly Func<bool>? _revealDataFile;
    readonly Fanfare? _fanfare;

    readonly NumericUpDown _headsUp = new() { Minimum = 0, Maximum = Settings.MaxHeadsUpDays, Width = 52, Font = Theme.Body, BorderStyle = BorderStyle.FixedSingle, TextAlign = HorizontalAlignment.Center, Margin = new Padding(0, 2, 0, 0) };
    readonly NumericUpDown _idle = new() { Minimum = 0, Maximum = Settings.MaxIdleMinutes, Width = 52, Font = Theme.Body, BorderStyle = BorderStyle.FixedSingle, TextAlign = HorizontalAlignment.Center, Margin = new Padding(0, 2, 0, 0) };
    readonly TrackBar _volume = new() { Minimum = 0, Maximum = 10, SmallChange = 1, LargeChange = 1, TickFrequency = 1, TickStyle = TickStyle.None, AutoSize = false, Width = 150, Height = 26, Margin = new Padding(0, 2, 0, 0) };
    readonly Label _volumeValue = Widgets.Label();
    readonly Label _soundName = new() { AutoSize = false, AutoEllipsis = true, Width = 190, Height = 22, Font = Theme.Body, UseMnemonic = false, Margin = new Padding(0, 6, 0, 0) };
    readonly Button _chooseSound = Widgets.Button("Choose…");
    readonly Button _defaultSound = Widgets.Button("Default");
    readonly Button _testSound = Widgets.Button("Test");
    readonly CheckBox _startup = new() { Text = "Start with Windows", AutoSize = true, Font = Theme.Body, Margin = new Padding(0, 10, 0, 0) };
    readonly Label _showDataFile = Widgets.Label("Show data file", Role.Accent, Theme.Small);
    readonly Label _note = Widgets.Label(role: Role.Error, font: Theme.Small);
    readonly Button _done = Widgets.Button("Done", Role.PrimaryButton);
    readonly Debouncer _headsUpCommit;
    readonly Debouncer _idleCommit;
    readonly Debouncer _volumeCommit;
    bool _syncing;

    /// <summary>Done was clicked: go back to the list.</summary>
    public event EventHandler? Closed;

    /// <param name="setStartWithWindows">Applies the startup setting; returns false if it couldn't.</param>
    /// <param name="time">Clock for the spinners' and slider's settle delay (tests pass a fake).</param>
    /// <param name="fanfare">Plays and imports the celebration sound; without it, Choose… and Test are disabled.</param>
    /// <param name="revealDataFile">Shows birthdays.json in Explorer; returns false if it couldn't. Null hides the link.</param>
    public SettingsPanel(Library library, Func<bool, bool> setStartWithWindows, TimeProvider time, Fanfare? fanfare, Func<bool>? revealDataFile)
    {
        _library = library;
        _setStartWithWindows = setStartWithWindows;
        _fanfare = fanfare;
        _revealDataFile = revealDataFile;
        // Commit the spinner once it settles, so stepping 3 → 30 is one save and one reminder check.
        var context = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _headsUpCommit = new Debouncer(TimeSpan.FromMilliseconds(800), CommitHeadsUp, time, context);
        _idleCommit = new Debouncer(TimeSpan.FromMilliseconds(800), CommitIdle, time, context);
        _volumeCommit = new Debouncer(TimeSpan.FromMilliseconds(800), CommitVolume, time, context);

        var heading = Widgets.Label("Settings", font: Theme.Heading);
        heading.Margin = new Padding(0, 0, 0, 10);

        var remindLabel = Widgets.Label("Remind me");
        remindLabel.Margin = new Padding(0, 5, 6, 0);
        var daysLabel = Widgets.Label("days before");
        daysLabel.Margin = new Padding(6, 5, 0, 0);
        var remind = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        remind.Controls.AddRange([remindLabel, _headsUp, daysLabel]);

        var idleLabel = Widgets.Label("Pause when idle for");
        idleLabel.Margin = new Padding(0, 5, 6, 0);
        var minLabel = Widgets.Label("min");
        minLabel.Margin = new Padding(6, 5, 0, 0);
        var idle = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 6, 0, 0) };
        idle.Controls.AddRange([idleLabel, _idle, minLabel]);

        var volumeLabel = Widgets.Label("Fanfare volume");
        volumeLabel.Margin = new Padding(0, 5, 6, 0);
        _volumeValue.Margin = new Padding(4, 5, 0, 0);
        var volume = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 6, 0, 0) };
        volume.Controls.AddRange([volumeLabel, _volume, _volumeValue]);

        // Two rows so the buttons fit at the minimum width: "Fanfare sound  <name>", then the buttons.
        var soundLabel = Widgets.Label("Fanfare sound");
        soundLabel.Margin = new Padding(0, 6, 6, 0);
        var sound = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 4, 0, 0) };
        sound.Controls.AddRange([soundLabel, _soundName]);
        foreach (var b in new[] { _chooseSound, _defaultSound, _testSound }) b.Margin = new Padding(0, 0, 6, 0);
        var soundButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 4, 0, 0) };
        soundButtons.Controls.AddRange([_chooseSound, _defaultSound, _testSound]);
        _chooseSound.Enabled = _testSound.Enabled = fanfare is not null;

        _showDataFile.Margin = new Padding(0, 10, 0, 0);
        _showDataFile.Cursor = Cursors.Hand;
        _showDataFile.Visible = revealDataFile is not null;
        _note.Margin = new Padding(0, 4, 0, 0);
        _done.Margin = new Padding(0, 16, 0, 0);

        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(20, 4, 20, 8) };
        layout.Controls.AddRange([heading, remind, idle, volume, sound, soundButtons, _startup, _showDataFile, _note, _done]);
        Controls.Add(layout);

        _headsUp.ValueChanged += (_, _) =>
        {
            if (!_syncing) _headsUpCommit.Trigger();
        };
        _idle.ValueChanged += (_, _) =>
        {
            if (!_syncing) _idleCommit.Trigger();
        };
        _volume.ValueChanged += (_, _) =>
        {
            _volumeValue.Text = VolumeText(_volume.Value / 10.0);
            if (!_syncing) _volumeCommit.Trigger();
        };
        _chooseSound.Click += (_, _) => ChooseSound();
        _defaultSound.Click += (_, _) => _library.UpdateSettings(_library.Data.Settings with { FanfareSound = null });
        _testSound.Click += (_, _) => _fanfare?.Play(_volume.Value / 10.0);
        _startup.CheckedChanged += (_, _) => OnStartupToggled();
        _showDataFile.Click += (_, _) => OnShowDataFile();
        _done.Click += (_, _) => Closed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Shows the saved settings, except values the user is still choosing.</summary>
    public void Sync()
    {
        _syncing = true;
        if (!_headsUpCommit.Pending) // don't snap back a value the user is still choosing
            _headsUp.Value = Math.Clamp(_library.Data.Settings.HeadsUpDays, 0, Settings.MaxHeadsUpDays);
        if (!_idleCommit.Pending)
            _idle.Value = Math.Clamp(_library.Data.Settings.IdleMinutes, 0, Settings.MaxIdleMinutes);
        if (!_volumeCommit.Pending)
            _volume.Value = (int)Math.Round(Settings.SnapVolume(_library.Data.Settings.FanfareVolume) * 10);
        _volumeValue.Text = VolumeText(_volume.Value / 10.0);
        _soundName.Text = _library.Data.Settings.FanfareSound ?? Wording.DefaultFanfare;
        _defaultSound.Enabled = _library.Data.Settings.FanfareSound is not null;
        _startup.Checked = _library.Data.Settings.StartWithWindows;
        _syncing = false;
    }

    /// <summary>Saves any value still settling.</summary>
    public void Flush()
    {
        _headsUpCommit.Flush();
        _idleCommit.Flush();
        _volumeCommit.Flush();
    }

    void CommitHeadsUp()
    {
        int days = (int)_headsUp.Value;
        if (days != _library.Data.Settings.HeadsUpDays)
            _library.UpdateSettings(_library.Data.Settings with { HeadsUpDays = days });
    }

    void CommitIdle()
    {
        int minutes = (int)_idle.Value;
        if (minutes != _library.Data.Settings.IdleMinutes)
            _library.UpdateSettings(_library.Data.Settings with { IdleMinutes = minutes });
    }

    void CommitVolume()
    {
        double volume = Settings.SnapVolume(_volume.Value / 10.0);
        if (volume != _library.Data.Settings.FanfareVolume)
            _library.UpdateSettings(_library.Data.Settings with { FanfareVolume = volume });
    }

    static string VolumeText(double volume) => volume.ToString("0.0", CultureInfo.InvariantCulture);

    void ChooseSound()
    {
        if (_fanfare is null) return;
        using var dialog = new OpenFileDialog { Filter = "Sound files (*.wav;*.mp3)|*.wav;*.mp3", Title = "Choose a fanfare" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var name = _fanfare.Import(dialog.FileName);
            _note.Text = "";
            _library.UpdateSettings(_library.Data.Settings with { FanfareSound = name });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error("Could not copy the chosen sound.", ex);
            _note.Text = Wording.SoundImportFailed;
        }
    }

    void OnStartupToggled()
    {
        if (_syncing) return;
        bool wanted = _startup.Checked;
        if (_setStartWithWindows(wanted))
        {
            _note.Text = "";
            return;
        }
        _syncing = true;
        _startup.Checked = !wanted;
        _syncing = false;
        _note.Text = "Couldn't change the startup setting. Details are in log.txt.";
    }

    void OnShowDataFile()
    {
        if (_revealDataFile is null) return;
        _note.Text = _revealDataFile() ? "" : "Couldn't open the data file location. Details are in log.txt.";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Flush();
            _headsUpCommit.Dispose();
            _idleCommit.Dispose();
            _volumeCommit.Dispose();
        }
        base.Dispose(disposing);
    }
}
