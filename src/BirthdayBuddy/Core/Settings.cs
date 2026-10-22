namespace BirthdayBuddy.Core;

public sealed record Settings
{
    public const int MaxHeadsUpDays = 30;

    public int HeadsUpDays { get; init; } = 3;
    public bool StartWithWindows { get; init; } = true;
}
