namespace BirthdayBuddy.Core;

public enum ReminderKind { Birthday, HeadsUp }

/// <param name="Date">The birthday occurrence this reminder is about.</param>
public sealed record Reminder(ReminderKind Kind, string Name, DateOnly Date, int DaysUntil, int? Age);

/// <param name="UpdatedPeople">All people, same order, with markers set for every reminder that fired.</param>
public sealed record ReminderPlan(IReadOnlyList<Reminder> Reminders, IReadOnlyList<Person> UpdatedPeople);
