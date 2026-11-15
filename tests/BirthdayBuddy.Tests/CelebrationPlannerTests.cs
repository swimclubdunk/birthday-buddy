using BirthdayBuddy.Core;
using static BirthdayBuddy.Tests.TestData;

namespace BirthdayBuddy.Tests;

public class CelebrationPlannerTests
{
    static readonly DateOnly Today = D(2026, 11, 15);

    [Fact]
    public void Due_today_and_not_acknowledged()
    {
        var sam = P(15, 11, 1992);
        Assert.Equal(new Celebration(sam.Id, "Sam", 34), Assert.Single(CelebrationPlanner.Due([sam], Today)));
    }

    [Fact]
    public void Acknowledged_today_is_not_due() =>
        Assert.Empty(CelebrationPlanner.Due([P(15, 11) with { LastBirthdayAck = Today }], Today));

    [Fact]
    public void Acknowledged_last_year_is_due() =>
        Assert.Single(CelebrationPlanner.Due([P(15, 11) with { LastBirthdayAck = D(2025, 11, 15) }], Today));

    [Fact]
    public void Leap_day_is_due_on_28_Feb_in_a_non_leap_year() =>
        Assert.Single(CelebrationPlanner.Due([P(29, 2)], D(2027, 2, 28)));

    [Fact]
    public void Not_today_is_not_due() => Assert.Empty(CelebrationPlanner.Due([P(16, 11)], Today));

    [Fact]
    public void Ordered_by_name_ignoring_case()
    {
        var due = CelebrationPlanner.Due([P(15, 11, name: "bob"), P(15, 11, name: "Alex"), P(15, 11, name: "Cleo")], Today);
        Assert.Equal(new[] { "Alex", "bob", "Cleo" }, due.Select(c => c.Name));
    }
}
