using System.Text.Json;

namespace BirthdayBuddy.Core;

/// <param name="RecoveredFrom">Where the unreadable file was moved to, or null if it loaded fine.</param>
/// <param name="Unavailable">The file exists but couldn't be opened or moved aside; it must not be overwritten.</param>
public sealed record LoadResult(AppData Data, string? RecoveredFrom, bool Unavailable = false);

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

        string json;
        try
        {
            json = ReadWithRetry();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Locked (antivirus, backup) or no permission: the content may be fine, so leave it alone.
            Log.Error("Could not open birthdays.json; running without saving.", ex);
            return new LoadResult(new AppData(), null, Unavailable: true);
        }

        try
        {
            var data = JsonSerializer.Deserialize<AppData>(json, Json) ?? throw new JsonException("The file was empty.");
            return new LoadResult(Sanitize(data), null);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            Log.Error("Could not read birthdays.json; starting fresh.", ex);
            var backup = Path.Combine(DirectoryPath, $"birthdays.corrupt-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            try
            {
                File.Move(FilePath, backup, overwrite: true);
                return new LoadResult(new AppData(), backup);
            }
            catch (Exception moveEx) when (moveEx is IOException or UnauthorizedAccessException)
            {
                Log.Error("Could not move the unreadable birthdays.json aside; running without saving.", moveEx);
                return new LoadResult(new AppData(), null, Unavailable: true);
            }
        }
    }

    /// <summary>Rides out short locks, e.g. an antivirus scan at login.</summary>
    string ReadWithRetry()
    {
        const int attempts = 5;
        for (int i = 1; ; i++)
        {
            try
            {
                return File.ReadAllText(FilePath);
            }
            catch (IOException) when (i < attempts)
            {
                Thread.Sleep(200);
            }
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
