using System.Globalization;
using BirthdayBuddy.Core;
using static BirthdayBuddy.Tests.TestData;

namespace BirthdayBuddy.Tests;

public class WordingTests
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly DateOnly Today = D(2026, 10, 22); // a Thursday

    static Reminder Bday(string name, int? age = null) => new(ReminderKind.Birthday, name, Today, 0, age);
    static Reminder Soon(string name, int days) => new(ReminderKind.HeadsUp, name, Today.AddDays(days), days, null);

    [Fact]
    public void Single_birthday_with_age()
    {
        var (title, body) = Wording.Notification([Bday("Sam", 34)], Inv);
        Assert.Equal("🎂 Birthday today", title);
        Assert.Equal("It's Sam's birthday today! Turning 34.", body);
    }

    [Fact]
    public void Single_birthday_without_age() =>
        Assert.Equal("It's Sam's birthday today!", Wording.Notification([Bday("Sam")], Inv).Body);

    [Fact]
    public void Single_heads_up()
    {
        var (title, body) = Wording.Notification([Soon("Sam", 2)], Inv);
        Assert.Equal("Upcoming birthday", title);
        Assert.Equal("Sam's birthday is in 2 days (Sat 24 Oct).", body);
    }

    [Fact]
    public void Heads_up_for_tomorrow() =>
        Assert.Equal("Sam's birthday is tomorrow (Fri 23 Oct).", Wording.Notification([Soon("Sam", 1)], Inv).Body);

    [Fact]
    public void Several_heads_ups_title() =>
        Assert.Equal("2 upcoming birthdays", Wording.Notification([Soon("A", 1), Soon("B", 2)], Inv).Title);

    [Fact]
    public void Mixed_reminders_are_one_notification_with_one_line_each()
    {
        var (title, body) = Wording.Notification([Bday("Sam"), Bday("Alex", 40), Soon("Jo", 3)], Inv);
        Assert.Equal("🎂 2 birthdays today", title);
        Assert.Equal(
            "It's Sam's birthday today!\nIt's Alex's birthday today! Turning 40.\nJo's birthday is in 3 days (Sun 25 Oct).",
            body);
    }

    // Review Focus 2
    [Fact]
    public void Many_reminders_stay_within_balloon_limits()
    {
        var reminders = Enumerable.Range(1, 10).Select(i => Soon(new string('x', 60), i)).ToList();
        var (title, body) = Wording.Notification(reminders, Inv);
        var lines = body.Split('\n');
        Assert.Equal(4, lines.Length);
        Assert.Equal("…and 7 more.", lines[^1]);
        Assert.True(body.Length <= Wording.MaxBody, $"body was {body.Length} chars");
        Assert.True(title.Length <= Wording.MaxTitle);
    }

    [Fact]
    public void Empty_reminder_list_is_rejected() =>
        Assert.Throws<ArgumentException>(() => Wording.Notification([], Inv));

    [Theory]
    [InlineData(0, "Today 🎉")]
    [InlineData(1, "Tomorrow")]
    [InlineData(12, "in 12 days")]
    public void Status(int days, string expected) => Assert.Equal(expected, Wording.Status(days));

    [Fact]
    public void Detail_with_and_without_age()
    {
        Assert.Equal("24 Oct · turns 34", Wording.Detail(D(2026, 10, 24), 34, Inv));
        Assert.Equal("24 Oct", Wording.Detail(D(2026, 10, 24), null, Inv));
    }

    [Fact]
    public void Upcoming_line() => Assert.Equal("Sam · in 3 days", Wording.UpcomingLine(P(25, 10), 3));

    [Fact]
    public void Subtitle_variants()
    {
        Assert.Equal("No birthdays yet", Wording.Subtitle([], Today));
        Assert.Equal("🎉 Today: Sam, Alex", Wording.Subtitle([P(22, 10), P(22, 10, name: "Alex"), P(1, 1, name: "Jo")], Today));
        Assert.Equal("Next up: Sam, in 2 days", Wording.Subtitle([P(24, 10)], Today));
        Assert.Equal("Next up: Sam, tomorrow", Wording.Subtitle([P(23, 10)], Today));
    }

    [Fact]
    public void Tooltip_variants()
    {
        Assert.Equal("Birthday Buddy", Wording.Tooltip([]));
        Assert.Equal("🎂 Sam's birthday today", Wording.Tooltip(["Sam"]));
        Assert.Equal("🎂 Birthdays today: Sam, Alex", Wording.Tooltip(["Sam", "Alex"]));
    }

    // Review Focus 3
    [Fact]
    public void Tooltip_never_exceeds_127_chars() =>
        Assert.True(Wording.Tooltip(Enumerable.Repeat(new string('x', 60), 5)).Length <= Wording.MaxTooltip);
}
