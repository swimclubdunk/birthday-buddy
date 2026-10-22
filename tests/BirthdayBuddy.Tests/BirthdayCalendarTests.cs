using BirthdayBuddy.Core;
using static BirthdayBuddy.Tests.TestData;

namespace BirthdayBuddy.Tests;

public class BirthdayCalendarTests
{
    [Fact]
    public void Next_occurrence_is_today_when_birthday_is_today() =>
        Assert.Equal(D(2026, 10, 22), BirthdayCalendar.NextOccurrence(P(22, 10), D(2026, 10, 22)));

    [Fact]
    public void Next_occurrence_later_this_year() =>
        Assert.Equal(D(2026, 10, 24), BirthdayCalendar.NextOccurrence(P(24, 10), D(2026, 10, 22)));

    [Fact]
    public void Next_occurrence_wraps_to_next_year() =>
        Assert.Equal(D(2027, 1, 1), BirthdayCalendar.NextOccurrence(P(1, 1), D(2026, 10, 22)));

    [Fact]
    public void Leap_day_birthday_falls_on_28_Feb_in_non_leap_years() =>
        Assert.Equal(D(2027, 2, 28), BirthdayCalendar.NextOccurrence(P(29, 2), D(2026, 10, 22)));

    [Fact]
    public void Leap_day_birthday_is_29_Feb_in_leap_years() =>
        Assert.Equal(D(2028, 2, 29), BirthdayCalendar.NextOccurrence(P(29, 2), D(2027, 3, 1)));

    [Fact]
    public void Leap_day_birthday_is_today_on_28_Feb_of_a_non_leap_year() =>
        Assert.Equal(0, BirthdayCalendar.DaysUntil(P(29, 2), D(2027, 2, 28)));

    [Fact]
    public void Days_until_counts_forward() =>
        Assert.Equal(2, BirthdayCalendar.DaysUntil(P(24, 10), D(2026, 10, 22)));

    [Fact]
    public void Days_until_crosses_new_year() =>
        Assert.Equal(3, BirthdayCalendar.DaysUntil(P(2, 1), D(2026, 12, 30)));

    [Fact]
    public void Age_uses_occurrence_year() =>
        Assert.Equal(34, BirthdayCalendar.AgeOn(P(24, 10, 1992), D(2026, 10, 24)));

    [Fact]
    public void Age_is_null_without_year() =>
        Assert.Null(BirthdayCalendar.AgeOn(P(24, 10), D(2026, 10, 24)));

    [Fact]
    public void Upcoming_orders_by_days_until_then_name()
    {
        var people = new[] { P(1, 1, name: "Zed"), P(24, 10, name: "Bea"), P(24, 10, name: "Amy"), P(22, 10, name: "Max") };
        var names = BirthdayCalendar.Upcoming(people, D(2026, 10, 22)).Select(p => p.Name);
        Assert.Equal(new[] { "Max", "Amy", "Bea", "Zed" }, names);
    }

    [Theory]
    [InlineData(29, 2, true)]
    [InlineData(30, 2, false)]
    [InlineData(31, 4, false)]
    [InlineData(31, 12, true)]
    [InlineData(0, 5, false)]
    [InlineData(1, 13, false)]
    [InlineData(1, 0, false)]
    public void IsValidDate(int day, int month, bool expected) =>
        Assert.Equal(expected, BirthdayCalendar.IsValidDate(day, month));
}
