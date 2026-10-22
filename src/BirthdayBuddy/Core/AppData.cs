namespace BirthdayBuddy.Core;

public sealed record AppData
{
    public int Version { get; init; } = 1;
    public Settings Settings { get; init; } = new();
    public IReadOnlyList<Person> People { get; init; } = [];
}
