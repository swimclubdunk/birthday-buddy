namespace BirthdayBuddy.Core;

public sealed record Person
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public int Day { get; init; }
    public int Month { get; init; }
    public int? Year { get; init; }

    /// <summary>The birthday occurrence a heads-up was last sent for.</summary>
    public DateOnly? LastHeadsUp { get; init; }

    /// <summary>The day the last day-of notification was sent.</summary>
    public DateOnly? LastBirthdayNotice { get; init; }
}
