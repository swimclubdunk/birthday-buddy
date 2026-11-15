using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using BirthdayBuddy.Core;
using BirthdayBuddy.Ui;
using Microsoft.Extensions.Time.Testing;

namespace BirthdayBuddy.Tests;

public class CelebrationPopupTests
{
    static readonly Rectangle OffScreen = new(-32000, -32000, 320, 135);
    static readonly Celebration Sam = new(Guid.NewGuid(), "Sam", 34);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);

    static IEnumerable<Button> Buttons(Control root) =>
        root.Controls.Cast<Control>().SelectMany(c => (c is Button b ? [b] : Enumerable.Empty<Button>()).Concat(Buttons(c)));

    /// <summary>A real mouse click raises Click; PerformClick doesn't, because the button is deliberately not selectable.</summary>
    internal static void Click(Control control) =>
        typeof(Control).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, [EventArgs.Empty]);

    [Fact]
    public void Popup_never_activates_and_has_no_taskbar_button() => Sta.Run(() =>
    {
        using var popup = CelebrationPopup.ForPerson(Sam, new FakeTimeProvider());
        popup.SlideIn(OffScreen);
        popup.FinishAnimation();

        long exStyle = GetWindowLongPtr(popup.Handle, -20).ToInt64();
        Assert.Equal(0x08000000, exStyle & 0x08000000); // WS_EX_NOACTIVATE
        Assert.Equal(0x80, exStyle & 0x80);             // WS_EX_TOOLWINDOW
        Assert.Equal(0x8, exStyle & 0x8);               // WS_EX_TOPMOST
        Assert.NotSame(popup, Form.ActiveForm);
        Assert.True(popup.Visible);
    });

    [Fact]
    public void Person_popup_has_the_acknowledge_button_and_the_more_popup_has_none() => Sta.Run(() =>
    {
        using var person = CelebrationPopup.ForPerson(Sam, new FakeTimeProvider());
        using var more = CelebrationPopup.ForMore(3, new FakeTimeProvider());
        Assert.Equal("Okay I got it!", Assert.Single(Buttons(person)).Text);
        Assert.Empty(Buttons(more));
        Assert.Equal(Sam.PersonId, person.PersonId);
        Assert.Null(more.PersonId);
    });

    [Fact]
    public void Click_is_ignored_until_half_a_second_after_slide_in() => Sta.Run(() =>
    {
        var time = new FakeTimeProvider();
        using var popup = CelebrationPopup.ForPerson(Sam, time);
        int acknowledged = 0;
        popup.Acknowledged += (_, _) => acknowledged++;
        var button = Assert.Single(Buttons(popup));

        popup.SlideIn(OffScreen);
        Click(button);
        Assert.Equal(0, acknowledged);

        popup.FinishAnimation();
        time.Advance(TimeSpan.FromMilliseconds(499));
        Click(button);
        Assert.Equal(0, acknowledged);

        time.Advance(TimeSpan.FromMilliseconds(1));
        Click(button);
        Assert.Equal(1, acknowledged);
    });

    [Fact]
    public void Acknowledge_button_never_takes_focus() => Sta.Run(() =>
    {
        using var popup = CelebrationPopup.ForPerson(Sam, new FakeTimeProvider());
        Assert.False(Assert.Single(Buttons(popup)).CanSelect);
    });

    [Fact]
    public void Slide_in_ends_at_the_target_and_raises_SlideInFinished() => Sta.Run(() =>
    {
        using var popup = CelebrationPopup.ForPerson(Sam, new FakeTimeProvider());
        int finished = 0;
        popup.SlideInFinished += (_, _) => finished++;
        popup.SlideIn(OffScreen);
        popup.FinishAnimation();
        Assert.Equal(OffScreen, popup.Bounds);
        Assert.Equal(1, finished);
    });

    [Fact]
    public void Slide_out_closes_the_popup() => Sta.Run(() =>
    {
        var popup = CelebrationPopup.ForPerson(Sam, new FakeTimeProvider());
        popup.SlideIn(OffScreen);
        popup.FinishAnimation();
        popup.SlideOut();
        popup.FinishAnimation();
        Assert.True(popup.IsDisposed);
    });

    [Fact]
    public void More_popup_click_raises_MoreClicked() => Sta.Run(() =>
    {
        using var popup = CelebrationPopup.ForMore(3, new FakeTimeProvider());
        int clicked = 0;
        popup.MoreClicked += (_, _) => clicked++;
        Click(popup);
        Assert.Equal(1, clicked);
    });
}
