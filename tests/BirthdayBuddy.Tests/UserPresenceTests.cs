using BirthdayBuddy.Core;
using BirthdayBuddy.Ui;

namespace BirthdayBuddy.Tests;

// Final review I2: a wait that never clears must leave a trace in log.txt.
public class UserPresenceTests
{
    [Fact]
    public void Logs_once_when_it_starts_waiting_and_once_when_it_shows_again()
    {
        var state = NotificationState.AcceptsNotifications;
        var log = new List<string>();
        var presence = new UserPresence(() => state, () => TimeSpan.FromMinutes(2), log.Add);

        presence.Check(5);
        state = NotificationState.Busy;
        presence.Check(5);
        presence.Check(5);
        state = NotificationState.AcceptsNotifications;
        presence.Check(5);

        Assert.Equal(2, log.Count);
        Assert.Contains("Busy", log[0]);
        Assert.Contains("2 min", log[0]);
        Assert.Contains("showing", log[1]);
    }

    [Fact]
    public void Decides_with_the_presence_rules()
    {
        var presence = new UserPresence(() => NotificationState.AcceptsNotifications, () => TimeSpan.FromMinutes(6), _ => { });
        Assert.Equal(Presence.Wait, presence.Check(5));
        Assert.Equal(Presence.Show, presence.Check(0));
    }
}
