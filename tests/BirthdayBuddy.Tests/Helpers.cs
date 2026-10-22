using BirthdayBuddy.Core;

namespace BirthdayBuddy.Tests;

internal static class TestData
{
    public static DateOnly D(int year, int month, int day) => new(year, month, day);

    public static Person P(int day, int month, int? year = null, string name = "Sam") =>
        new() { Name = name, Day = day, Month = month, Year = year };
}

internal sealed class TempDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "BirthdayBuddyTests", Guid.NewGuid().ToString("N"));

    public TempDirectory() => Directory.CreateDirectory(Path);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
    }
}

/// <summary>A clock frozen at <see cref="Now"/>, in UTC so local == UTC.</summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
