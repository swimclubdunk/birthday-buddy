using System.Globalization;
using BirthdayBuddy.Core;

namespace BirthdayBuddy.Ui;

/// <summary>The Birthday Buddy window. Closing hides it to the tray.</summary>
public sealed class MainWindow : Form
{
    readonly Library _library;
    readonly Func<bool, bool> _setStartWithWindows;
    readonly Func<DateOnly> _today;
    readonly Func<bool>? _revealDataFile;

    readonly Label _subtitle = Widgets.Label(role: Role.Subtle);
    readonly Button _add = Widgets.Button("+ Add", Role.PrimaryButton);
    readonly PeopleList _list = new() { Dock = DockStyle.Fill };
    readonly TableLayoutPanel _empty = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1 };
    readonly PersonEditor _editor;
    readonly NumericUpDown _headsUp = new() { Minimum = 0, Maximum = Settings.MaxHeadsUpDays, Width = 52, Font = Theme.Body, BorderStyle = BorderStyle.FixedSingle, TextAlign = HorizontalAlignment.Center, Margin = new Padding(0, 2, 0, 0) };
    readonly CheckBox _startup = new() { Text = "Start with Windows", AutoSize = true, Font = Theme.Body, Margin = new Padding(0, 8, 0, 0) };
    readonly Label _note = Widgets.Label(role: Role.Error, font: Theme.Small);
    readonly Label _showDataFile = Widgets.Label("Show data file", Role.Accent, Theme.Small);
    readonly Button _emptyAdd = Widgets.Button("Add your first birthday", Role.PrimaryButton);
    readonly TextBox _search = new() { PlaceholderText = "Search names", Font = Theme.Body, BorderStyle = BorderStyle.FixedSingle, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 12, 0, 0) };
    readonly Label _noMatch = new() { Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Font = Theme.Body, Tag = Role.Subtle, UseMnemonic = false, AutoEllipsis = true };
    readonly NumericUpDown _idle = new() { Minimum = 0, Maximum = Settings.MaxIdleMinutes, Width = 52, Font = Theme.Body, BorderStyle = BorderStyle.FixedSingle, TextAlign = HorizontalAlignment.Center, Margin = new Padding(0, 2, 0, 0) };
    readonly TrackBar _volume = new() { Minimum = 0, Maximum = 10, SmallChange = 1, LargeChange = 1, TickFrequency = 1, TickStyle = TickStyle.None, AutoSize = false, Width = 150, Height = 26, Margin = new Padding(0, 2, 0, 0) };
    readonly Label _volumeValue = Widgets.Label();
    readonly Label _soundName = new() { AutoSize = false, AutoEllipsis = true, Width = 190, Height = 22, Font = Theme.Body, UseMnemonic = false, Margin = new Padding(0, 6, 0, 0) };
    readonly Button _chooseSound = Widgets.Button("Choose…");
    readonly Button _defaultSound = Widgets.Button("Default");
    readonly Button _testSound = Widgets.Button("Test");
    readonly Fanfare? _fanfare;
    readonly Debouncer _headsUpCommit;
    readonly Debouncer _idleCommit;
    readonly Debouncer _volumeCommit;
    bool _syncing;

    /// <param name="setStartWithWindows">Applies the startup setting; returns false if it couldn't.</param>
    /// <param name="time">Clock for the spinners' and slider's settle delay (tests pass a fake).</param>
    /// <param name="fanfare">Plays and imports the celebration sound; without it, Choose… and Test are disabled.</param>
    /// <param name="revealDataFile">Shows birthdays.json in Explorer; returns false if it couldn't. Null hides the link.</param>
    public MainWindow(Library library, Func<bool, bool> setStartWithWindows, Func<DateOnly> today, TimeProvider? time = null, Fanfare? fanfare = null, Func<bool>? revealDataFile = null)
    {
        _revealDataFile = revealDataFile;
        _library = library;
        _fanfare = fanfare;
        _setStartWithWindows = setStartWithWindows;
        _today = today;
        _editor = new PersonEditor(today) { Dock = DockStyle.Fill, Visible = false };
        // Commit the spinner once it settles, so stepping 3 → 30 is one save and one reminder check.
        var context = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();
        _headsUpCommit = new Debouncer(TimeSpan.FromMilliseconds(800), CommitHeadsUp, time ?? TimeProvider.System, context);
        _idleCommit = new Debouncer(TimeSpan.FromMilliseconds(800), CommitIdle, time ?? TimeProvider.System, context);
        _volumeCommit = new Debouncer(TimeSpan.FromMilliseconds(800), CommitVolume, time ?? TimeProvider.System, context);

        Text = "Birthday Buddy";
        Icon = AppIcon.Load(SystemInformation.IconSize);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        ClientSize = new Size(420, 670);
        MinimumSize = new Size(360, 570);
        StartPosition = FormStartPosition.CenterScreen;
        Font = Theme.Body;

        // Header: title + subtitle on the left, Add on the right, search underneath.
        var title = Widgets.Label("Birthday Buddy", font: Theme.Title);
        title.Margin = new Padding(0);
        _subtitle.AutoSize = false;
        _subtitle.AutoEllipsis = true;
        _subtitle.Height = 22;
        _subtitle.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _subtitle.Margin = new Padding(1, 2, 8, 0);
        _add.Anchor = AnchorStyles.Right;
        var header = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 3, Padding = new Padding(20, 18, 20, 12) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(title, 0, 0);
        header.Controls.Add(_subtitle, 0, 1);
        header.Controls.Add(_add, 1, 0);
        header.SetRowSpan(_add, 2);
        header.Controls.Add(_search, 0, 2);
        header.SetColumnSpan(_search, 2);

        // Empty state.
        var emptyTitle = Widgets.Label("No birthdays yet", font: Theme.Heading);
        var emptyHint = Widgets.Label("Add the people you care about and\nBirthday Buddy will remind you.", Role.Subtle);
        emptyHint.TextAlign = ContentAlignment.MiddleCenter;
        emptyHint.Margin = new Padding(0, 6, 0, 16);
        foreach (Control c in new Control[] { emptyTitle, emptyHint, _emptyAdd }) c.Anchor = AnchorStyles.None;
        var emptyStack = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Anchor = AnchorStyles.None };
        emptyStack.Controls.AddRange([emptyTitle, emptyHint, _emptyAdd]);
        _empty.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _empty.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _empty.Controls.Add(emptyStack, 0, 0);

        // Footer: settings.
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

        _showDataFile.Margin = new Padding(0, 8, 0, 0);
        _showDataFile.Cursor = Cursors.Hand;
        _showDataFile.Visible = revealDataFile is not null;
        _note.Margin = new Padding(0, 4, 0, 0);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(20, 12, 20, 14) };
        footer.Controls.AddRange([remind, idle, volume, sound, soundButtons, _startup, _showDataFile, _note]);
        var divider = new Panel { Dock = DockStyle.Bottom, Height = 1, Tag = Role.Border };

        var body = new Panel { Dock = DockStyle.Fill };
        body.Controls.AddRange([_list, _empty, _noMatch, _editor]);

        // Docking runs in reverse add order: header and footer claim their edges, body fills the rest.
        Controls.Add(body);
        Controls.Add(divider);
        Controls.Add(footer);
        Controls.Add(header);

        _add.Click += (_, _) => ShowEditor(null);
        _emptyAdd.Click += (_, _) => ShowEditor(null);
        _list.PersonActivated += (_, person) => ShowEditor(person);
        _editor.Saved += (_, person) => { _library.Upsert(person); CloseEditor(); };
        _editor.Deleted += (_, id) => { _library.Remove(id); CloseEditor(); };
        _editor.Cancelled += (_, _) => CloseEditor();
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
        _search.TextChanged += (_, _) => RefreshContent();
        _library.Changed += OnLibraryChanged;
    }

    public void ShowAndActivate()
    {
        if (!Visible) Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    void ShowEditor(Person? person)
    {
        _list.Visible = false;
        _empty.Visible = false;
        _noMatch.Visible = false;
        _add.Visible = false;
        _search.Visible = false;
        _editor.Visible = true;
        if (person is null) _editor.BeginAdd();
        else _editor.BeginEdit(person);
    }

    /// <summary>Back to the list, with focus on it rather than on the next tab stop (the heads-up spinner).</summary>
    void CloseEditor()
    {
        _editor.Visible = false;
        RefreshContent();
        if (_list.Visible) _list.Focus();
        else if (_search.Visible) _search.Focus();
        else _emptyAdd.Focus();
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

    void RefreshContent()
    {
        var today = _today();
        var people = BirthdayCalendar.Upcoming(_library.Data.People, today);
        if (people.Count == 0 && _search.Text.Length > 0)
        {
            _search.Text = ""; // re-enters via TextChanged with a clean filter
            return;
        }
        var shown = people.Where(p => PeopleFilter.Matches(p, _search.Text)).ToList();
        _list.SetPeople(shown, today);
        _subtitle.Text = Wording.Subtitle(people, today); // about everyone, not just the search results

        if (!_editor.Visible)
        {
            _list.Visible = shown.Count > 0;
            _empty.Visible = people.Count == 0;
            _noMatch.Visible = people.Count > 0 && shown.Count == 0;
            _noMatch.Text = Wording.NoMatch(_search.Text);
            _search.Visible = people.Count > 0;
        }
        _add.Visible = !_editor.Visible && people.Count > 0; // the empty state has its own button

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

    void OnLibraryChanged(object? sender, EventArgs e)
    {
        if (Visible) RefreshContent();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (!Visible)
        {
            _headsUpCommit.Flush();
            _idleCommit.Flush();
            _volumeCommit.Flush();
            return;
        }
        bool dark = Theme.IsDarkMode();
        Widgets.ApplyPalette(this, dark ? Theme.Dark : Theme.Light);
        Theme.ApplyTitleBar(this, dark);
        RefreshContent();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            _editor.Visible = false;
            Hide();
        }
        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && !_editor.Visible)
        {
            if (_search.Text.Length > 0) _search.Clear(); // first Esc clears the search, the next one hides
            else Hide();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _headsUpCommit.Flush();
            _headsUpCommit.Dispose();
            _idleCommit.Flush();
            _idleCommit.Dispose();
            _volumeCommit.Flush();
            _volumeCommit.Dispose();
            _library.Changed -= OnLibraryChanged;
        }
        base.Dispose(disposing);
    }
}
