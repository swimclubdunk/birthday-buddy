using System.Drawing.Drawing2D;
using System.Globalization;
using BirthdayBuddy.Core;

namespace BirthdayBuddy.Ui;

/// <summary>Owner-drawn list of soft cards: name and date on the left, status on the right.</summary>
public sealed class PeopleList : ListBox
{
    const int RowHeight = 48;
    const TextFormatFlags TextFlags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;

    Palette _palette = Theme.Light;
    DateOnly _today = DateOnly.FromDateTime(DateTime.Today);
    int _hover = -1;

    public event EventHandler<Person>? PersonActivated;

    public PeopleList()
    {
        DrawMode = DrawMode.OwnerDrawFixed;
        BorderStyle = BorderStyle.None;
        IntegralHeight = false;
        Cursor = Cursors.Hand;
        ItemHeight = RowHeight;
    }

    public void ApplyPalette(Palette palette)
    {
        _palette = palette;
        BackColor = palette.Window;
        Invalidate();
    }

    public void SetPeople(IReadOnlyList<Person> sorted, DateOnly today)
    {
        _today = today;
        _hover = -1;
        BeginUpdate();
        Items.Clear();
        foreach (var person in sorted) Items.Add(person);
        EndUpdate();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ItemHeight = LogicalToDeviceUnits(RowHeight);
    }

    // Cards span the full width, so a resize must repaint every row, not just the newly exposed strip.
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ItemHeight = LogicalToDeviceUnits(RowHeight);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        var g = e.Graphics;
        using (var background = new SolidBrush(_palette.Window)) g.FillRectangle(background, e.Bounds);
        if (e.Index < 0 || e.Index >= Items.Count || Items[e.Index] is not Person person) return;

        g.SmoothingMode = SmoothingMode.AntiAlias;
        int gap = LogicalToDeviceUnits(3), side = LogicalToDeviceUnits(16), pad = LogicalToDeviceUnits(14);
        var card = new Rectangle(e.Bounds.X + side, e.Bounds.Y + gap, e.Bounds.Width - 2 * side - 1, e.Bounds.Height - 2 * gap);
        bool highlighted = e.Index == _hover || (Focused && (e.State & DrawItemState.Selected) != 0);

        var next = BirthdayCalendar.NextOccurrence(person, _today);
        int days = next.DayNumber - _today.DayNumber;
        int? age = BirthdayCalendar.AgeOn(person, next);
        bool round = BirthdayCalendar.IsRoundBirthday(age);

        // Round birthdays get a soft accent glow on the border instead of the plain hairline.
        var border = round ? Blend(_palette.Border, _palette.Accent, 0.5f) : _palette.Border;
        using (var path = Shapes.RoundedRect(card, LogicalToDeviceUnits(9)))
        using (var fill = new SolidBrush(MonthTint.For(person.Month, _palette, hover: highlighted)))
        using (var pen = new Pen(border, round ? LogicalToDeviceUnits(3) / 2f : 1f))
        {
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }

        string status = Wording.Status(days, age);
        var statusFont = days == 0 ? Theme.BodyBold : Theme.Small;
        var statusColor = days == 0 ? _palette.Accent : _palette.SubtleText;
        int statusWidth = TextRenderer.MeasureText(g, status, statusFont, Size.Empty, StatusFlags).Width;

        int textWidth = card.Width - 3 * pad - statusWidth;
        int half = card.Height / 2;
        var nameRect = new Rectangle(card.X + pad, card.Y, textWidth, half + LogicalToDeviceUnits(1));
        var detailRect = new Rectangle(card.X + pad, card.Y + half, textWidth, half);
        var statusRect = new Rectangle(card.Right - pad - statusWidth, card.Y, statusWidth, card.Height);
        string detail = Wording.Detail(next, CultureInfo.CurrentCulture);

        TextRenderer.DrawText(g, person.Name, Theme.BodyBold, nameRect, _palette.Text, TextFlags | TextFormatFlags.Bottom);
        TextRenderer.DrawText(g, detail, Theme.Small, detailRect, _palette.SubtleText, TextFlags | TextFormatFlags.Top);

        if (round && days != 0)
        {
            // "40 in 12 days": the age picks up the accent, the rest stays quiet.
            string ageText = age!.Value.ToString(CultureInfo.CurrentCulture);
            int ageWidth = TextRenderer.MeasureText(g, ageText, statusFont, Size.Empty, StatusFlags).Width;
            var ageRect = statusRect with { Width = ageWidth };
            var restRect = new Rectangle(statusRect.X + ageWidth, statusRect.Y, statusRect.Width - ageWidth, statusRect.Height);
            TextRenderer.DrawText(g, ageText, statusFont, ageRect, _palette.Accent, StatusFlags | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, status[ageText.Length..], statusFont, restRect, statusColor, StatusFlags | TextFormatFlags.VerticalCenter);
        }
        else
        {
            TextRenderer.DrawText(g, status, statusFont, statusRect, statusColor, StatusFlags | TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }
    }

    // No padding, so a status split into accent + plain parts lines up exactly with an unsplit one.
    const TextFormatFlags StatusFlags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

    static Color Blend(Color a, Color b, float amount) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * amount), (int)(a.G + (b.G - a.G) * amount), (int)(a.B + (b.B - a.B) * amount));

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        SetHover(ItemAt(e.Location));
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        SetHover(-1);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        int index = ItemAt(e.Location);
        if (e.Button == MouseButtons.Left && index >= 0 && Items[index] is Person person) PersonActivated?.Invoke(this, person);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Enter && SelectedItem is Person person)
        {
            PersonActivated?.Invoke(this, person);
            e.Handled = true;
        }
    }

    int ItemAt(Point point)
    {
        int index = IndexFromPoint(point);
        return index >= 0 && index < Items.Count && GetItemRectangle(index).Contains(point) ? index : -1;
    }

    void SetHover(int index)
    {
        if (index == _hover) return;
        if (_hover >= 0 && _hover < Items.Count) Invalidate(GetItemRectangle(_hover));
        _hover = index;
        if (_hover >= 0 && _hover < Items.Count) Invalidate(GetItemRectangle(_hover));
    }
}
