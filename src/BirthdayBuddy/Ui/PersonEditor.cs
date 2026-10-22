using System.Globalization;
using BirthdayBuddy.Core;

namespace BirthdayBuddy.Ui;

/// <summary>Inline add/edit form for one person. Errors show inline; deleting takes a second click to confirm.</summary>
public sealed class PersonEditor : UserControl
{
    readonly Func<DateOnly> _today;
    readonly Label _heading = Widgets.Label(font: Theme.Heading);
    readonly TextBox _name = new() { MaxLength = PersonRules.MaxNameLength, Font = Theme.Body, BorderStyle = BorderStyle.FixedSingle, Anchor = AnchorStyles.Left | AnchorStyles.Right };
    readonly ComboBox _day = new() { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, Font = Theme.Body, Width = 64, Margin = new Padding(0, 0, 6, 0) };
    readonly ComboBox _month = new() { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, Font = Theme.Body, Width = 130, Margin = new Padding(0, 0, 6, 0) };
    readonly TextBox _year = new() { PlaceholderText = "Year (optional)", MaxLength = 4, Font = Theme.Body, Width = 112, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(0) };
    readonly Label _error = Widgets.Label(role: Role.Error, font: Theme.Small);
    readonly Button _save = Widgets.Button("Save", Role.PrimaryButton);
    readonly Button _cancel = Widgets.Button("Cancel");
    readonly Button _delete = Widgets.Button("Delete", Role.DangerButton);
    Person? _editing;
    bool _confirmingDelete;

    public event EventHandler<Person>? Saved;
    public event EventHandler<Guid>? Deleted;
    public event EventHandler? Cancelled;

    public PersonEditor(Func<DateOnly> today)
    {
        _today = today;
        for (int d = 1; d <= 31; d++) _day.Items.Add(d);
        foreach (var month in CultureInfo.CurrentCulture.DateTimeFormat.MonthNames.Take(12)) _month.Items.Add(month);

        var date = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        date.Controls.AddRange([_day, _month, _year]);

        _save.Margin = new Padding(8, 0, 0, 0);
        _cancel.Margin = new Padding(0);
        _delete.Margin = new Padding(0);
        var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = false, FlowDirection = FlowDirection.RightToLeft, Anchor = AnchorStyles.Right, Margin = new Padding(0) };
        actions.Controls.AddRange([_save, _cancel]);

        var buttons = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 16, 0, 0) };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        buttons.Controls.Add(_delete, 0, 0);
        buttons.Controls.Add(actions, 1, 0);

        _heading.Margin = new Padding(0, 0, 0, 6);
        _error.Margin = new Padding(0, 10, 0, 0);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(20, 4, 20, 8) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(_heading);
        layout.Controls.Add(FieldLabel("Name"));
        layout.Controls.Add(_name);
        layout.Controls.Add(FieldLabel("Birthday"));
        layout.Controls.Add(date);
        layout.Controls.Add(_error);
        layout.Controls.Add(buttons);
        Controls.Add(layout);

        _save.Click += (_, _) => Save();
        _cancel.Click += (_, _) => Cancelled?.Invoke(this, EventArgs.Empty);
        _delete.Click += (_, _) => Delete();
    }

    public void BeginAdd() => Populate(null, "Add a birthday");

    public void BeginEdit(Person person) => Populate(person, "Edit birthday");

    void Populate(Person? person, string heading)
    {
        _editing = person;
        _heading.Text = heading;
        _name.Text = person?.Name ?? "";
        _day.SelectedIndex = person is null ? -1 : person.Day - 1;
        _month.SelectedIndex = person is null ? -1 : person.Month - 1;
        _year.Text = person?.Year?.ToString(CultureInfo.InvariantCulture) ?? "";
        _error.Text = "";
        _confirmingDelete = false;
        _delete.Text = "Delete";
        _delete.Visible = person is not null;
        _name.Focus();
    }

    void Save()
    {
        if (_day.SelectedIndex < 0 || _month.SelectedIndex < 0)
        {
            _error.Text = "Please choose a day and month.";
            return;
        }
        if (!PersonRules.TryParseYear(_year.Text, out var year))
        {
            _error.Text = "Year should be a number, like 1992.";
            return;
        }

        int day = _day.SelectedIndex + 1, month = _month.SelectedIndex + 1;
        var error = PersonRules.Validate(_name.Text, day, month, year, _today());
        if (error is not null)
        {
            _error.Text = error;
            return;
        }

        var person = _editing is null
            ? PersonRules.Create(_name.Text, day, month, year)
            : PersonRules.Edit(_editing, _name.Text, day, month, year);
        Saved?.Invoke(this, person);
    }

    void Delete()
    {
        if (_editing is null) return;
        if (!_confirmingDelete)
        {
            _confirmingDelete = true;
            _delete.Text = "Really delete?";
            return;
        }
        Deleted?.Invoke(this, _editing.Id);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        bool dropdownOpen = _day.DroppedDown || _month.DroppedDown;
        if (keyData == Keys.Enter && !dropdownOpen && ActiveControl is not Button)
        {
            Save();
            return true;
        }
        if (keyData == Keys.Escape && !dropdownOpen)
        {
            Cancelled?.Invoke(this, EventArgs.Empty);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    static Label FieldLabel(string text)
    {
        var label = Widgets.Label(text, Role.Subtle, Theme.Small);
        label.Margin = new Padding(0, 12, 0, 4);
        return label;
    }
}
