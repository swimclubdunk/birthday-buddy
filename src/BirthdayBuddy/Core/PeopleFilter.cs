using System.Globalization;

namespace BirthdayBuddy.Core;

/// <summary>The search box rule: any part of the name, ignoring case and accents.</summary>
public static class PeopleFilter
{
    const CompareOptions Loose = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    public static bool Matches(Person person, string query)
    {
        var q = query.Trim();
        return q.Length == 0 || CultureInfo.CurrentCulture.CompareInfo.IndexOf(person.Name, q, Loose) >= 0;
    }
}
