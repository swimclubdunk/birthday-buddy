using BirthdayBuddy.Core;
using static BirthdayBuddy.Tests.TestData;

namespace BirthdayBuddy.Tests;

public class PeopleFilterTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_query_matches_everyone(string query) =>
        Assert.True(PeopleFilter.Matches(P(1, 1, name: "Sam"), query));

    [Theory]
    [InlineData("sam")]
    [InlineData("SAM")]
    [InlineData("anth")]
    [InlineData(" mant ")]
    public void Matches_any_part_of_the_name_ignoring_case_and_padding(string query) =>
        Assert.True(PeopleFilter.Matches(P(1, 1, name: "Samantha"), query));

    [Fact]
    public void Ignores_accents() =>
        Assert.True(PeopleFilter.Matches(P(1, 1, name: "Zoë"), "zoe"));

    [Fact]
    public void Non_matching_query_is_rejected() =>
        Assert.False(PeopleFilter.Matches(P(1, 1, name: "Samantha"), "alex"));
}
