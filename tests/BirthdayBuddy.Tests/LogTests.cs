using BirthdayBuddy.Core;

namespace BirthdayBuddy.Tests;

public class LogTests : IDisposable
{
    readonly TempDirectory _dir = new();
    readonly string? _previous = Log.FilePath;

    public void Dispose()
    {
        Log.FilePath = _previous;
        _dir.Dispose();
    }

    [Fact]
    public void Error_appends_message_and_exception()
    {
        Log.FilePath = Path.Combine(_dir.Path, "sub", "log.txt");
        Log.Error("Something broke", new InvalidOperationException("boom"));
        var text = File.ReadAllText(Log.FilePath);
        Assert.Contains("Something broke", text);
        Assert.Contains("boom", text);
    }

    [Fact]
    public void Error_without_path_does_nothing()
    {
        Log.FilePath = null;
        Log.Error("ignored"); // must not throw
    }

    [Fact]
    public void Large_log_is_rotated()
    {
        Log.FilePath = Path.Combine(_dir.Path, "log.txt");
        File.WriteAllText(Log.FilePath, new string('x', 1_100_000));
        Log.Error("fresh");
        Assert.True(File.Exists(Log.FilePath + ".old"));
        Assert.True(new FileInfo(Log.FilePath).Length < 1000);
    }
}
