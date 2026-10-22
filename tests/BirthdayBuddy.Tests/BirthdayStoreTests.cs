using BirthdayBuddy.Core;
using static BirthdayBuddy.Tests.TestData;

namespace BirthdayBuddy.Tests;

public class BirthdayStoreTests : IDisposable
{
    readonly TempDirectory _dir = new();
    BirthdayStore Store() => new(_dir.Path);
    public void Dispose() => _dir.Dispose();

    [Fact]
    public void Missing_file_loads_defaults()
    {
        var result = Store().Load();
        Assert.Null(result.RecoveredFrom);
        Assert.Empty(result.Data.People);
        Assert.Equal(3, result.Data.Settings.HeadsUpDays);
        Assert.True(result.Data.Settings.StartWithWindows);
    }

    [Fact]
    public void Save_then_load_round_trips()
    {
        var sam = P(24, 10, 1992) with { LastHeadsUp = D(2026, 10, 24), LastBirthdayNotice = D(2025, 10, 24) };
        var alex = P(29, 2, name: "Alex");
        var data = new AppData { Settings = new Settings { HeadsUpDays = 7, StartWithWindows = false }, People = [sam, alex] };

        Store().Save(data);
        var loaded = Store().Load().Data;

        Assert.Equal(data.Settings, loaded.Settings);
        Assert.Equal(new[] { sam, alex }, loaded.People);
    }

    [Fact]
    public void File_is_readable_camel_case_with_iso_dates()
    {
        Store().Save(new AppData { People = [P(24, 10) with { LastHeadsUp = D(2026, 10, 24) }] });
        var json = File.ReadAllText(Store().FilePath);
        Assert.Contains("\"headsUpDays\": 3", json);
        Assert.Contains("\"lastHeadsUp\": \"2026-10-24\"", json);
    }

    [Fact]
    public void Save_leaves_no_temp_file()
    {
        Store().Save(new AppData());
        Assert.False(File.Exists(Store().FilePath + ".tmp"));
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("")]
    [InlineData("null")]
    public void Unreadable_file_is_kept_aside_and_defaults_load(string content)
    {
        File.WriteAllText(Store().FilePath, content);

        var result = Store().Load();

        Assert.NotNull(result.RecoveredFrom);
        Assert.Matches(@"birthdays\.corrupt-\d{8}-\d{6}\.json$", result.RecoveredFrom);
        Assert.Equal(content, File.ReadAllText(result.RecoveredFrom!));
        Assert.False(File.Exists(Store().FilePath));
        Assert.Empty(result.Data.People);
    }

    // Review Focus 1
    [Fact]
    public void Invalid_entries_are_dropped_and_settings_clamped()
    {
        File.WriteAllText(Store().FilePath, """
            { "version": 1,
              "settings": { "headsUpDays": 999, "startWithWindows": false },
              "people": [
                { "name": "Ghost", "day": 31, "month": 2 },
                { "name": "   ", "day": 1, "month": 1 },
                { "name": "Sam", "day": 24, "month": 10 } ] }
            """);

        var result = Store().Load();

        Assert.Null(result.RecoveredFrom);
        Assert.Equal("Sam", Assert.Single(result.Data.People).Name);
        Assert.Equal(Settings.MaxHeadsUpDays, result.Data.Settings.HeadsUpDays);
        Assert.False(result.Data.Settings.StartWithWindows);
    }

    [Fact]
    public void Null_people_and_settings_become_defaults()
    {
        File.WriteAllText(Store().FilePath, """{ "version": 1, "settings": null, "people": null }""");
        var data = Store().Load().Data;
        Assert.Empty(data.People);
        Assert.Equal(new Settings(), data.Settings);
    }

    [Fact]
    public void Duplicate_ids_keep_the_first()
    {
        var sam = P(24, 10);
        Store().Save(new AppData { People = [sam, sam with { Name = "Copy" }] });
        Assert.Equal("Sam", Assert.Single(Store().Load().Data.People).Name);
    }
}
