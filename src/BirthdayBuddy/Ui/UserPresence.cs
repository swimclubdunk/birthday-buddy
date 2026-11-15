using System.Runtime.InteropServices;
using BirthdayBuddy.Core;

namespace BirthdayBuddy.Ui;

/// <summary>Asks Windows whether now is a good moment: full screen, presentation, locked, or idle.</summary>
public sealed class UserPresence : IUserPresence
{
    bool _queryFailureLogged;

    public Presence Check(int idleMinutes) => PresenceRules.Decide(QueryState(), SinceLastInput(), idleMinutes);

    NotificationState QueryState()
    {
        int hr = SHQueryUserNotificationState(out int state);
        if (hr == 0)
        {
            _queryFailureLogged = false;
            return Enum.IsDefined((NotificationState)state) ? (NotificationState)state : NotificationState.Unknown;
        }
        if (!_queryFailureLogged)
        {
            Log.Error($"SHQueryUserNotificationState failed (0x{hr:X8}); showing the popup anyway.", null);
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
