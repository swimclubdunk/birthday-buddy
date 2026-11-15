using System.Globalization;
using BirthdayBuddy.Core;
using static BirthdayBuddy.Tests.TestData;

namespace BirthdayBuddy.Tests;

public class ReminderServiceTests : IDisposable
{
    readonly TempDirectory _dir = new();
    readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 10, 22, 9, 0, 0, TimeSpan.Zero));
    readonly List<IReadOnlyList<Reminder>> _sent = [];

    public void Dispose() => _dir.Dispose();

    ReminderService Create(params Person[] people) =>
        new(new Library(new BirthdayStore(_dir.Path), new AppData { People = people }), _sent.Add, _clock);

    [Fact]
    public void Today_follows_the_clock() => Assert.Equal(D(2026, 10, 22), Create().Today);

    [Fact]
    public void Check_notifies_once_and_persists_markers()
    {
        var service = Create(P(22, 10, name: "Sam"), P(24, 10, name: "Alex"));

        service.CheckNow();
        service.CheckNow();

        Assert.Equal("Alex", Assert.Single(Assert.Single(_sent)).Name);
        var saved = new BirthdayStore(_dir.Path).Load().Data.People;
        Assert.Equal(D(2026, 10, 24), saved.Single(p => p.Name == "Alex").LastHeadsUp);
    }

    [Fact]
    public void Nothing_due_sends_nothing()
    {
        Create(P(1, 1)).CheckNow();
        Assert.Empty(_sent);
    }

    [Fact]
    public void Heads_up_fires_but_the_day_itself_sends_no_toast()
    {
        var service = Create(P(24, 10));
        service.CheckNow();
        _clock.Now = new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);
        service.CheckNow();

        Assert.Equal(ReminderKind.HeadsUp, Assert.Single(Assert.Single(_sent)).Kind);
    }

    [Theory]
    [InlineData("2026-10-22T09:00:00", 3600)]   // capped at an hour
    [InlineData("2026-10-22T23:30:00", 1805)]   // midnight + 5 s
    [InlineData("2026-10-22T23:59:58", 7)]
    public void Next_check_is_midnight_plus_five_seconds_capped_at_an_hour(string now, double expectedSeconds)
    {
        var local = new DateTimeOffset(DateTime.Parse(now, CultureInfo.InvariantCulture), TimeSpan.Zero);
        Assert.Equal(expectedSeconds, ReminderService.NextCheckDelay(local).TotalSeconds);
    }

    [Fact]
    public void Start_checks_immediately_by_default() => Sta.Run(() =>
    {
        SynchronizationContext.SetSynchronizationContext(new System.Windows.Forms.WindowsFormsSynchronizationContext());
        using var service = Create(P(24, 10));
        service.Start();
        Assert.Single(_sent);
    });

    // Final review: at login the tray may not be ready, and a dropped toast would still burn today's marker.
    [Fact]
    public void Start_with_an_initial_delay_does_not_check_yet() => Sta.Run(() =>
    {
        SynchronizationContext.SetSynchronizationContext(new System.Windows.Forms.WindowsFormsSynchronizationContext());
        using var service = Create(P(24, 10));
        service.Start(TimeSpan.FromSeconds(15));
        Assert.Empty(_sent);
        Assert.Null(new BirthdayStore(_dir.Path).Load().Data.People.SingleOrDefault()?.LastHeadsUp);
    });
}
