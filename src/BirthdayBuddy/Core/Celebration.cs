namespace BirthdayBuddy.Core;

/// <summary>One person whose birthday popup is due today.</summary>
/// <param name="Age">The age they turn today, when the birth year is known.</param>
public sealed record Celebration(Guid PersonId, string Name, int? Age);
