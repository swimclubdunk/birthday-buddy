using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace BirthdayBuddy.Ui;

/// <summary>Draws the Birthday Buddy cake at any pixel size. Designed on a 64×64 grid.</summary>
public static class IconArt
{
    public static Bitmap Draw(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.ScaleTransform(size / 64f, size / 64f);

        using var sponge = new SolidBrush(Color.FromArgb(0xE2, 0x5C, 0x8A));
        using var frosting = new SolidBrush(Color.FromArgb(0xFF, 0xF4, 0xE8));
        using var candle = new SolidBrush(Color.FromArgb(0x5B, 0x9B, 0xE8));
        using var flame = new SolidBrush(Color.FromArgb(0xFF, 0xB0, 0x2E));

        using (var body = Shapes.RoundedRect(new RectangleF(6, 30, 52, 30), 8)) g.FillPath(sponge, body);
        using (var top = Shapes.RoundedRect(new RectangleF(6, 30, 52, 12), 6)) g.FillPath(frosting, top);
        g.FillEllipse(frosting, 12, 36, 9, 11);   // drips
        g.FillEllipse(frosting, 27, 36, 10, 9);
        g.FillEllipse(frosting, 43, 36, 9, 12);
        g.FillRectangle(candle, 29, 13, 6, 17);
        g.FillEllipse(flame, 27.5f, 1, 9, 13);
        return bmp;
    }
}
