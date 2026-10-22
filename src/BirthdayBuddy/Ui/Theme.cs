using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace BirthdayBuddy.Ui;

public sealed record Palette(
    Color Window, Color Surface, Color SurfaceHover, Color Text, Color SubtleText,
    Color Accent, Color AccentText, Color Border, Color Danger);

public static class Theme
{
    public static readonly Palette Light = new(
        Window: Color.FromArgb(0xFA, 0xF8, 0xF6),
        Surface: Color.White,
        SurfaceHover: Color.FromArgb(0xF4, 0xEE, 0xEA),
        Text: Color.FromArgb(0x1F, 0x1B, 0x18),
        SubtleText: Color.FromArgb(0x6E, 0x66, 0x60),
        Accent: Color.FromArgb(0xD9, 0x4F, 0x7A),
        AccentText: Color.White,
        Border: Color.FromArgb(0xE8, 0xE2, 0xDC),
        Danger: Color.FromArgb(0xC4, 0x2B, 0x1C));

    public static readonly Palette Dark = new(
        Window: Color.FromArgb(0x1C, 0x1A, 0x19),
        Surface: Color.FromArgb(0x27, 0x24, 0x22),
        SurfaceHover: Color.FromArgb(0x31, 0x2E, 0x2B),
        Text: Color.FromArgb(0xF2, 0xEE, 0xEA),
        SubtleText: Color.FromArgb(0xA8, 0xA0, 0x99),
        Accent: Color.FromArgb(0xF0, 0x7A, 0xA0),
        AccentText: Color.FromArgb(0x1C, 0x1A, 0x19),
        Border: Color.FromArgb(0x3A, 0x36, 0x33),
        Danger: Color.FromArgb(0xFF, 0x8A, 0x80));

    public static readonly Font Title = new("Segoe UI Semibold", 15f);
    public static readonly Font Heading = new("Segoe UI Semibold", 12f);
    public static readonly Font Body = new("Segoe UI", 10f);
    public static readonly Font BodyBold = new("Segoe UI Semibold", 10.5f);
    public static readonly Font Small = new("Segoe UI", 9f);

    /// <summary>Follows Settings → Personalization → Colors → "Choose your app mode".</summary>
    public static bool IsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return false;
        }
    }

    public static void ApplyTitleBar(Form form, bool dark)
    {
        int value = dark ? 1 : 0;
        _ = DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref value, sizeof(int));
    }

    const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
