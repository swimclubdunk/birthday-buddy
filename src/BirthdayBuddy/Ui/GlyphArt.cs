using System.Drawing.Drawing2D;

namespace BirthdayBuddy.Ui;

/// <summary>Bold one-colour glyphs for icon buttons, drawn as vector shapes on a 24×24 grid.</summary>
public static class GlyphArt
{
    /// <summary>A gear: eight teeth around a solid ring with a hole.</summary>
    public static void Gear(Graphics g, RectangleF bounds, Color color)
    {
        const float body = 8.5f, tip = 11.5f, hole = 3.5f;
        var state = Begin(g, bounds);
        using var path = new GraphicsPath(); // alternate fill: the hole stays empty
        var points = new List<PointF>();
        for (int i = 0; i < 8; i++)
        {
            float a = i * 45;
            points.Add(Polar(body, a - 15));
            points.Add(Polar(tip, a - 9));
            points.Add(Polar(tip, a + 9));
            points.Add(Polar(body, a + 15));
            points.Add(Polar(body, a + 22.5f)); // keep the gap between teeth round
        }
        path.AddPolygon(points.ToArray());
        path.AddEllipse(12 - hole, 12 - hole, hole * 2, hole * 2);
        using var brush = new SolidBrush(color);
        g.FillPath(brush, path);
        g.Restore(state);
    }

    /// <summary>A return arrow (↩): points left, with a tail that curls back underneath.</summary>
    public static void Back(Graphics g, RectangleF bounds, Color color)
    {
        var state = Begin(g, bounds);
        using var brush = new SolidBrush(color);
        g.FillPolygon(brush, [new PointF(3, 10), new PointF(9.5f, 3.5f), new PointF(9.5f, 16.5f)]);
        using var pen = new Pen(color, 3f) { StartCap = LineCap.Flat, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        using var tail = new GraphicsPath();
        tail.AddLine(9, 10, 14.5f, 10);
        tail.AddArc(10, 10, 9.5f, 9.5f, 270, 180); // right half circle, top to bottom
        tail.AddLine(14.75f, 19.5f, 8, 19.5f);
        g.DrawPath(pen, tail);
        g.Restore(state);
    }

    static GraphicsState Begin(Graphics g, RectangleF bounds)
    {
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        float scale = Math.Min(bounds.Width, bounds.Height) / 24f;
        g.TranslateTransform(bounds.X + (bounds.Width - 24 * scale) / 2, bounds.Y + (bounds.Height - 24 * scale) / 2);
        g.ScaleTransform(scale, scale);
        return state;
    }

    static PointF Polar(float radius, float degrees)
    {
        double a = degrees * Math.PI / 180;
        return new PointF(12 + radius * (float)Math.Cos(a), 12 + radius * (float)Math.Sin(a));
    }
}
