namespace BirthdayBuddy.Core;

/// <summary>Pure date math for birthdays. No clock, no I/O.</summary>
public static class BirthdayCalendar
{
    /// <summary>The person's birthday in <paramref name="year"/>; 29 Feb becomes 28 Feb in non-leap years.</summary>
    public static DateOnly OccurrenceIn(Person person, int year)
    {
        int day = person.Month == 2 && person.Day == 29 && !DateTime.IsLeapYear(year) ? 28 : person.Day;
        return new DateOnly(year, person.Month, day);
    }

    public static DateOnly NextOccurrence(Person person, DateOnly today)
    {
        var thisYear = OccurrenceIn(person, today.Year);
        return thisYear >= today ? thisYear : OccurrenceIn(person, today.Year + 1);
    }

    public static int DaysUntil(Person person, DateOnly today) =>
        NextOccurrence(person, today).DayNumber - today.DayNumber;

    public static int? AgeOn(Person person, DateOnly occurrence) =>
        person.Year is int year ? occurrence.Year - year : null;

    public static IReadOnlyList<Person> Upcoming(IEnumerable<Person> people, DateOnly today) =>
        people.OrderBy(p => DaysUntil(p, today))
              .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
              .ToList();

    /// <summary>True if the day exists in the month in at least one year (so 29 Feb is valid).</summary>
    public static bool IsValidDate(int day, int month) =>
        month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(2000, month);
}
