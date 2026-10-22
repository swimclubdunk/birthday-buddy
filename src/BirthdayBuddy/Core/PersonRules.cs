using System.Globalization;

namespace BirthdayBuddy.Core;

public static class PersonRules
{
    public const int MaxNameLength = 60;
    public const int MinYear = 1900;

    /// <summary>Returns a friendly error message, or null when the input is valid.</summary>
    public static string? Validate(string name, int day, int month, int? year, DateOnly today)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0) return "Please enter a name.";
        if (trimmed.Length > MaxNameLength) return $"Names can be up to {MaxNameLength} characters.";
        if (!BirthdayCalendar.IsValidDate(day, month)) return "That date doesn't exist.";

        if (year is int y)
        {
            if (y < MinYear || y > today.Year) return $"Year must be between {MinYear} and {today.Year}.";
            if (month == 2 && day == 29 && !DateTime.IsLeapYear(y)) return $"{y} wasn't a leap year.";
            if (new DateOnly(y, month, day) > today) return "That date is in the future.";
        }

        return null;
    }

    /// <summary>Blank means "no year". Returns false for anything that isn't a whole number.</summary>
    public static bool TryParseYear(string text, out int? year)
    {
        year = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int parsed)) return false;
        year = parsed;
        return true;
    }

    public static Person Create(string name, int day, int month, int? year) =>
        new() { Name = name.Trim(), Day = day, Month = month, Year = year };

    /// <summary>Applies an edit. Changing the day or month clears the sent-reminder markers.</summary>
    public static Person Edit(Person original, string name, int day, int month, int? year)
    {
        bool dateChanged = original.Day != day || original.Month != month;
        return original with
        {
            Name = name.Trim(),
            Day = day,
            Month = month,
            Year = year,
            LastHeadsUp = dateChanged ? null : original.LastHeadsUp,
            LastBirthdayNotice = dateChanged ? null : original.LastBirthdayNotice,
        };
    }
}
