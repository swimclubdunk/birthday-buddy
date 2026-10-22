namespace BirthdayBuddy.Core;

/// <summary>Decides which reminders are due right now. Pure: same input, same output.</summary>
public static class ReminderPlanner
{
    public static ReminderPlan Plan(IReadOnlyList<Person> people, Settings settings, DateOnly today)
    {
        int window = Math.Clamp(settings.HeadsUpDays, 0, Settings.MaxHeadsUpDays);
        var reminders = new List<Reminder>();
        var updated = new List<Person>(people.Count);

        foreach (var person in people)
        {
            var next = BirthdayCalendar.NextOccurrence(person, today);
            int days = next.DayNumber - today.DayNumber;
            var result = person;

            if (days == 0)
            {
                if (person.LastBirthdayNotice != today)
                {
                    reminders.Add(new Reminder(ReminderKind.Birthday, person.Name, next, 0, BirthdayCalendar.AgeOn(person, next)));
                    result = person with { LastBirthdayNotice = today };
                }
            }
            else if (days <= window && person.LastHeadsUp != next)
            {
                reminders.Add(new Reminder(ReminderKind.HeadsUp, person.Name, next, days, BirthdayCalendar.AgeOn(person, next)));
                result = person with { LastHeadsUp = next };
            }

            updated.Add(result);
        }

        var ordered = reminders
            .OrderBy(r => r.Kind == ReminderKind.Birthday ? 0 : 1)
            .ThenBy(r => r.DaysUntil)
            .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new ReminderPlan(ordered, updated);
    }
}
