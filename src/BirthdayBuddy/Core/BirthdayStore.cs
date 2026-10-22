using System.Text.Json;

namespace BirthdayBuddy.Core;

/// <param name="RecoveredFrom">Where the unreadable file was moved to, or null if it loaded fine.</param>
public sealed record LoadResult(AppData Data, string? RecoveredFrom);

/// <summary>Reads and writes birthdays.json. Saves are atomic; unreadable files are kept aside, never lost.</summary>
public sealed class BirthdayStore(string directoryPath)
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BirthdayBuddy");

    public string DirectoryPath { get; } = directoryPath;
    public string FilePath { get; } = Path.Combine(directoryPath, "birthdays.json");

    public LoadResult Load()
    {
        if (!File.Exists(FilePath)) return new LoadResult(new AppData(), null);

        try
        {
            var data = JsonSerializer.Deserialize<AppData>(File.ReadAllText(FilePath), Json)
                       ?? throw new JsonException("The file was empty.");
            return new LoadResult(Sanitize(data), null);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            Log.Error("Could not read birthdays.json; starting fresh.", ex);
            var backup = Path.Combine(DirectoryPath, $"birthdays.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.Move(FilePath, backup, overwrite: true);
            return new LoadResult(new AppData(), backup);
        }
    }

    public void Save(AppData data)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(data, Json));
        File.Move(temp, FilePath, overwrite: true);
    }

    /// <summary>Survives hand edits: drops people that can't be scheduled, clamps settings, fills nulls.</summary>
    static AppData Sanitize(AppData data)
    {
        var settings = data.Settings ?? new Settings();
        var people = (data.People ?? Array.Empty<Person>())
            .Where(p => p is not null && !string.IsNullOrWhiteSpace(p.Name) && BirthdayCalendar.IsValidDate(p.Day, p.Month))
            .DistinctBy(p => p.Id)
            .ToList();

        return data with
        {
            Settings = settings with { HeadsUpDays = Math.Clamp(settings.HeadsUpDays, 0, Settings.MaxHeadsUpDays) },
            People = people,
        };
    }
}
