using System.Drawing.Drawing2D;
using BirthdayBuddy.Core;

namespace BirthdayBuddy.Ui;

/// <summary>
/// One borderless, always-on-top celebration card that never takes focus: a person's birthday with an
/// "Okay I got it!" button, or the "+N more birthdays today" card. Slides in from and out to the right edge.
/// </summary>
public sealed class CelebrationPopup : Form
{
    /// <summary>The button ignores clicks this long after the slide-in ends, so a stray click can't acknowledge.</summary>
    public static readonly TimeSpan ClickGuard = TimeSpan.FromMilliseconds(500);

    const int WsExNoActivate = 0x08000000, WsExToolWindow = 0x80, WsExTopMost = 0x8;
    const int WmMouseActivate = 0x21, MaNoActivate = 3;

    enum Motion { None, In, Move, Out }

    readonly TimeProvider _time;
    readonly Celebration? _person;
    readonly Palette _palette = Theme.IsDarkMode() ? Theme.Dark : Theme.Light;
    readonly Button? _button;
    readonly System.Windows.Forms.Timer _frames = new() { Interval = 16 };
    readonly Confetti _confetti;
    int _moreCount;

    Motion _motion;
    Point _from, _to;
    long _motionStart, _lastFrame;
    DateTimeOffset? _armedAt;
    bool _leaving, _pointerInside;
    Font? _headlineFont, _ageFont, _buttonFont;

    public Guid? PersonId => _person?.PersonId;

    public event EventHandler? Acknowledged, MoreClicked, SlideInFinished, PointerEntered, PointerLeft;

    public static CelebrationPopup ForPerson(Celebration person, TimeProvider time) => new(person, 0, time);

    public static CelebrationPopup ForMore(int count, TimeProvider time) => new(null, count, time);

    CelebrationPopup(Celebration? person, int moreCount, TimeProvider time)
    {
        _person = person;
        _moreCount = moreCount;
        _time = time;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = _palette.Surface;
        Text = person is null ? Wording.MoreBirthdays(moreCount) : Wording.CelebrationHeadline(person.Name);
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        _confetti = new Confetti(new Size(320, 135), 26, person?.PersonId.GetHashCode() ?? moreCount);

        if (person is not null)
        {
            _button = new NoFocusButton
            {
                Text = Wording.AcknowledgeButton,
                Tag = Role.PrimaryButton,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false,
                UseMnemonic = false,
            };
            Widgets.ApplyPalette(_button, _palette);
            _button.Click += (_, _) =>
            {
                if (!_leaving && _armedAt is { } armed && _time.GetUtcNow() >= armed) Acknowledged?.Invoke(this, EventArgs.Empty);
            };
            HookPointer(_button);
            Controls.Add(_button);
        }
        else
        {
            Cursor = Cursors.Hand;
        }

        HookPointer(this);
        _frames.Tick += (_, _) => OnFrame();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExNoActivate | WsExToolWindow | WsExTopMost;
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmMouseActivate)
        {
            m.Result = MaNoActivate; // clicking must not steal focus from what the owner is typing in
            return;
        }
        base.WndProc(ref m);
    }

    /// <summary>Shows the popup just off the right edge and slides it to <paramref name="target"/>.</summary>
    public void SlideIn(Rectangle target)
    {
        Bounds = new Rectangle(target.Right + 16, target.Y, target.Width, target.Height);
        if (!Visible) Show();
        Animate(Motion.In, target.Location);
    }

    /// <summary>Moves to a new slot, e.g. when a popup below it closed.</summary>
    public void MoveTo(Rectangle target)
    {
        if (_leaving) return;
        Size = target.Size;
        if (_motion == Motion.In) _to = target.Location; // still sliding in: just aim elsewhere
        else Animate(Motion.Move, target.Location);
    }

    /// <summary>Slides off to the right, then closes and disposes itself.</summary>
    public void SlideOut()
    {
        if (_leaving || IsDisposed) return;
        _leaving = true;
        Animate(Motion.Out, new Point(Right + 16 + Width / 4, Top));
    }

    /// <summary>Jumps a running slide to its end (tests, and when the next step shouldn't wait).</summary>
    public void FinishAnimation()
    {
        if (_motion == Motion.None) return;
        Location = _to;
        Complete();
    }

    public void SetMoreCount(int count)
    {
        _moreCount = count;
        Text = Wording.MoreBirthdays(count);
        Invalidate();
    }

    void Animate(Motion motion, Point to)
    {
        _motion = motion;
        _from = Location;
        _to = to;
        _motionStart = Environment.TickCount64;
        _lastFrame = _motionStart;
        _frames.Start();
    }

    void Complete()
    {
        var finished = _motion;
        _motion = Motion.None;
        switch (finished)
        {
            case Motion.In:
                _armedAt = _time.GetUtcNow() + ClickGuard;
                SlideInFinished?.Invoke(this, EventArgs.Empty);
                break;
            case Motion.Out:
                _frames.Stop();
                Close();
                Dispose();
                break;
        }
    }

    void OnFrame()
    {
        if (IsDisposed) return;
        long now = Environment.TickCount64;
        _confetti.Step((now - _lastFrame) / 1000f);
        _lastFrame = now;

        if (_motion != Motion.None)
        {
            double duration = _motion switch { Motion.In => 280, Motion.Move => 160, _ => 220 };
            double t = Math.Min(1, (now - _motionStart) / duration);
            double eased = _motion == Motion.Out ? t * t : 1 - Math.Pow(1 - t, 3);
            Location = new Point(
                _from.X + (int)Math.Round((_to.X - _from.X) * eased),
                _from.Y + (int)Math.Round((_to.Y - _from.Y) * eased));
            if (t >= 1)
            {
                Complete();
                if (IsDisposed) return;
            }
        }
        Invalidate();
    }

    void HookPointer(Control control)
    {
        control.MouseEnter += (_, _) =>
        {
            if (_pointerInside) return;
            _pointerInside = true;
            PointerEntered?.Invoke(this, EventArgs.Empty);
        };
        control.MouseLeave += (_, _) =>
        {
            // Moving from the card onto its button is not leaving.
            if (!_pointerInside || (!IsDisposed && Bounds.Contains(Cursor.Position))) return;
            _pointerInside = false;
            PointerLeft?.Invoke(this, EventArgs.Empty);
        };
    }

    protected override void OnClick(EventArgs e)
    {
        if (_person is null && !_leaving) MoreClicked?.Invoke(this, EventArgs.Empty);
        base.OnClick(e);
    }

    float Unit => Math.Max(0.5f, Height / 135f);

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (Width <= 0 || Height <= 0) return;

        using (var path = Shapes.RoundedRect(new RectangleF(0, 0, Width, Height), 12 * Unit))
        {
            Region?.Dispose();
            Region = new Region(path);
        }
        _confetti.Resize(ClientSize);
        LayoutContent();
    }

    void LayoutContent()
    {
        float u = Unit;
        _headlineFont?.Dispose();
        _ageFont?.Dispose();
        _buttonFont?.Dispose();

        var headline = Text;
        var headlineRect = HeadlineRect();
        _headlineFont = FitFont(headline, "Segoe UI Semibold", (_person is null ? 19 : 21) * u, 12 * u, headlineRect.Width);
        _ageFont = new Font("Segoe UI Semibold", 15 * u, GraphicsUnit.Pixel);

        if (_button is null) return;
        _buttonFont = new Font("Segoe UI Semibold", 12 * u, GraphicsUnit.Pixel);
        _button.Font = _buttonFont;
        int buttonWidth = TextRenderer.MeasureText(_button.Text, _buttonFont).Width + (int)(26 * u);
        int buttonHeight = (int)(30 * u);
        _button.Bounds = new Rectangle(Width - (int)(12 * u) - buttonWidth, Height - (int)(12 * u) - buttonHeight, buttonWidth, buttonHeight);
    }

    Rectangle HeadlineRect()
    {
        float u = Unit;
        int trumpet = (int)(48 * u), pad = (int)(10 * u);
        return _person is null
            ? new Rectangle(pad, 0, Width - 2 * pad, Height)
            : new Rectangle(pad + trumpet, pad, Width - 2 * (pad + trumpet), (int)(40 * u));
    }

    static Font FitFont(string text, string family, float largest, float smallest, int width)
    {
        for (float size = largest; size > smallest; size -= 1)
        {
            var font = new Font(family, size, GraphicsUnit.Pixel);
            if (TextRenderer.MeasureText(text, font).Width <= width) return font;
            font.Dispose();
        }
        return new Font(family, smallest, GraphicsUnit.Pixel);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float u = Unit;

        // Card with an accent border.
        using (var background = new LinearGradientBrush(ClientRectangle, _palette.Surface, Blend(_palette.Surface, _palette.Accent, 0.12f), LinearGradientMode.Vertical))
            g.FillRectangle(background, ClientRectangle);
        _confetti.Draw(g);
        using (var border = new Pen(_palette.Accent, 2.5f * u))
        using (var path = Shapes.RoundedRect(new RectangleF(1.25f * u, 1.25f * u, Width - 2.5f * u, Height - 2.5f * u), 11 * u))
            g.DrawPath(border, path);

        const TextFormatFlags centred = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis
            | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;

        if (_person is null)
        {
            TextRenderer.DrawText(g, Text, _headlineFont, HeadlineRect(), _palette.Text, centred);
            return;
        }

        int trumpet = (int)(48 * u), pad = (int)(10 * u);
        TrumpetArt.Draw(g, new RectangleF(pad - 2 * u, pad - 4 * u, trumpet, trumpet), mirror: false, _palette.Accent);
        TrumpetArt.Draw(g, new RectangleF(Width - pad - trumpet + 2 * u, pad - 4 * u, trumpet, trumpet), mirror: true, _palette.Accent);

        TextRenderer.DrawText(g, Text, _headlineFont, HeadlineRect(), _palette.Text, centred);

        if (Wording.CelebrationAge(_person.Age) is string age)
        {
            bool round = BirthdayCalendar.IsRoundBirthday(_person.Age);
            var ageRect = new Rectangle(pad + trumpet, pad + (int)(40 * u), Width - 2 * (pad + trumpet), (int)(24 * u));
            TextRenderer.DrawText(g, age, _ageFont, ageRect, round ? _palette.Accent : _palette.SubtleText, centred);
        }
    }

    static Color Blend(Color under, Color over, float amount) => Color.FromArgb(
        (int)(under.R + (over.R - under.R) * amount),
        (int)(under.G + (over.G - under.G) * amount),
        (int)(under.B + (over.B - under.B) * amount));

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _frames.Dispose();
            _headlineFont?.Dispose();
            _ageFont?.Dispose();
            _buttonFont?.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>A button that never takes keyboard focus, so clicking it can't activate the popup.</summary>
    sealed class NoFocusButton : Button
    {
        public NoFocusButton() => SetStyle(ControlStyles.Selectable, false);
    }
}
