namespace BirthdayBuddy.Core;

/// <summary>Append-only error log. Never throws: logging must not take the app down.</summary>
public static class Log
{
    const long MaxBytes = 1_000_000;

    public static string? FilePath { get; set; }

    public static void Error(string message, Exception? ex = null)
    {
        if (FilePath is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var info = new FileInfo(FilePath);
            if (info.Exists && info.Length > MaxBytes) File.Move(FilePath, FilePath + ".old", overwrite: true);

            var detail = ex is null ? "" : Environment.NewLine + ex;
            File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{detail}{Environment.NewLine}");
        }
        catch
        {
            // Nowhere left to report this.
        }
    }
}
