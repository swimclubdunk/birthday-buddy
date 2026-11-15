using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using BirthdayBuddy.Core;
using BirthdayBuddy.Ui;
using Microsoft.Extensions.Time.Testing;
using static BirthdayBuddy.Tests.TestData;

namespace BirthdayBuddy.Tests;

public class CelebrationPresenterTests
{
    static readonly DateOnly Today = D(2026, 11, 15);

    sealed record Context(CelebrationPresenter Presenter, Library Library, FakeTimeProvider Time, Rectangle WorkingArea, Func<int> Opened, CelebrationLoop Loop);

    static void Run(Action<Context> test, int workingHeight, params string[] names) => Sta.Run(() =>
    {
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        using var dir = new TempDirectory();
        var library = new Library(new BirthdayStore(dir.Path), new AppData { People = names.Select(n => P(15, 11, name: n)).ToList() });
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 15, 9, 0, 0, TimeSpan.Zero));
        time.SetLocalTimeZone(TimeZoneInfo.Utc);
        var bounds = new Rectangle(-32000, -32000, 1920, 1080);
        var workingArea = bounds with { Height = workingHeight };
        int opened = 0;

        using var loop = new CelebrationLoop(() => CelebrationPlanner.Due(library.Data.People, Today), new FakePresence(), () => 5, () => (int)VisibleFor.TotalSeconds,
            new FakeFanfare(), time, new InlineSynchronizationContext());
        using var presenter = new CelebrationPresenter(loop, library, () => Today, () => opened++, time, () => (bounds, workingArea, 1f));
        loop.Start();
        test(new Context(presenter, library, time, workingArea, () => opened, loop));
    });

    static readonly TimeSpan VisibleFor = TimeSpan.FromSeconds(4);

    static void Raise(Control control, string method) =>
        typeof(Control).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, [EventArgs.Empty]);

    static Button ButtonOf(CelebrationPopup popup) => popup.Controls.OfType<Button>().Single();

    static PopupPlan Plan(Context c, int people) =>
        PopupLayout.Plan(c.WorkingArea, PopupLayout.PopupSize(new Size(1920, 1080), 1f), 1f, people);

    static void FinishAll(IEnumerable<CelebrationPopup> popups)
    {
        foreach (var popup in popups.ToList()) popup.FinishAnimation();
    }

    Guid Id(Library library, string name) => library.Data.People.Single(p => p.Name == name).Id;

    [Fact]
    public void Stack_change_opens_one_popup_per_person_in_the_planned_slots() => Run(c =>
    {
        c.Time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(2, c.Presenter.Popups.Count);
        FinishAll(c.Presenter.Popups);
        Assert.Equal(Id(c.Library, "Alex"), c.Presenter.Popups[0].PersonId);
        Assert.Equal(Plan(c, 2).PersonSlots[0], c.Presenter.Popups[0].Bounds);
        Assert.Equal(Plan(c, 2).PersonSlots[1], c.Presenter.Popups[1].Bounds);
    }, 1040, "Sam", "Alex");

    [Fact]
    public void Acknowledge_saves_the_marker_and_closes_only_that_popup() => Run(c =>
    {
        c.Time.Advance(TimeSpan.FromSeconds(1));
        FinishAll(c.Presenter.Popups);
        c.Time.Advance(CelebrationPopup.ClickGuard);
        var alex = c.Presenter.Popups[0];

        CelebrationPopupTests.Click(ButtonOf(alex));

        Assert.Equal(Today, c.Library.Data.People.Single(p => p.Name == "Alex").LastBirthdayAck);
        Assert.Null(c.Library.Data.People.Single(p => p.Name == "Sam").LastBirthdayAck);
        var sam = Assert.Single(c.Presenter.Popups);
        Assert.Equal(Id(c.Library, "Sam"), sam.PersonId);
        sam.FinishAnimation();
        Assert.Equal(Plan(c, 1).PersonSlots[0], sam.Bounds);
    }, 1040, "Sam", "Alex");

    [Fact]
    public void Overflow_shows_a_more_popup_that_opens_the_main_window() => Run(c =>
    {
        c.Time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(2, c.Presenter.Popups.Count);
        Assert.NotNull(c.Presenter.Popups[0].PersonId);
        var more = c.Presenter.Popups[1];
        Assert.Null(more.PersonId);
        Assert.Equal("+3 more birthdays today", more.Text);

        CelebrationPopupTests.Click(more);
        Assert.Equal(1, c.Opened());
    }, 400, "Alex", "Bo", "Cleo", "Dan");

    [Fact]
    public void Retract_slides_every_popup_out() => Run(c =>
    {
        c.Time.Advance(TimeSpan.FromSeconds(1));
        var popups = c.Presenter.Popups.ToList();
        FinishAll(popups);
        c.Time.Advance(VisibleFor);
        Assert.Empty(c.Presenter.Popups);
        FinishAll(popups);
        Assert.All(popups, p => Assert.True(p.IsDisposed));
    }, 1040, "Sam", "Alex");

    [Fact]
    public void Pointer_over_any_popup_holds_the_stack() => Run(c =>
    {
        c.Time.Advance(TimeSpan.FromSeconds(1));
        FinishAll(c.Presenter.Popups);
        var first = c.Presenter.Popups[0];

        Raise(first, "OnMouseEnter");
        c.Time.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(2, c.Presenter.Popups.Count);

        Raise(first, "OnMouseLeave");
        c.Time.Advance(VisibleFor);
        Assert.Empty(c.Presenter.Popups);
    }, 1040, "Sam", "Alex");

    // Final review M1: an appearance that slides in no new person popup must still retract.
    [Fact]
    public void Stack_retracts_when_only_the_more_popup_is_new() => Run(c =>
    {
        c.Time.Advance(TimeSpan.FromSeconds(1));
        FinishAll(c.Presenter.Popups);
        c.Library.Upsert(P(15, 11, name: "Cleo"));
        c.Loop.Recheck();
        Assert.Null(c.Presenter.Popups[^1].PersonId); // "+2 more" is the only new popup
        FinishAll(c.Presenter.Popups);

        c.Time.Advance(VisibleFor);
        Assert.Empty(c.Presenter.Popups);
    }, 400, "Alex", "Bo");

    // Final review M2: a popup sliding out under the pointer must not leave the stack held for good.
    [Fact]
    public void Pointer_entering_a_leaving_popup_does_not_hold_later_appearances() => Run(c =>
    {
        c.Time.Advance(TimeSpan.FromSeconds(1));
        var first = c.Presenter.Popups.ToList();
        FinishAll(first);
        c.Time.Advance(VisibleFor); // retract starts: popups are sliding out
        Raise(first[0], "OnMouseEnter");
        FinishAll(first);

        c.Time.Advance(CelebrationLoop.RepeatEvery - VisibleFor);
        Assert.Equal(2, c.Presenter.Popups.Count);
        FinishAll(c.Presenter.Popups);
        c.Time.Advance(VisibleFor);
        Assert.Empty(c.Presenter.Popups);
    }, 1040, "Sam", "Alex");
}
