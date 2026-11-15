namespace BirthdayBuddy.Core;

/// <summary>Decides whose birthday popup is due. Pure: same input, same output.</summary>
public static class CelebrationPlanner
{
    /// <summary>People whose birthday is today and who haven't been acknowledged today, ordered by name.</summary>
    public static IReadOnlyList<Celebration> Due(IEnumerable<Person> people, DateOnly today) =>
        people.Where(p => BirthdayCalendar.NextOccurrence(p, today) == today && p.LastBirthdayAck != today)
              .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
              .Select(p => new Celebration(p.Id, p.Name, BirthdayCalendar.AgeOn(p, today)))
              .ToList();
}
