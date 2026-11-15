using BirthdayBuddy.Core;

namespace BirthdayBuddy.Ui;

/// <summary>The Birthday Buddy window. Closing hides it to the tray.</summary>
public sealed class MainWindow : Form
{
    readonly Library _library;
    readonly Func<DateOnly> _today;

    readonly Label _subtitle = Widgets.Label(role: Role.Subtle);
    readonly Button _add = Widgets.Button("+ Add", Role.PrimaryButton);
    readonly Button _gear = new()
    {
        Text = "⚙", // ⚙
        AccessibleName = "Settings",
        Tag = Role.SecondaryButton,
        AutoSize = false,
        Width = 34,
        Anchor = AnchorStyles.Top | AnchorStyles.Bottom, // as tall as + Add
        FlatStyle = FlatStyle.Flat,
        Font = new Font("Segoe UI Symbol", 11f),
        Padding = new Padding(0),
        Margin = new Padding(6, 0, 0, 0),
        Cursor = Cursors.Hand,
        UseVisualStyleBackColor = false,
    };
    readonly ToolTip _tips = new();
    readonly PeopleList _list = new() { Dock = DockStyle.Fill };
    readonly TableLayoutPanel _empty = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 1 };
    readonly PersonEditor _editor;
    readonly SettingsPanel _settings;
    readonly Button _emptyAdd = Widgets.Button("Add your first birthday", Role.PrimaryButton);
    readonly TextBox _search = new() { PlaceholderText = "Search names", Font = Theme.Body, BorderStyle = BorderStyle.FixedSingle, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 12, 0, 0) };
    readonly Label _noMatch = new() { Dock = DockStyle.Fill, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Font = Theme.Body, Tag = Role.Subtle, UseMnemonic = false, AutoEllipsis = true };

    /// <param name="setStartWithWindows">Applies the startup setting; returns false if it couldn't.</param>
    /// <param name="time">Clock for the spinners' and slider's settle delay (tests pass a fake).</param>
    /// <param name="fanfare">Plays and imports the celebration sound; without it, Choose… and Test are disabled.</param>
    /// <param name="revealDataFile">Shows birthdays.json in Explorer; returns false if it couldn't. Null hides the link.</param>
    public MainWindow(Library library, Func<bool, bool> setStartWithWindows, Func<DateOnly> today, TimeProvider? time = null, Fanfare? fanfare = null, Func<bool>? revealDataFile = null)
    {
        _library = library;
        _today = today;
        _editor = new PersonEditor(today) { Dock = DockStyle.Fill, Visible = false };
        _settings = new SettingsPanel(library, setStartWithWindows, time ?? TimeProvider.System, fanfare, revealDataFile) { Dock = DockStyle.Fill, Visible = false };

        Text = "Birthday Buddy";
        Icon = AppIcon.Load(SystemInformation.IconSize);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        ClientSize = new Size(420, 670);
        MinimumSize = new Size(360, 570);
        StartPosition = FormStartPosition.CenterScreen;
        Font = Theme.Body;

        // Header: title + subtitle on the left, Add and the gear on the right, search underneath.
        var title = Widgets.Label("Birthday Buddy", font: Theme.Title);
        title.Margin = new Padding(0);
        _subtitle.AutoSize = false;
        _subtitle.AutoEllipsis = true;
        _subtitle.Height = 22;
        _subtitle.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        _subtitle.Margin = new Padding(1, 2, 8, 0);
        _add.Margin = new Padding(0);
        _tips.SetToolTip(_gear, "Settings");
        var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Anchor = AnchorStyles.Right, Margin = new Padding(0) };
        actions.Controls.AddRange([_add, _gear]);
        var header = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 3, Padding = new Padding(20, 18, 20, 12) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(title, 0, 0);
        header.Controls.Add(_subtitle, 0, 1);
        header.Controls.Add(actions, 1, 0);
        header.SetRowSpan(actions, 2);
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

        var body = new Panel { Dock = DockStyle.Fill };
        body.Controls.AddRange([_list, _empty, _noMatch, _editor, _settings]);

        // Docking runs in reverse add order: the header claims the top, body fills the rest.
        Controls.Add(body);
        Controls.Add(header);

        _add.Click += (_, _) => ShowEditor(null);
        _emptyAdd.Click += (_, _) => ShowEditor(null);
        _gear.Click += (_, _) => ShowSettings();
        _list.PersonActivated += (_, person) => ShowEditor(person);
        _editor.Saved += (_, person) => { _library.Upsert(person); CloseEditor(); };
        _editor.Deleted += (_, id) => { _library.Remove(id); CloseEditor(); };
        _editor.Cancelled += (_, _) => CloseEditor();
        _settings.Closed += (_, _) => CloseSettings();
        _search.TextChanged += (_, _) => RefreshContent();
        _library.Changed += OnLibraryChanged;
    }

    public void ShowAndActivate()
    {
        if (!Visible) Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    /// <summary>The list, empty state or no-match note is showing, not the editor or the settings.</summary>
    bool OnList => !_editor.Visible && !_settings.Visible;

    void HideList()
    {
        _list.Visible = false;
        _empty.Visible = false;
        _noMatch.Visible = false;
        _add.Visible = false;
        _gear.Visible = false;
        _search.Visible = false;
    }

    void ShowEditor(Person? person)
    {
        HideList();
        _editor.Visible = true;
        if (person is null) _editor.BeginAdd();
        else _editor.BeginEdit(person);
    }

    void ShowSettings()
    {
        HideList();
        _settings.Sync();
        _settings.Visible = true;
    }

    /// <summary>Back to the list, with focus on it rather than on the next tab stop.</summary>
    void CloseEditor()
    {
        _editor.Visible = false;
        ReturnToList();
    }

    void CloseSettings()
    {
        _settings.Flush();
        _settings.Visible = false;
        ReturnToList();
    }

    void ReturnToList()
    {
        RefreshContent();
        if (_list.Visible) _list.Focus();
        else if (_search.Visible) _search.Focus();
        else _emptyAdd.Focus();
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

        if (OnList)
        {
            _list.Visible = shown.Count > 0;
            _empty.Visible = people.Count == 0;
            _noMatch.Visible = people.Count > 0 && shown.Count == 0;
            _noMatch.Text = Wording.NoMatch(_search.Text);
            _search.Visible = people.Count > 0;
        }
        _add.Visible = OnList && people.Count > 0; // the empty state has its own button
        _gear.Visible = OnList;
        _settings.Sync();
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
            _settings.Flush();
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
            _settings.Visible = false; // reopen on the list
            Hide();
        }
        base.OnFormClosing(e);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape && _settings.Visible)
        {
            CloseSettings();
            return true;
        }
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
            _settings.Flush(); // while the window still listens, so the save refreshes it
            _library.Changed -= OnLibraryChanged;
            _tips.Dispose();
        }
        base.Dispose(disposing);
    }
}
