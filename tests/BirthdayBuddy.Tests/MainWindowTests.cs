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

    static void InWindow(Action<MainWindow, Library, FakeTimeProvider> test) => Sta.Run(() =>
    {
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        using var dir = new TempDirectory();
        var library = new Library(new BirthdayStore(dir.Path), new AppData());
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
}
