using System.Drawing;
using BirthdayBuddy.Ui;

namespace BirthdayBuddy.Tests;

public class MonthTintTests
{
    static int Distance(Color a, Color b) =>
        Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));

    public static TheoryData<string> Palettes => new() { "Light", "Dark" };
    static Palette Get(string name) => name == "Light" ? Theme.Light : Theme.Dark;

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Every_month_gets_its_own_tint(string palette)
    {
        var tints = Enumerable.Range(1, 12).Select(m => MonthTint.For(m, Get(palette)).ToArgb()).ToList();
        Assert.Equal(12, tints.Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Tints_stay_a_gentle_wash_over_the_card_surface(string palette)
    {
        var p = Get(palette);
        foreach (int month in Enumerable.Range(1, 12))
        {
            int d = Distance(MonthTint.For(month, p), p.Surface);
            Assert.InRange(d, 4, 40);
        }
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void Hover_is_a_little_stronger_than_rest(string palette)
    {
        var p = Get(palette);
        Assert.True(Distance(MonthTint.For(5, p, hover: true), p.Surface) > Distance(MonthTint.For(5, p), p.Surface));
    }

    [Fact]
    public void Tints_are_opaque() => Assert.Equal(255, MonthTint.For(1, Theme.Light).A);
}
