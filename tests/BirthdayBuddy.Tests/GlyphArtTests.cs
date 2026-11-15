using System.Drawing;
using System.Drawing.Drawing2D;
using BirthdayBuddy.Ui;

namespace BirthdayBuddy.Tests;

public class GlyphArtTests
{
    static Bitmap Render(Action<Graphics, RectangleF, Color> draw, int size = 48)
    {
        var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        draw(g, new RectangleF(0, 0, size, size), Color.Black);
        return bmp;
    }

    static bool Ink(Bitmap bmp, double x, double y) => bmp.GetPixel((int)x, (int)y).A > 128;

    static (double X, double Y) Polar(double radius, double degrees, int size = 48)
    {
        double a = degrees * Math.PI / 180;
        return (size / 2.0 + radius * Math.Cos(a), size / 2.0 + radius * Math.Sin(a));
    }

    [Fact]
    public void Gear_has_a_hole_a_solid_ring_and_eight_teeth()
    {
        using var bmp = Render(GlyphArt.Gear);
        Assert.False(Ink(bmp, 24, 24)); // the hole
        for (int deg = 0; deg < 360; deg += 15)
        {
            var (x, y) = Polar(12, deg);
            Assert.True(Ink(bmp, x, y), $"ring has a gap at {deg}°");
        }
        int teeth = 0;
        bool wasInk = Ink(bmp, Polar(21, -1).X, Polar(21, -1).Y);
        for (int deg = 0; deg < 360; deg++)
        {
            var (x, y) = Polar(21, deg);
            bool ink = Ink(bmp, x, y);
            if (ink && !wasInk) teeth++;
            wasInk = ink;
        }
        Assert.Equal(8, teeth);
    }

    [Fact]
    public void Gear_is_bold_and_fills_its_bounds()
    {
        using var bmp = Render(GlyphArt.Gear);
        int ink = 0, top = 48, bottom = 0;
        for (int y = 0; y < 48; y++)
            for (int x = 0; x < 48; x++)
                if (Ink(bmp, x, y)) { ink++; top = Math.Min(top, y); bottom = Math.Max(bottom, y); }
        Assert.True(bottom - top >= 42, $"gear spans {bottom - top} of 48 px");
        Assert.True(ink > 48 * 48 * 0.4, $"gear covers {ink} of {48 * 48} px");
    }

    [Fact]
    public void Back_arrow_points_left()
    {
        using var bmp = Render(GlyphArt.Back);
        int leftInk = 0, rightInk = 0;
        for (int y = 0; y < 48; y++)
            for (int x = 0; x < 48; x++)
                if (Ink(bmp, x, y)) { if (x < 16) leftInk++; else if (x >= 32) rightInk++; }
        Assert.True(leftInk > 0 && rightInk > 0, "arrow spans the icon");
        Assert.True(Ink(bmp, 8, 20), "arrowhead tip on the left");
    }
}
