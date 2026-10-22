using System.Globalization;

namespace BirthdayBuddy.Core;

/// <summary>Every user-facing sentence lives here, so wording stays consistent and testable.</summary>
public static class Wording
{
    public const int MaxTitle = 63;     // NotifyIcon balloon title limit
    public const int MaxBody = 255;     // NotifyIcon balloon text limit
    public const int MaxTooltip = 127;  // NotifyIcon.Text limit
    const int MaxLines = 4;

    public static (string Title, string Body) Notification(IReadOnlyList<Reminder> reminders, CultureInfo culture)
    {
        if (reminders.Count == 0) throw new ArgumentException("Nothing to notify about.", nameof(reminders));

        int birthdays = reminders.Count(r => r.Kind == ReminderKind.Birthday);
        int headsUps = reminders.Count - birthdays;
        string title = birthdays switch
        {
            0 => headsUps == 1 ? "Upcoming birthday" : $"{headsUps} upcoming birthdays",
            1 => "🎂 Birthday today",
            _ => $"🎂 {birthdays} birthdays today",
        };

        var lines = reminders.Select(r => Line(r, culture)).ToList();
        if (lines.Count > MaxLines)
            lines = [.. lines.Take(MaxLines - 1), $"…and {reminders.Count - (MaxLines - 1)} more."];

        int perLine = (MaxBody - (lines.Count - 1)) / lines.Count;
        return (title, string.Join("\n", lines.Select(l => Truncate(l, perLine))));
    }

    public static string Line(Reminder r, CultureInfo culture) => r.Kind == ReminderKind.Birthday
        ? (r.Age is int age ? $"It's {r.Name}'s birthday today! Turning {age}." : $"It's {r.Name}'s birthday today!")
        : $"{r.Name}'s birthday is {When(r.DaysUntil)} ({r.Date.ToString("ddd d MMM", culture)}).";

    public static string Status(int daysUntil) => daysUntil switch
    {
        0 => "Today 🎉",
        1 => "Tomorrow",
        _ => $"in {daysUntil} days",
    };

    public static string Detail(DateOnly occurrence, int? age, CultureInfo culture)
    {
        var date = occurrence.ToString("d MMM", culture);
        return age is int a ? $"{date} · turns {a}" : date;
    }

    public static string UpcomingLine(Person person, int daysUntil) => $"{person.Name} · {Status(daysUntil)}";

    public static string NoMatch(string query) => $"No one matches “{query.Trim()}”";

    /// <param name="upcoming">People already sorted by <see cref="BirthdayCalendar.Upcoming"/>.</param>
    public static string Subtitle(IReadOnlyList<Person> upcoming, DateOnly today)
    {
        if (upcoming.Count == 0) return "No birthdays yet";

        var todays = upcoming.Where(p => BirthdayCalendar.DaysUntil(p, today) == 0).Select(p => p.Name).ToList();
        if (todays.Count > 0) return $"🎉 Today: {string.Join(", ", todays)}";

        var next = upcoming[0];
        return $"Next up: {next.Name}, {When(BirthdayCalendar.DaysUntil(next, today))}";
    }

    public static string Tooltip(IEnumerable<string> namesWithBirthdayToday)
    {
        var names = namesWithBirthdayToday.ToList();
        var text = names.Count switch
        {
            0 => "Birthday Buddy",
            1 => $"🎂 {names[0]}'s birthday today",
            _ => $"🎂 Birthdays today: {string.Join(", ", names)}",
        };
        return Truncate(text, MaxTooltip);
    }

    static string When(int days) => days == 1 ? "tomorrow" : $"in {days} days";

    static string Truncate(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}
