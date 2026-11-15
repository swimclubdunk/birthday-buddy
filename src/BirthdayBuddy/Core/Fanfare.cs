namespace BirthdayBuddy.Core;

/// <summary>Plays a sound file at a volume (0.0–1.0). Returns false when the file can't be played.</summary>
public interface IFanfarePlayer
{
    bool TryPlay(string path, double volume);
    void Stop();
}

public interface IFanfare
{
    void Play();
    void Stop();
}

/// <summary>
/// The celebration sound: the owner's chosen file from the sounds folder, or the bundled fanfare.
/// The bundled fanfare is written to disk first, because the player only plays files.
/// </summary>
public sealed class Fanfare(IFanfarePlayer player, string dataDirectory, Func<Settings> settings) : IFanfare
{
    const string ResourceName = "BirthdayBuddy.fanfare.wav";

    public string SoundsDirectory { get; } = Path.Combine(dataDirectory, "sounds");
    public string DefaultPath { get; } = Path.Combine(dataDirectory, "default-fanfare.wav");

    public static byte[] BundledWav()
    {
        using var stream = typeof(Fanfare).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource {ResourceName}.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    public void Play() => Play(settings().FanfareVolume);

    /// <summary>Plays at <paramref name="volume"/> instead of the saved one (the Test button).</summary>
    public void Play(double volume)
    {
        if (volume <= 0)
        {
            player.Stop();
            return;
        }

        if (settings().FanfareSound is string name)
        {
            var custom = Path.Combine(SoundsDirectory, name);
            if (File.Exists(custom) && player.TryPlay(custom, volume)) return;
            Log.Error($"Could not play the chosen sound {name}; playing the default fanfare.");
        }

        try
        {
            EnsureDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error("Could not write the default fanfare.", ex);
        }
        if (!player.TryPlay(DefaultPath, volume)) Log.Error("Could not play the default fanfare.");
    }

    public void Stop() => player.Stop();

    /// <summary>Copies a chosen sound into the sounds folder under its own name, replacing the previous one.</summary>
    /// <returns>The file name to store in <see cref="Settings.FanfareSound"/>.</returns>
    public string Import(string sourcePath)
    {
        var source = Path.GetFullPath(sourcePath);
        var name = Path.GetFileName(source);
        var folder = Path.GetFullPath(SoundsDirectory);
        if (string.Equals(Path.GetDirectoryName(source), folder, StringComparison.OrdinalIgnoreCase)) return name;

        player.Stop(); // MCI keeps the last played file open, which would block deleting it
        Directory.CreateDirectory(folder);
        foreach (var old in Directory.GetFiles(folder)) File.Delete(old);
        File.Copy(source, Path.Combine(folder, name));
        return name;
    }

    void EnsureDefault()
    {
        var bundled = BundledWav();
        if (File.Exists(DefaultPath) && File.ReadAllBytes(DefaultPath).AsSpan().SequenceEqual(bundled)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(DefaultPath)!);
        File.WriteAllBytes(DefaultPath, bundled);
    }
}
