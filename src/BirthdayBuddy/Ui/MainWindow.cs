using BirthdayBuddy.Core;

namespace BirthdayBuddy.Ui;

/// <summary>The Birthday Buddy window. Closing hides it to the tray.</summary>
public sealed class MainWindow : Form
{
    readonly Library _library;
    readonly Func<bool, bool> _setStartWithWindows;
    readonly Func<DateOnly> _today;

    readonly Label _subtitle = Widgets.Label(role: Role.Subtle);
    readonly Button _add = Widgets.Button("+ Add", Role.PrimaryButton);
    readonly PeopleList _list = new() { Dock = DockStyle.Fill };
    readonly TableLayoutPanel _empty = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1 };
    readonly PersonEditor _editor;
    readonly NumericUpDown _headsUp = new() { Minimum = 0, Maximum = Settings.MaxHeadsUpDays, Width = 52, Font = Theme.Body, BorderStyle = BorderStyle.FixedSingle, TextAlign = HorizontalAlignment.Center, Margin = new Padding(0, 2, 0, 0) };
    readonly CheckBox _startup = new() { Text = "Start with Windows", AutoSize = true, Font = Theme.Body, Margin = new Padding(0, 8, 0, 0) };
    readonly Label _note = Widgets.Label(role: Role.Error, font: Theme.Small);
    readonly Button _emptyAdd = Widgets.Button("Add your first birthday", Role.PrimaryButton);
    readonly TextBox _search = new() { PlaceholderText = "Search names", Font = Theme.Body, BorderStyle = BorderStyle.FixedSingle, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 12, 0, 0) };
    readonly Label _noMatch = new() { Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Font = Theme.Body, Tag = Role.Subtle, UseMnemonic = false, AutoEllipsis = true };
    readonly Debouncer _headsUpCommit;
    bool _syncing;

    /// <param name="setStartWithWindows">Applies the startup setting; returns false if it couldn't.</param>
    /// <param name="time">Clock for the heads-up spinner's settle delay (tests pass a fake).</param>
    public MainWindow(Library library, Func<bool, bool> setStartWithWindows, Func<DateOnly> today, TimeProvider? time = null)
    {
        _library = library;
        _setStartWithWindows = setStartWithWindows;
        _today = today;
        _editor = new PersonEditor(today) { Dock = DockStyle.Fill, Visible = false };
        // Commit the spinner once it settles, so stepping 3 → 30 is one save and one reminder check.
        _headsUpCommit = new Debouncer(TimeSpan.FromMilliseconds(800), CommitHeadsUp, time ?? TimeProvider.System,
            SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext());

        Text = "Birthday Buddy";
        Icon = AppIcon.Load(SystemInformation.IconSize);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        ClientSize = new Size(420, 520);
        MinimumSize = new Size(360, 420);
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
        _note.Margin = new Padding(0, 4, 0, 0);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(20, 12, 20, 14) };
        footer.Controls.AddRange([remind, _startup, _note]);
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
        _startup.CheckedChanged += (_, _) => OnStartupToggled();
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
            _library.Changed -= OnLibraryChanged;
        }
        base.Dispose(disposing);
    }
}
