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

    // Final review I1: an antivirus/backup lock at login must not make the list look lost.
    [Fact]
    public void Locked_file_is_left_alone_and_reported_unavailable()
    {
        Store().Save(new AppData { People = [P(24, 10)] });
        var original = File.ReadAllText(Store().FilePath);

        LoadResult result;
        using (new FileStream(Store().FilePath, FileMode.Open, FileAccess.Read, FileShare.None))
            result = Store().Load();

        Assert.True(result.Unavailable);
        Assert.Null(result.RecoveredFrom);
        Assert.Equal(original, File.ReadAllText(Store().FilePath));
        Assert.Empty(Directory.GetFiles(_dir.Path, "birthdays.corrupt-*"));
    }

    // Final review I1: if a corrupt file can't be moved aside, loading must not throw (it would crash every launch).
    [Fact]
    public void Corrupt_file_that_cannot_be_moved_does_not_throw()
    {
        File.WriteAllText(Store().FilePath, "{ not json");

        LoadResult result;
        using (new FileStream(Store().FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            result = Store().Load();

        Assert.True(result.Unavailable);
        Assert.Equal("{ not json", File.ReadAllText(Store().FilePath));
    }

    [Fact]
    public void Old_file_without_new_fields_loads_defaults()
    {
        File.WriteAllText(Store().FilePath, """{"version":1,"settings":{"headsUpDays":3,"startWithWindows":true},"people":[]}""");
        var settings = Store().Load().Data.Settings;
        Assert.Equal(5, settings.IdleMinutes);
        Assert.Equal(10, settings.PopupSeconds);
        Assert.Equal(1.0, settings.FanfareVolume);
        Assert.Null(settings.FanfareSound);
    }

    [Theory]
    [InlineData(1, 3)]
    [InlineData(10, 10)]
    [InlineData(600, 60)]
    public void Sanitize_clamps_popup_seconds(int seconds, int expected)
    {
        Store().Save(new AppData { Settings = new Settings { PopupSeconds = seconds } });
        Assert.Equal(expected, Store().Load().Data.Settings.PopupSeconds);
    }

    [Theory]
    [InlineData(90, 0.46, 60, 0.5)]
    [InlineData(-3, 7.0, 0, 1.0)]
    [InlineData(500, -1.0, 60, 0.0)]
    public void Sanitize_clamps_idle_and_snaps_volume(int idle, double volume, int expectedIdle, double expectedVolume)
    {
        Store().Save(new AppData { Settings = new Settings { IdleMinutes = idle, FanfareVolume = volume } });
        var settings = Store().Load().Data.Settings;
        Assert.Equal(expectedIdle, settings.IdleMinutes);
        Assert.Equal(expectedVolume, settings.FanfareVolume);
    }

    [Theory]
    [InlineData(@"..\x.mp3", null)]
    [InlineData(@"C:\x.mp3", null)]
    [InlineData("a/b.mp3", null)]
    [InlineData("", null)]
    [InlineData("Party Horn.MP3", "Party Horn.MP3")]
    public void Sanitize_drops_a_fanfare_sound_that_is_not_a_plain_file_name(string sound, string? expected)
    {
        Store().Save(new AppData { Settings = new Settings { FanfareSound = sound } });
        Assert.Equal(expected, Store().Load().Data.Settings.FanfareSound);
    }

    [Fact]
    public void Ack_marker_round_trips()
    {
        Store().Save(new AppData { People = [P(15, 11) with { LastBirthdayAck = D(2026, 11, 15) }] });
        Assert.Equal(D(2026, 11, 15), Assert.Single(Store().Load().Data.People).LastBirthdayAck);
    }
}
