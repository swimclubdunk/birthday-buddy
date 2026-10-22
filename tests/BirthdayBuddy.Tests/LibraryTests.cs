using BirthdayBuddy.Core;
using static BirthdayBuddy.Tests.TestData;

namespace BirthdayBuddy.Tests;

public class LibraryTests : IDisposable
{
    readonly TempDirectory _dir = new();
    BirthdayStore Store() => new(_dir.Path);
    public void Dispose() => _dir.Dispose();

    (Library library, Func<int> changes) Create(AppData? data = null)
    {
        var library = new Library(Store(), data ?? new AppData());
        int count = 0;
        library.Changed += (_, _) => count++;
        return (library, () => count);
    }

    [Fact]
    public void Upsert_adds_persists_and_raises_changed()
    {
        var (library, changes) = Create();
        var sam = P(24, 10);

        library.Upsert(sam);

        Assert.Equal(new[] { sam }, library.Data.People);
        Assert.Equal(new[] { sam }, Store().Load().Data.People);
        Assert.Equal(1, changes());
    }

    [Fact]
    public void Upsert_existing_replaces_it()
    {
        var (library, _) = Create();
        var sam = P(24, 10);
        library.Upsert(sam);
        library.Upsert(sam with { Name = "Samantha" });
        Assert.Equal("Samantha", Assert.Single(library.Data.People).Name);
    }

    [Fact]
    public void Remove_deletes_by_id()
    {
        var sam = P(24, 10);
        var (library, changes) = Create(new AppData { People = [sam, P(1, 1, name: "Jo")] });
        library.Remove(sam.Id);
        Assert.Equal("Jo", Assert.Single(Store().Load().Data.People).Name);
        Assert.Equal(1, changes());
    }

    [Fact]
    public void UpdateSettings_persists_and_raises_changed()
    {
        var (library, changes) = Create();
        library.UpdateSettings(new Settings { HeadsUpDays = 10, StartWithWindows = false });
        Assert.Equal(10, Store().Load().Data.Settings.HeadsUpDays);
        Assert.Equal(1, changes());
    }

    [Fact]
    public void RecordSent_persists_without_raising_changed()
    {
        var sam = P(24, 10);
        var (library, changes) = Create(new AppData { People = [sam] });
        library.RecordSent([sam with { LastHeadsUp = D(2026, 10, 24) }]);
        Assert.Equal(D(2026, 10, 24), Assert.Single(Store().Load().Data.People).LastHeadsUp);
        Assert.Equal(0, changes());
    }

    // Final review I1: when the file couldn't be opened, never overwrite it.
    [Fact]
    public void Read_only_library_keeps_changes_in_memory_without_saving()
    {
        var library = new Library(Store(), new AppData(), readOnly: true);
        library.Upsert(P(24, 10));
        Assert.Single(library.Data.People);
        Assert.False(File.Exists(Store().FilePath));
    }
}
