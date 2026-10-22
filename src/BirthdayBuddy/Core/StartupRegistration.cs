using Microsoft.Win32;

namespace BirthdayBuddy.Core;

/// <summary>Per-user "start at login" entry in the HKCU Run key. No admin rights needed.</summary>
public sealed class StartupRegistration
{
    public const string DefaultRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    readonly string _runKeyPath;
    readonly string _valueName;

    public StartupRegistration() : this(DefaultRunKeyPath, "BirthdayBuddy") { }

    public StartupRegistration(string runKeyPath, string valueName)
    {
        _runKeyPath = runKeyPath;
        _valueName = valueName;
    }

    public static string CommandFor(string exePath) => $"\"{exePath}\" --tray";

    public string? RegisteredCommand()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath);
        return key?.GetValue(_valueName) as string;
    }

    /// <summary>Adds the entry, or points it at <paramref name="exePath"/> if the exe moved.</summary>
    public void Register(string exePath)
    {
        var command = CommandFor(exePath);
        using var key = Registry.CurrentUser.CreateSubKey(_runKeyPath);
        if (key.GetValue(_valueName) as string != command) key.SetValue(_valueName, command);
    }

    public void Unregister()
    {
        using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath, writable: true);
        key?.DeleteValue(_valueName, throwOnMissingValue: false);
    }
}
