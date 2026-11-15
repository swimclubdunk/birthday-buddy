namespace BirthdayBuddy.Core;

public sealed record Settings
{
    public const int MaxHeadsUpDays = 30;
    public const int MaxIdleMinutes = 60;

    public int HeadsUpDays { get; init; } = 3;
    public bool StartWithWindows { get; init; } = true;

    /// <summary>Minutes without keyboard or mouse input after which the celebration popup waits; 0 turns the check off.</summary>
    public int IdleMinutes { get; init; } = 5;

    /// <summary>0.0 to 1.0 in steps of 0.1; see <see cref="SnapVolume"/>.</summary>
    public double FanfareVolume { get; init; } = 1.0;

    /// <summary>A file name inside the sounds folder, or null for the bundled fanfare.</summary>
    public string? FanfareSound { get; init; }

    /// <summary>Clamps to 0.0–1.0 and rounds to one decimal; anything that isn't a number becomes 1.0.</summary>
    public static double SnapVolume(double volume) =>
        double.IsFinite(volume) ? Math.Round(Math.Clamp(volume, 0, 1), 1, MidpointRounding.AwayFromZero) : 1.0;
}
