namespace BirthdayBuddy.Ui;

/// <param name="PersonSlots">One rectangle per person popup, bottom to top.</param>
/// <param name="MoreSlot">Where the "+N more" popup goes, or null when everyone fits.</param>
public sealed record PopupPlan(IReadOnlyList<Rectangle> PersonSlots, Rectangle? MoreSlot, int MoreCount);

/// <summary>Size and position of the celebration popups. Pure: no screen access.</summary>
public static class PopupLayout
{
    const int MinWidth = 320, MinHeight = 128, Margin = 12, Gap = 8;

    /// <summary>1/6 of the screen width and 1/8 of its height, but never below 320×128 at 100% scaling.</summary>
    public static Size PopupSize(Size screen, float scale) => new(
        Math.Max((int)Math.Round(screen.Width / 6.0), (int)Math.Round(MinWidth * scale)),
        Math.Max((int)Math.Round(screen.Height / 8.0), (int)Math.Round(MinHeight * scale)));

    /// <summary>Stacks popups upward from the bottom-right of the working area (above the taskbar).</summary>
    public static PopupPlan Plan(Rectangle workingArea, Size popup, float scale, int people)
    {
        int margin = (int)Math.Round(Margin * scale), gap = (int)Math.Round(Gap * scale);
        // At least two slots, so an overflowing stack always shows one person next to the "+N more" popup.
        int capacity = Math.Max(2, (workingArea.Height - 2 * margin + gap) / (popup.Height + gap));

        Rectangle Slot(int i) => new(
            workingArea.Right - margin - popup.Width,
            workingArea.Bottom - margin - popup.Height - i * (popup.Height + gap),
            popup.Width, popup.Height);

        if (people <= capacity)
            return new PopupPlan(Enumerable.Range(0, people).Select(Slot).ToList(), null, 0);

        return new PopupPlan(Enumerable.Range(0, capacity - 1).Select(Slot).ToList(), Slot(capacity - 1), people - (capacity - 1));
    }
}
