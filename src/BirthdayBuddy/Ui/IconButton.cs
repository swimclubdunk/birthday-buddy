namespace BirthdayBuddy.Ui;

/// <summary>A square secondary button that shows a <see cref="GlyphArt"/> glyph in its text colour.</summary>
public sealed class IconButton : Button
{
    readonly Action<Graphics, RectangleF, Color> _glyph;

    /// <param name="name">Read by screen readers and shown as the tooltip.</param>
    public IconButton(string name, Action<Graphics, RectangleF, Color> glyph)
    {
        _glyph = glyph;
        AccessibleName = name;
        Tag = Role.SecondaryButton;
        FlatStyle = FlatStyle.Flat;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        Padding = new Padding(0);
        Margin = new Padding(6, 0, 0, 0);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        float inset = Math.Min(Width, Height) * 0.14f;
        _glyph(e.Graphics, new RectangleF(inset, inset, Width - 2 * inset, Height - 2 * inset), ForeColor);
    }
}
