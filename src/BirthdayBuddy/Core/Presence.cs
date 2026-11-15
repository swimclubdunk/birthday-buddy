namespace BirthdayBuddy.Core;

public enum Presence { Show, Wait }

/// <summary>What Windows reports about the user's state. Values match QUERY_USER_NOTIFICATION_STATE.</summary>
public enum NotificationState
{
    Unknown = 0,
    NotPresent = 1,
    Busy = 2,
    RunningD3DFullScreen = 3,
    PresentationMode = 4,
    AcceptsNotifications = 5,
    QuietTime = 6,
    App = 7,
}

public interface IUserPresence
{
    /// <param name="idleMinutes">Wait when there was no keyboard or mouse input for this long; 0 turns the idle check off.</param>
    Presence Check(int idleMinutes);
}

/// <summary>When the celebration popup may appear. Pure.</summary>
public static class PresenceRules
{
    public static Presence Decide(NotificationState state, TimeSpan sinceLastInput, int idleMinutes)
    {
        if (idleMinutes > 0 && sinceLastInput >= TimeSpan.FromMinutes(idleMinutes)) return Presence.Wait;

        return state switch
        {
            NotificationState.Busy or NotificationState.RunningD3DFullScreen or NotificationState.App
                or NotificationState.PresentationMode or NotificationState.NotPresent => Presence.Wait,
            _ => Presence.Show, // normal, quiet time (first hour on a new install), or unknown
        };
    }
}
