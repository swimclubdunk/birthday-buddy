namespace BirthdayBuddy.Ui;

/// <summary>
/// A faint per-month wash for cards: 12 hues evenly spaced around the colour wheel,
/// blended into the palette's surface so months group gently without shouting.
/// </summary>
public static class MonthTint
{
    const float Saturation = 0.65f;
    const float Lightness = 0.55f;

    public static Color For(int month, Palette palette, bool hover = false)
    {
        bool dark = palette.Surface.GetBrightness() < 0.5f;
        float strength = (dark ? 0.12f : 0.08f) + (hover ? 0.05f : 0f);
        var hue = FromHsl((month - 1) * 30f, Saturation, Lightness);
        return Mix(palette.Surface, hue, strength);
    }

    static Color Mix(Color under, Color over, float amount) => Color.FromArgb(
        (int)Math.Round(under.R + (over.R - under.R) * amount),
        (int)Math.Round(under.G + (over.G - under.G) * amount),
        (int)Math.Round(under.B + (over.B - under.B) * amount));

    static Color FromHsl(float hue, float s, float l)
    {
        float c = (1 - Math.Abs(2 * l - 1)) * s;
        float x = c * (1 - Math.Abs(hue / 60f % 2 - 1));
        float m = l - c / 2;
        var (r, g, b) = (int)(hue / 60f) switch
        {
            0 => (c, x, 0f),
            1 => (x, c, 0f),
            2 => (0f, c, x),
            3 => (0f, x, c),
            4 => (x, 0f, c),
            _ => (c, 0f, x),
        };
        return Color.FromArgb((int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
    }
}
