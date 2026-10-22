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

        Assert.Equal(2, Assert.Single(_sent).Count);
        var saved = new BirthdayStore(_dir.Path).Load().Data.People;
        Assert.Equal(D(2026, 10, 22), saved.Single(p => p.Name == "Sam").LastBirthdayNotice);
        Assert.Equal(D(2026, 10, 24), saved.Single(p => p.Name == "Alex").LastHeadsUp);
    }

    [Fact]
    public void Nothing_due_sends_nothing()
    {
        Create(P(1, 1)).CheckNow();
        Assert.Empty(_sent);
    }

    [Fact]
    public void Heads_up_then_the_birthday_itself_on_the_day()
    {
        var service = Create(P(24, 10));
        service.CheckNow();
        _clock.Now = new DateTimeOffset(2026, 10, 24, 9, 0, 0, TimeSpan.Zero);
        service.CheckNow();

        Assert.Equal(2, _sent.Count);
        Assert.Equal(ReminderKind.HeadsUp, Assert.Single(_sent[0]).Kind);
        Assert.Equal(ReminderKind.Birthday, Assert.Single(_sent[1]).Kind);
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
}
