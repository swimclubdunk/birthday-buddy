using BirthdayBuddy.Core;
using static BirthdayBuddy.Tests.TestData;

namespace BirthdayBuddy.Tests;

public class ReminderPlannerTests
{
    static readonly Settings ThreeDays = new() { HeadsUpDays = 3 };
    static readonly DateOnly Today = D(2026, 10, 22);

    [Fact]
    public void Birthday_today_fires_with_age_and_is_marked()
    {
        var plan = ReminderPlanner.Plan([P(22, 10, 1992)], ThreeDays, Today);
        Assert.Equal(new Reminder(ReminderKind.Birthday, "Sam", Today, 0, 34), Assert.Single(plan.Reminders));
        Assert.Equal(Today, Assert.Single(plan.UpdatedPeople).LastBirthdayNotice);
    }

    [Fact]
    public void Birthday_already_noticed_today_does_not_fire_again()
    {
        var sam = P(22, 10) with { LastBirthdayNotice = Today };
        Assert.Empty(ReminderPlanner.Plan([sam], ThreeDays, Today).Reminders);
    }

    [Fact]
    public void Birthday_noticed_last_year_fires_this_year()
    {
        var sam = P(22, 10) with { LastBirthdayNotice = D(2025, 10, 22) };
        Assert.Equal(ReminderKind.Birthday, Assert.Single(ReminderPlanner.Plan([sam], ThreeDays, Today).Reminders).Kind);
    }

    [Fact]
    public void Birthday_today_does_not_also_send_a_heads_up()
    {
        var plan = ReminderPlanner.Plan([P(22, 10)], ThreeDays, Today);
        Assert.Equal(ReminderKind.Birthday, Assert.Single(plan.Reminders).Kind);
        Assert.Null(Assert.Single(plan.UpdatedPeople).LastHeadsUp);
    }

    [Fact]
    public void Leap_day_birthday_fires_on_28_Feb_in_non_leap_year() =>
        Assert.Equal(ReminderKind.Birthday,
            Assert.Single(ReminderPlanner.Plan([P(29, 2)], ThreeDays, D(2027, 2, 28)).Reminders).Kind);

    [Fact]
    public void Heads_up_inside_window_fires_and_marks_the_occurrence()
    {
        var plan = ReminderPlanner.Plan([P(24, 10)], ThreeDays, Today);
        Assert.Equal(new Reminder(ReminderKind.HeadsUp, "Sam", D(2026, 10, 24), 2, null), Assert.Single(plan.Reminders));
        Assert.Equal(D(2026, 10, 24), Assert.Single(plan.UpdatedPeople).LastHeadsUp);
    }

    [Fact]
    public void Heads_up_already_sent_for_this_occurrence_does_not_repeat()
    {
        var sam = P(24, 10) with { LastHeadsUp = D(2026, 10, 24) };
        Assert.Empty(ReminderPlanner.Plan([sam], ThreeDays, D(2026, 10, 23)).Reminders);
    }

    [Fact]
    public void Heads_up_sent_last_year_fires_again_this_year()
    {
        var sam = P(24, 10) with { LastHeadsUp = D(2025, 10, 24) };
        Assert.Single(ReminderPlanner.Plan([sam], ThreeDays, Today).Reminders);
    }

    [Fact]
    public void Outside_window_nothing_fires_and_person_is_unchanged()
    {
        var sam = P(30, 10);
        var plan = ReminderPlanner.Plan([sam], ThreeDays, Today);
        Assert.Empty(plan.Reminders);
        Assert.Equal(sam, Assert.Single(plan.UpdatedPeople));
    }

    [Fact]
    public void Zero_heads_up_days_disables_heads_ups() =>
        Assert.Empty(ReminderPlanner.Plan([P(23, 10)], new Settings { HeadsUpDays = 0 }, Today).Reminders);

    [Fact]
    public void Heads_up_window_crosses_new_year() =>
        Assert.Equal(new Reminder(ReminderKind.HeadsUp, "Sam", D(2027, 1, 2), 3, null),
            Assert.Single(ReminderPlanner.Plan([P(2, 1)], ThreeDays, D(2026, 12, 30)).Reminders));

    [Fact]
    public void First_check_mid_window_still_fires() =>
        Assert.Equal(2, Assert.Single(ReminderPlanner.Plan([P(24, 10)], new Settings { HeadsUpDays = 7 }, Today).Reminders).DaysUntil);

    [Fact]
    public void Planning_again_on_its_own_output_fires_nothing()
    {
        var first = ReminderPlanner.Plan([P(22, 10), P(24, 10, name: "Alex")], ThreeDays, Today);
        Assert.Equal(2, first.Reminders.Count);
        Assert.Empty(ReminderPlanner.Plan(first.UpdatedPeople, ThreeDays, Today).Reminders);
    }

    // Review Focus 5
    [Fact]
    public void Widening_the_window_after_a_heads_up_does_not_repeat_it()
    {
        var first = ReminderPlanner.Plan([P(24, 10)], ThreeDays, Today);
        Assert.Empty(ReminderPlanner.Plan(first.UpdatedPeople, new Settings { HeadsUpDays = 7 }, Today).Reminders);
    }

    [Fact]
    public void Reminders_are_ordered_birthdays_first_then_soonest_then_name()
    {
        var people = new[] { P(25, 10, name: "Zoe"), P(24, 10, name: "Bob"), P(22, 10, name: "Max"), P(24, 10, name: "Amy") };
        var names = ReminderPlanner.Plan(people, ThreeDays, Today).Reminders.Select(r => r.Name);
        Assert.Equal(new[] { "Max", "Amy", "Bob", "Zoe" }, names);
    }

    [Fact]
    public void Heads_up_days_above_maximum_are_clamped_to_30()
    {
        var huge = new Settings { HeadsUpDays = 99 };
        Assert.Single(ReminderPlanner.Plan([P(21, 11)], huge, Today).Reminders);   // 30 days away
        Assert.Empty(ReminderPlanner.Plan([P(22, 11)], huge, Today).Reminders);    // 31 days away
    }
}
