using System.Runtime.InteropServices;
using BirthdayBuddy.Core;

namespace BirthdayBuddy.Ui;

/// <summary>
/// Asks Windows whether now is a good moment: full screen, presentation, locked, or idle.
/// Logs when the popup starts waiting and when it may show again, so a wait that never clears leaves a trace.
/// </summary>
/// <param name="state">Reads the Windows notification state; tests pass a fake.</param>
/// <param name="sinceLastInput">Time since the last keyboard or mouse input; tests pass a fake.</param>
/// <param name="log">Where waits are reported; defaults to log.txt.</param>
public sealed class UserPresence(Func<NotificationState>? state = null, Func<TimeSpan>? sinceLastInput = null, Action<string>? log = null)
    : IUserPresence
{
    readonly Action<string> _log = log ?? (message => Log.Error(message));
    bool _queryFailureLogged;
    bool _waiting;

    public Presence Check(int idleMinutes)
    {
        var current = state is null ? QueryState() : state();
        var idle = sinceLastInput is null ? SinceLastInput() : sinceLastInput();
        var decision = PresenceRules.Decide(current, idle, idleMinutes);

        if (decision == Presence.Wait && !_waiting)
            _log($"Birthday popup waiting: Windows reports {current}, last input {idle.TotalMinutes:0} min ago (idle limit {idleMinutes} min).");
        else if (decision == Presence.Show && _waiting)
            _log($"Birthday popup showing again: Windows reports {current}.");
        _waiting = decision == Presence.Wait;
        return decision;
    }

    NotificationState QueryState()
    {
        int hr = SHQueryUserNotificationState(out int value);
        if (hr == 0)
        {
            _queryFailureLogged = false;
            return Enum.IsDefined((NotificationState)value) ? (NotificationState)value : NotificationState.Unknown;
        }
        if (!_queryFailureLogged)
        {
            _log($"SHQueryUserNotificationState failed (0x{hr:X8}); showing the popup anyway.");
            _queryFailureLogged = true;
        }
        return NotificationState.Unknown;
    }

    static TimeSpan SinceLastInput()
    {
        var info = new LastInputInfo { cbSize = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;
        // Unsigned subtraction survives the 49.7-day tick-count wrap.
        return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.dwTime));
    }

    [StructLayout(LayoutKind.Sequential)]
    struct LastInputInfo
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("shell32.dll")]
    static extern int SHQueryUserNotificationState(out int state);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetLastInputInfo(ref LastInputInfo info);
}
