using System.Drawing.Drawing2D;
using System.Globalization;
using BirthdayBuddy.Core;

namespace BirthdayBuddy.Ui;

/// <summary>Owner-drawn list of soft cards: name and date on the left, status on the right.</summary>
public sealed class PeopleList : ListBox
{
    const int RowHeight = 62;
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
        int gap = LogicalToDeviceUnits(4), side = LogicalToDeviceUnits(16), pad = LogicalToDeviceUnits(14);
        var card = new Rectangle(e.Bounds.X + side, e.Bounds.Y + gap, e.Bounds.Width - 2 * side - 1, e.Bounds.Height - 2 * gap);
        bool highlighted = e.Index == _hover || (Focused && (e.State & DrawItemState.Selected) != 0);

        using (var path = Shapes.RoundedRect(card, LogicalToDeviceUnits(10)))
        using (var fill = new SolidBrush(highlighted ? _palette.SurfaceHover : _palette.Surface))
        using (var pen = new Pen(_palette.Border))
        {
            g.FillPath(fill, path);
            g.DrawPath(pen, path);
        }

        var next = BirthdayCalendar.NextOccurrence(person, _today);
        int days = next.DayNumber - _today.DayNumber;
        string status = Wording.Status(days);
        var statusFont = days == 0 ? Theme.BodyBold : Theme.Small;
        var statusColor = days == 0 ? _palette.Accent : _palette.SubtleText;
        int statusWidth = TextRenderer.MeasureText(g, status, statusFont).Width;

        int textWidth = card.Width - 3 * pad - statusWidth;
        int half = card.Height / 2;
        var nameRect = new Rectangle(card.X + pad, card.Y, textWidth, half + LogicalToDeviceUnits(2));
        var detailRect = new Rectangle(card.X + pad, card.Y + half + LogicalToDeviceUnits(1), textWidth, half);
        var statusRect = new Rectangle(card.Right - pad - statusWidth, card.Y, statusWidth, card.Height);
        string detail = Wording.Detail(next, BirthdayCalendar.AgeOn(person, next), CultureInfo.CurrentCulture);

        TextRenderer.DrawText(g, person.Name, Theme.BodyBold, nameRect, _palette.Text, TextFlags | TextFormatFlags.Bottom);
        TextRenderer.DrawText(g, detail, Theme.Small, detailRect, _palette.SubtleText, TextFlags | TextFormatFlags.Top);
        TextRenderer.DrawText(g, status, statusFont, statusRect, statusColor, TextFlags | TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
    }

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
