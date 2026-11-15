using BirthdayBuddy.Core;

namespace BirthdayBuddy.Tests;

public class SettingsTests
{
    [Theory]
    [InlineData(0.74, 0.7)]
    [InlineData(0.75, 0.8)]
    [InlineData(-0.2, 0.0)]
    [InlineData(1.3, 1.0)]
    [InlineData(double.NaN, 1.0)]
    [InlineData(double.PositiveInfinity, 1.0)]
    public void SnapVolume(double input, double expected) => Assert.Equal(expected, Settings.SnapVolume(input));

    [Fact]
    public void Defaults()
    {
        var settings = new Settings();
        Assert.Equal(5, settings.IdleMinutes);
        Assert.Equal(10, settings.PopupSeconds);
        Assert.Equal(1.0, settings.FanfareVolume);
        Assert.Null(settings.FanfareSound);
    }
}
