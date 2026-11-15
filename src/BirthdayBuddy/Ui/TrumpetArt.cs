using System.Drawing.Drawing2D;

namespace BirthdayBuddy.Ui;

/// <summary>A brass trumpet with a little banner, drawn as vector shapes on a 64×64 grid.</summary>
public static class TrumpetArt
{
    static readonly Color Brass = Color.FromArgb(0xF2, 0xB7, 0x2E);
    static readonly Color BrassDark = Color.FromArgb(0xB9, 0x7A, 0x12);
    static readonly Color BrassLight = Color.FromArgb(0xFF, 0xE0, 0x8A);

    /// <param name="mirror">Point the bell left instead of right.</param>
    /// <param name="banner">Banner colour hanging under the tube.</param>
    public static void Draw(Graphics g, RectangleF bounds, bool mirror, Color banner)
    {
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TranslateTransform(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
        float scale = Math.Min(bounds.Width, bounds.Height) / 64f;
        g.ScaleTransform(mirror ? -scale : scale, scale);
        g.RotateTransform(-18); // bell raised, like a fanfare
        g.TranslateTransform(-32, -32);

        using var brass = new SolidBrush(Brass);
        using var dark = new SolidBrush(BrassDark);
        using var light = new SolidBrush(BrassLight);
        using var outline = new Pen(BrassDark, 1.2f);
        using var bannerBrush = new SolidBrush(banner);

        // Banner hanging from the tube, swallow-tailed.
        using (var flag = new GraphicsPath())
        {
            flag.AddPolygon([new PointF(16, 36), new PointF(36, 36), new PointF(34, 48), new PointF(26, 44), new PointF(18, 48)]);
            g.FillPath(bannerBrush, flag);
        }

        // Mouthpiece and lead pipe.
        g.FillRectangle(dark, 2, 30.5f, 5, 4);
        g.FillRectangle(brass, 6, 31, 34, 3.5f);
        g.DrawRectangle(outline, 6, 31, 34, 3.5f);

        // Valve block and caps.
        g.FillRectangle(brass, 16, 24, 15, 7);
        g.DrawRectangle(outline, 16, 24, 15, 7);
        foreach (float x in new[] { 17.5f, 22.5f, 27.5f })
        {
            g.FillRectangle(light, x, 19, 3, 5);
            g.FillRectangle(dark, x - 0.5f, 18, 4, 1.8f);
        }

        // Bell.
        using (var bell = new GraphicsPath())
        {
            bell.AddBezier(new PointF(38, 31), new PointF(48, 30), new PointF(54, 24), new PointF(61, 18));
            bell.AddLine(new PointF(61, 18), new PointF(61, 48));
            bell.AddBezier(new PointF(61, 48), new PointF(54, 41), new PointF(48, 35.5f), new PointF(38, 34.5f));
            bell.CloseFigure();
            g.FillPath(brass, bell);
            g.DrawPath(outline, bell);
        }
        using (var rim = new Pen(BrassLight, 2f)) g.DrawLine(rim, 60, 19.5f, 60, 46.5f);

        g.Restore(state);
    }
}
