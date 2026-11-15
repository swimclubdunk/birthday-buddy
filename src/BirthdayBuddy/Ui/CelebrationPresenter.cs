using System.Runtime.InteropServices;
using BirthdayBuddy.Core;

namespace BirthdayBuddy.Ui;

/// <summary>
/// Turns <see cref="CelebrationLoop.StackChanged"/> into popup windows on the primary screen: opens, restacks and
/// slides out popups, and reports slide-in, pointer and acknowledge events back to the loop.
/// </summary>
/// <param name="screen">Primary screen bounds, working area and display scale; tests pass fixed values.</param>
public sealed class CelebrationPresenter : IDisposable
{
    readonly CelebrationLoop _loop;
    readonly Library _library;
    readonly Func<DateOnly> _today;
    readonly Action _openMainWindow;
    readonly TimeProvider _time;
    readonly Func<(Rectangle Bounds, Rectangle WorkingArea, float Scale)> _screen;
    readonly List<CelebrationPopup> _people = [];
    readonly HashSet<CelebrationPopup> _pointerInside = [];
    CelebrationPopup? _more;

    public CelebrationPresenter(CelebrationLoop loop, Library library, Func<DateOnly> today, Action openMainWindow,
        TimeProvider time, Func<(Rectangle Bounds, Rectangle WorkingArea, float Scale)>? screen = null)
    {
        _loop = loop;
        _library = library;
        _today = today;
        _openMainWindow = openMainWindow;
        _time = time;
        _screen = screen ?? PrimaryScreen;
        _loop.StackChanged += OnStackChanged;
    }

    /// <summary>Open popups that aren't leaving: person popups bottom to top, then the "+N more" popup.</summary>
    public IReadOnlyList<CelebrationPopup> Popups => _more is null ? _people.ToList() : [.. _people, _more];

    void OnStackChanged(object? sender, IReadOnlyList<Celebration> stack)
    {
        // Recomputed every time: resolution, scaling or the taskbar may have moved since the last appearance.
        var (bounds, workingArea, scale) = _screen();
        var size = PopupLayout.PopupSize(bounds.Size, scale);
        var plan = PopupLayout.Plan(workingArea, size, scale, stack.Count);
        var shown = stack.Take(plan.PersonSlots.Count).ToList();

        foreach (var gone in _people.Where(p => !shown.Any(c => c.PersonId == p.PersonId)).ToList())
        {
            _people.Remove(gone);
            Leave(gone);
        }

        var ordered = new List<CelebrationPopup>(shown.Count);
        bool reportSlideIn = true;
        for (int i = 0; i < shown.Count; i++)
        {
            var slot = plan.PersonSlots[i];
            var popup = _people.Find(p => p.PersonId == shown[i].PersonId);
            if (popup is not null)
            {
                popup.MoveTo(slot);
            }
            else
            {
                popup = CelebrationPopup.ForPerson(shown[i], _time);
                Hook(popup);
                popup.Acknowledged += OnAcknowledged;
                if (reportSlideIn)
                {
                    // All new popups slide in together; the first one speaks for the stack.
                    popup.SlideInFinished += (_, _) => _loop.SlideInFinished();
                    reportSlideIn = false;
                }
                popup.SlideIn(slot);
            }
            ordered.Add(popup);
        }
        _people.Clear();
        _people.AddRange(ordered);

        if (plan.MoreSlot is Rectangle moreSlot)
        {
            if (_more is null)
            {
                _more = CelebrationPopup.ForMore(plan.MoreCount, _time);
                Hook(_more);
                _more.MoreClicked += (_, _) => _openMainWindow();
                _more.SlideIn(moreSlot);
            }
            else
            {
                _more.SetMoreCount(plan.MoreCount);
                _more.MoveTo(moreSlot);
            }
        }
        else if (_more is not null)
        {
            Leave(_more);
            _more = null;
        }
    }

    void OnAcknowledged(object? sender, EventArgs e)
    {
        if (sender is not CelebrationPopup { PersonId: Guid id }) return;
        _library.Acknowledge(id, _today());
        _loop.Acknowledge(id);
    }

    void Hook(CelebrationPopup popup)
    {
        popup.PointerEntered += (_, _) =>
        {
            if (_pointerInside.Add(popup) && _pointerInside.Count == 1) _loop.PointerEntered();
        };
        popup.PointerLeft += (_, _) => ForgetPointer(popup);
    }

    void ForgetPointer(CelebrationPopup popup)
    {
        if (_pointerInside.Remove(popup) && _pointerInside.Count == 0) _loop.PointerLeft();
    }

    void Leave(CelebrationPopup popup)
    {
        ForgetPointer(popup); // a popup that slides out from under the pointer no longer holds the stack
        popup.SlideOut();
    }

    static (Rectangle, Rectangle, float) PrimaryScreen()
    {
        var screen = Screen.PrimaryScreen ?? Screen.AllScreens[0];
        float scale = 1f;
        try
        {
            var monitor = MonitorFromPoint(new Point(screen.Bounds.Left + 1, screen.Bounds.Top + 1), MonitorDefaultToPrimary);
            if (GetDpiForMonitor(monitor, MdtEffectiveDpi, out uint dpiX, out _) == 0) scale = dpiX / 96f;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            // Older Windows: assume 100%.
        }
        return (screen.Bounds, screen.WorkingArea, scale);
    }

    public void Dispose()
    {
        _loop.StackChanged -= OnStackChanged;
        foreach (var popup in Popups) popup.Dispose();
        _people.Clear();
        _more = null;
    }

    const uint MonitorDefaultToPrimary = 1;
    const int MdtEffectiveDpi = 0;

    [DllImport("user32.dll")]
    static extern IntPtr MonitorFromPoint(Point pt, uint flags);

    [DllImport("shcore.dll")]
    static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);
}
