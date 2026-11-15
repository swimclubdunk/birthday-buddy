using BirthdayBuddy.Core;

namespace BirthdayBuddy.Tests;

public class PresenceRulesTests
{
    [Theory]
    [InlineData(NotificationState.AcceptsNotifications, Presence.Show)]
    [InlineData(NotificationState.QuietTime, Presence.Show)]
    [InlineData(NotificationState.Unknown, Presence.Show)]
    [InlineData(NotificationState.Busy, Presence.Wait)]
    [InlineData(NotificationState.RunningD3DFullScreen, Presence.Wait)]
    [InlineData(NotificationState.App, Presence.Wait)]
    [InlineData(NotificationState.PresentationMode, Presence.Wait)]
    [InlineData(NotificationState.NotPresent, Presence.Wait)]
    public void Decide(NotificationState state, Presence expected) =>
        Assert.Equal(expected, PresenceRules.Decide(state, TimeSpan.FromMinutes(1), idleMinutes: 5));

    [Theory]
    [InlineData(NotificationState.AcceptsNotifications, 300, 5, Presence.Wait)]
    [InlineData(NotificationState.AcceptsNotifications, 299, 5, Presence.Show)]
    [InlineData(NotificationState.AcceptsNotifications, 10_800, 0, Presence.Show)]
    [InlineData(NotificationState.PresentationMode, 1, 0, Presence.Wait)]
    public void Idle(NotificationState state, int idleSeconds, int idleMinutes, Presence expected) =>
        Assert.Equal(expected, PresenceRules.Decide(state, TimeSpan.FromSeconds(idleSeconds), idleMinutes));
}
