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

    /// <summary>A window with a <see cref="Fanfare"/> on a fake player, so Test and Choose… work.</summary>
    static void InWindowWithSound(Action<MainWindow, Library, FakeTimeProvider, FakePlayer> test, Settings? settings = null) => Sta.Run(() =>
    {
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        using var dir = new TempDirectory();
        var library = new Library(new BirthdayStore(dir.Path), new AppData { Settings = settings ?? new Settings() });
        var time = new FakeTimeProvider();
        var player = new FakePlayer();
        var fanfare = new Fanfare(player, dir.Path, () => library.Data.Settings);
        using var window = new MainWindow(library, _ => true, () => D(2026, 10, 22), time, fanfare)
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000),
            ShowInTaskbar = false,
        };
        window.Show();
        Application.DoEvents();
        test(window, library, time, player);
    });

    static NumericUpDown SpinnerAfter(Control root, string label) =>
        All<NumericUpDown>(root).Single(n => n.Parent!.Controls.OfType<Label>().Any(l => l.Text == label));

    static NumericUpDown IdleSpinner(Control root) => SpinnerAfter(root, "Pause when idle for");

    static TrackBar VolumeSlider(Control root) => All<TrackBar>(root).Single();

    static void Settle(FakeTimeProvider time, double milliseconds)
    {
        time.Advance(TimeSpan.FromMilliseconds(milliseconds));
        Application.DoEvents();
    }

    [Fact]
    public void Idle_spinner_commits_once_it_settles() => InWindowWithSound((window, library, time, _) =>
    {
        IdleSpinner(window).Value = 10;
        Settle(time, 799);
        Assert.Equal(5, library.Data.Settings.IdleMinutes);
        Settle(time, 1);
        Assert.Equal(10, library.Data.Settings.IdleMinutes);
    });

    [Fact]
    public void Idle_row_reads_Pause_when_idle_for_N_min() => InWindowWithSound((window, _, _, _) =>
    {
        var row = IdleSpinner(window).Parent!;
        var labels = row.Controls.OfType<Label>().Select(l => l.Text).ToList();
        Assert.Equal(new[] { "Pause when idle for", "min" }, labels);
        Assert.Equal((0, Settings.MaxIdleMinutes), (IdleSpinner(window).Minimum, IdleSpinner(window).Maximum));
    });

    static NumericUpDown PopupSpinner(Control root) => SpinnerAfter(root, "Show popup for");

    [Fact]
    public void Popup_row_reads_Show_popup_for_N_seconds_and_commits_once_it_settles() => InWindowWithSound((window, library, time, _) =>
    {
        var spinner = PopupSpinner(window);
        Assert.Equal(new[] { "Show popup for", "seconds" }, spinner.Parent!.Controls.OfType<Label>().Select(l => l.Text));
        Assert.Equal((Settings.MinPopupSeconds, Settings.MaxPopupSeconds, 10m), (spinner.Minimum, spinner.Maximum, spinner.Value));

        spinner.Value = 25;
        Settle(time, 799);
        Assert.Equal(10, library.Data.Settings.PopupSeconds);
        Settle(time, 1);
        Assert.Equal(25, library.Data.Settings.PopupSeconds);
    });

    [Fact]
    public void Volume_slider_commits_tenths() => InWindowWithSound((window, library, time, _) =>
    {
        var slider = VolumeSlider(window);
        Assert.Equal((0, 10, 10), (slider.Minimum, slider.Maximum, slider.Value));
        slider.Value = 7;
        Assert.Contains(All<Label>(window), l => l.Text == "0.7");
        Settle(time, 800);
        Assert.Equal(0.7, library.Data.Settings.FanfareVolume);
    });

    [Fact]
    public void Default_button_clears_the_custom_sound() => InWindowWithSound((window, library, _, _) =>
    {
        OpenSettings(window);
        Assert.Contains(All<Label>(window), l => l.Text == "x.mp3");
        ButtonNamed(window, "Default").PerformClick();
        Assert.Null(library.Data.Settings.FanfareSound);
        Assert.Contains(All<Label>(window), l => l.Text == "Default fanfare");
    }, new Settings { FanfareSound = "x.mp3" });

    [Fact]
    public void Test_plays_at_the_slider_volume_before_it_is_saved() => InWindowWithSound((window, _, _, player) =>
    {
        OpenSettings(window);
        VolumeSlider(window).Value = 3;
        ButtonNamed(window, "Test").PerformClick();
        Assert.Equal(0.3, Assert.Single(player.Played).Volume);
    });

    static Button Gear(Control root) => All<Button>(root).Single(b => b.AccessibleName == "Settings");

    static void OpenSettings(MainWindow window)
    {
        Gear(window).PerformClick();
        Application.DoEvents();
    }

    static bool PressEscape(MainWindow window)
    {
        var msg = new Message();
        object[] args = [msg, Keys.Escape];
        var handled = (bool)typeof(MainWindow).GetMethod("ProcessCmdKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(window, args)!;
        Application.DoEvents();
        return handled;
    }

    [Fact]
    public void Gear_sits_right_of_Add() => InWindow((window, _, _) =>
    {
        var add = ButtonNamed(window, "+ Add");
        var gear = Gear(window);
        Assert.True(gear.Visible);
        Assert.Same(add.Parent, gear.Parent);
        Assert.True(gear.Left >= add.Right, $"gear at {gear.Left}, Add ends at {add.Right}");
    }, P(24, 10, name: "Sam"));

    [Fact]
    public void Settings_are_not_shown_on_the_list() => InWindowWithSound((window, _, _, _) =>
    {
        Assert.False(IdleSpinner(window).Visible);
        Assert.False(VolumeSlider(window).Visible);
        Assert.False(All<CheckBox>(window).Single(c => c.Text == "Start with Windows").Visible);
    });

    [Fact]
    public void Gear_opens_the_settings_over_the_list() => InWindow((window, _, _) =>
    {
        OpenSettings(window);

        Assert.True(All<SettingsPanel>(window).Single().Visible);
        Assert.True(IdleSpinner(window).Visible);
        Assert.False(All<PeopleList>(window).Single().Visible);
        Assert.False(ButtonNamed(window, "+ Add").Visible);
        Assert.False(Gear(window).Visible);
        Assert.False(SearchBox(window).Visible);
    }, P(24, 10, name: "Sam"));

    [Fact]
    public void Gear_works_from_the_empty_state() => InWindow((window, _, _) =>
    {
        Assert.True(Gear(window).Visible);
        OpenSettings(window);
        Assert.True(All<SettingsPanel>(window).Single().Visible);
        Assert.False(ButtonNamed(window, "Add your first birthday").Visible);
    });

    static Button Back(Control root) => All<Button>(root).Single(b => b.AccessibleName == "Back");

    [Fact]
    public void Back_arrow_returns_to_the_list() => InWindow((window, _, _) =>
    {
        Assert.False(Back(window).Visible);
        OpenSettings(window);
        Back(window).PerformClick();
        Application.DoEvents();

        Assert.False(All<SettingsPanel>(window).Single().Visible);
        Assert.True(All<PeopleList>(window).Single().Visible);
        Assert.True(ButtonNamed(window, "+ Add").Visible);
        Assert.True(Gear(window).Visible);
        Assert.False(Back(window).Visible);
    }, P(24, 10, name: "Sam"));

    [Fact]
    public void Back_arrow_takes_the_gears_place_and_size() => InWindow((window, _, _) =>
    {
        var gear = Gear(window);
        var gearBounds = gear.RectangleToScreen(gear.ClientRectangle);
        var add = ButtonNamed(window, "+ Add");
        Assert.Equal(add.Height, gear.Height);
        Assert.Equal(gear.Width, gear.Height);

        OpenSettings(window);
        var back = Back(window);
        Assert.True(back.Visible);
        Assert.Equal(gearBounds, back.RectangleToScreen(back.ClientRectangle));
    }, P(24, 10, name: "Sam"));

    [Fact]
    public void Settings_have_no_Done_button() => InWindow((window, _, _) =>
    {
        OpenSettings(window);
        Assert.DoesNotContain(All<Button>(window), b => b.Text == "Done");
    });

    [Fact]
    public void Escape_leaves_the_settings_but_keeps_the_window_open() => InWindow((window, _, _) =>
    {
        OpenSettings(window);
        Assert.True(PressEscape(window));

        Assert.True(window.Visible);
        Assert.False(All<SettingsPanel>(window).Single().Visible);
        Assert.True(All<PeopleList>(window).Single().Visible);
    }, P(24, 10, name: "Sam"));

    [Fact]
    public void Reopening_the_window_shows_the_list() => InWindow((window, _, _) =>
    {
        OpenSettings(window);
        window.Close(); // hides to the tray
        window.ShowAndActivate();
        Application.DoEvents();

        Assert.False(All<SettingsPanel>(window).Single().Visible);
        Assert.True(All<PeopleList>(window).Single().Visible);
    }, P(24, 10, name: "Sam"));

    [Fact]
    public void Settings_fit_at_minimum_size() => InWindowWithSound((window, _, _, _) =>
    {
        OpenSettings(window);
        window.Size = window.MinimumSize;
        window.PerformLayout();
        Application.DoEvents();
        var client = window.RectangleToScreen(window.ClientRectangle);
        foreach (var control in new Control[] { IdleSpinner(window), VolumeSlider(window), ButtonNamed(window, "Choose…"), ButtonNamed(window, "Test"), All<CheckBox>(window).Single(), Back(window) })
            Assert.True(client.Contains(control.RectangleToScreen(control.ClientRectangle)), $"{control.GetType().Name} {control.Text} is cut off: {control.RectangleToScreen(control.ClientRectangle)} vs {client}");
    });

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
        var spinner = All<NumericUpDown>(window).Single(n => n.Maximum == Settings.MaxHeadsUpDays);

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

    static void Click(Control c) =>
        typeof(Control).GetMethod("OnClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(c, [EventArgs.Empty]);

    [Theory]
    [InlineData(true, "")]
    [InlineData(false, "Couldn't open the data file location. Details are in log.txt.")]
    public void Show_data_file_reveals_the_file_and_reports_failure(bool succeeds, string note) => Sta.Run(() =>
    {
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        using var dir = new TempDirectory();
        var library = new Library(new BirthdayStore(dir.Path), new AppData());
        int reveals = 0;
        using var window = new MainWindow(library, _ => true, () => D(2026, 10, 22), new FakeTimeProvider(),
            revealDataFile: () => { reveals++; return succeeds; })
        {
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000),
            ShowInTaskbar = false,
        };
        window.Show();
        Application.DoEvents();

        var link = All<Label>(window).Single(l => l.Text == "Show data file");
        Click(link);

        Assert.Equal(1, reveals);
        Assert.Contains(All<Label>(window), l => l.Tag is Role.Error && l.Text == note);
    });
}
