using System.Drawing;
using System.Windows.Forms;
using BirthdayBuddy.Core;
using BirthdayBuddy.Ui;
using Microsoft.Extensions.Time.Testing;
using static BirthdayBuddy.Tests.TestData;

namespace BirthdayBuddy.Tests;

// Final review I2: after the editor closes, keys/wheel must not silently change the heads-up setting.
public class MainWindowTests
{
    static IEnumerable<T> All<T>(Control root) where T : Control =>
        root.Controls.Cast<Control>().SelectMany(c => (c is T t ? [t] : Enumerable.Empty<T>()).Concat(All<T>(c)));

    static Button ButtonNamed(Control root, string text) => All<Button>(root).Single(b => b.Text == text);

    static void InWindow(Action<MainWindow, Library, FakeTimeProvider> test, params Person[] people) => Sta.Run(() =>
    {
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        using var dir = new TempDirectory();
        var library = new Library(new BirthdayStore(dir.Path), new AppData { People = people });
        var time = new FakeTimeProvider();
        using var window = new MainWindow(library, _ => true, () => D(2026, 10, 22), time)
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000),
            ShowInTaskbar = false,
        };
        window.Show();
        Application.DoEvents();
        test(window, library, time);
    });

    [Fact]
    public void Saving_a_person_does_not_leave_focus_on_the_heads_up_spinner() => InWindow((window, library, _) =>
    {
        ButtonNamed(window, "Add your first birthday").PerformClick();
        var editor = All<PersonEditor>(window).Single();
        All<TextBox>(editor).First().Text = "Sam";
        var combos = All<ComboBox>(editor).ToList();
        combos[0].SelectedIndex = 23;
        combos[1].SelectedIndex = 9;

        ButtonNamed(editor, "Save").PerformClick();
        Application.DoEvents();

        Assert.Single(library.Data.People);
        Assert.IsNotType<NumericUpDown>(window.ActiveControl);
    });

    [Fact]
    public void Stepping_the_spinner_commits_the_setting_once_when_it_settles() => InWindow((window, library, time) =>
    {
        int changes = 0;
        library.Changed += (_, _) => changes++;
        var spinner = All<NumericUpDown>(window).Single();

        for (int value = 4; value <= 9; value++) spinner.Value = value;
        Application.DoEvents();
        Assert.Equal(3, library.Data.Settings.HeadsUpDays);

        time.Advance(TimeSpan.FromSeconds(2));
        Application.DoEvents();
        Assert.Equal(9, library.Data.Settings.HeadsUpDays);
        Assert.Equal(1, changes);
    });

    static TextBox SearchBox(Control root) => All<TextBox>(root).Single(t => t.PlaceholderText == "Search names");

    [Fact]
    public void Typing_in_search_filters_the_list() => InWindow((window, _, _) =>
    {
        var list = All<PeopleList>(window).Single();
        Assert.Equal(3, list.Items.Count);

        SearchBox(window).Text = "sa";
        Application.DoEvents();

        Assert.Equal(new[] { "Sam", "Samira" }, list.Items.Cast<Person>().Select(p => p.Name).Order());
    }, P(24, 10, name: "Sam"), P(1, 1, name: "Alex"), P(5, 5, name: "Samira"));

    [Fact]
    public void Search_with_no_match_says_so() => InWindow((window, _, _) =>
    {
        SearchBox(window).Text = "zz";
        Application.DoEvents();

        Assert.False(All<PeopleList>(window).Single().Visible);
        Assert.Contains(All<Label>(window), l => l.Visible && l.Text == "No one matches “zz”");
    }, P(24, 10, name: "Sam"));

    [Fact]
    public void Search_is_hidden_while_the_library_is_empty() => InWindow((window, _, _) =>
        Assert.False(SearchBox(window).Visible));

    [Fact]
    public void Window_resizes_but_not_below_its_minimum() => InWindow((window, _, _) =>
    {
        Assert.Equal(FormBorderStyle.Sizable, window.FormBorderStyle);
        Assert.True(window.MaximizeBox);

        window.Size = new Size(100, 100);
        Assert.Equal(window.MinimumSize, window.Size);

        window.Size = window.MinimumSize + new Size(300, 300);
        Assert.Equal(window.MinimumSize + new Size(300, 300), window.Size);
    });
}
