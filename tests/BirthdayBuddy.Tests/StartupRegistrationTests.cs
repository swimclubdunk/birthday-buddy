using BirthdayBuddy.Core;
using Microsoft.Win32;

namespace BirthdayBuddy.Tests;

public class StartupRegistrationTests : IDisposable
{
    const string TestRoot = @"Software\BirthdayBuddyTests";
    readonly StartupRegistration _startup = new($@"{TestRoot}\{Guid.NewGuid():N}", "BirthdayBuddy");

    public void Dispose() => Registry.CurrentUser.DeleteSubKeyTree(TestRoot, throwOnMissingSubKey: false);

    [Fact]
    public void Nothing_is_registered_initially() => Assert.Null(_startup.RegisteredCommand());

    // Review Focus 4
    [Fact]
    public void Register_quotes_path_with_spaces_and_adds_tray_flag()
    {
        _startup.Register(@"C:\Users\Jo Smith\Downloads\BirthdayBuddy.exe");
        Assert.Equal(@"""C:\Users\Jo Smith\Downloads\BirthdayBuddy.exe"" --tray", _startup.RegisteredCommand());
    }

    [Fact]
    public void Registering_again_follows_a_moved_exe()
    {
        _startup.Register(@"C:\Old\BirthdayBuddy.exe");
        _startup.Register(@"D:\New\BirthdayBuddy.exe");
        Assert.Equal(StartupRegistration.CommandFor(@"D:\New\BirthdayBuddy.exe"), _startup.RegisteredCommand());
    }

    [Fact]
    public void Unregister_removes_the_entry()
    {
        _startup.Register(@"C:\App\BirthdayBuddy.exe");
        _startup.Unregister();
        Assert.Null(_startup.RegisteredCommand());
    }

    [Fact]
    public void Unregister_when_missing_is_harmless() => _startup.Unregister();
}
