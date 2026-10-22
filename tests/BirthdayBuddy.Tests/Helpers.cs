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

/// <summary>Runs posted callbacks immediately on the posting thread.</summary>
internal sealed class InlineSynchronizationContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback d, object? state) => d(state);
}

internal static class Sta
{
    /// <summary>Runs WinForms code on a fresh STA thread and rethrows anything it throws.</summary>
    public static void Run(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw();
    }
}
