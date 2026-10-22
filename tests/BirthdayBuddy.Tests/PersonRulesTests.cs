using BirthdayBuddy.Core;
using static BirthdayBuddy.Tests.TestData;

namespace BirthdayBuddy.Tests;

public class PersonRulesTests
{
    static readonly DateOnly Today = D(2026, 10, 22);

    [Fact]
    public void Valid_input_has_no_error() =>
        Assert.Null(PersonRules.Validate("Sam", 24, 10, 1992, Today));

    [Fact]
    public void Year_is_optional() =>
        Assert.Null(PersonRules.Validate("Sam", 24, 10, null, Today));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Name_is_required(string name) =>
        Assert.Equal("Please enter a name.", PersonRules.Validate(name, 1, 1, null, Today));

    [Fact]
    public void Name_longer_than_60_is_rejected() =>
        Assert.Equal("Names can be up to 60 characters.", PersonRules.Validate(new string('a', 61), 1, 1, null, Today));

    [Fact]
    public void Name_is_measured_after_trimming() =>
        Assert.Null(PersonRules.Validate("  " + new string('a', 60) + "  ", 1, 1, null, Today));

    [Fact]
    public void Impossible_date_is_rejected() =>
        Assert.Equal("That date doesn't exist.", PersonRules.Validate("Sam", 31, 4, null, Today));

    [Fact]
    public void Leap_day_without_year_is_allowed() =>
        Assert.Null(PersonRules.Validate("Sam", 29, 2, null, Today));

    [Fact]
    public void Leap_day_in_non_leap_year_is_rejected() =>
        Assert.Equal("1991 wasn't a leap year.", PersonRules.Validate("Sam", 29, 2, 1991, Today));

    [Theory]
    [InlineData(1899)]
    [InlineData(2027)]
    public void Year_out_of_range_is_rejected(int year) =>
        Assert.Equal("Year must be between 1900 and 2026.", PersonRules.Validate("Sam", 1, 1, year, Today));

    [Fact]
    public void Date_later_this_year_is_in_the_future() =>
        Assert.Equal("That date is in the future.", PersonRules.Validate("Baby", 1, 12, 2026, Today));

    [Theory]
    [InlineData("", true, null)]
    [InlineData("  ", true, null)]
    [InlineData("1992", true, 1992)]
    [InlineData(" 1992 ", true, 1992)]
    [InlineData("19x2", false, null)]
    public void TryParseYear(string text, bool ok, int? expected)
    {
        Assert.Equal(ok, PersonRules.TryParseYear(text, out var year));
        Assert.Equal(expected, year);
    }

    [Fact]
    public void Create_trims_name()
    {
        var p = PersonRules.Create("  Sam ", 24, 10, null);
        Assert.Equal("Sam", p.Name);
        Assert.NotEqual(Guid.Empty, p.Id);
    }

    [Fact]
    public void Editing_the_date_resets_sent_markers()
    {
        var original = P(24, 10) with { LastHeadsUp = D(2026, 10, 24), LastBirthdayNotice = D(2025, 10, 24) };
        var edited = PersonRules.Edit(original, "Sam", 25, 10, null);
        Assert.Equal(original.Id, edited.Id);
        Assert.Null(edited.LastHeadsUp);
        Assert.Null(edited.LastBirthdayNotice);
    }

    [Fact]
    public void Editing_only_the_name_keeps_sent_markers()
    {
        var original = P(24, 10) with { LastHeadsUp = D(2026, 10, 24) };
        var edited = PersonRules.Edit(original, "Samantha", 24, 10, 1992);
        Assert.Equal("Samantha", edited.Name);
        Assert.Equal(1992, edited.Year);
        Assert.Equal(D(2026, 10, 24), edited.LastHeadsUp);
    }
}
