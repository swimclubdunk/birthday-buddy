using System.Drawing;
using BirthdayBuddy.Ui;

namespace BirthdayBuddy.Tests;

public class PopupLayoutTests
{
    static readonly Size Popup = new(320, 135);

    [Theory]
    [InlineData(1920, 1080, 1.0f, 320, 135)]
    [InlineData(1366, 768, 1.0f, 320, 128)]
    [InlineData(2560, 1440, 1.5f, 480, 192)]
    public void PopupSize(int width, int height, float scale, int expectedWidth, int expectedHeight) =>
        Assert.Equal(new Size(expectedWidth, expectedHeight), PopupLayout.PopupSize(new Size(width, height), scale));

    [Fact]
    public void First_slot_is_bottom_right_inside_the_margin() =>
        Assert.Equal(new Rectangle(1588, 893, 320, 135), PopupLayout.Plan(new Rectangle(0, 0, 1920, 1040), Popup, 1f, 1).PersonSlots[0]);

    [Fact]
    public void Slots_stack_upward_with_an_eight_pixel_gap() =>
        Assert.Equal(new Rectangle(1588, 750, 320, 135), PopupLayout.Plan(new Rectangle(0, 0, 1920, 1040), Popup, 1f, 2).PersonSlots[1]);

    [Fact]
    public void Taskbar_on_the_left_is_respected() =>
        Assert.Equal(new Rectangle(1588, 933, 320, 135), PopupLayout.Plan(new Rectangle(48, 0, 1872, 1080), Popup, 1f, 1).PersonSlots[0]);

    [Fact]
    public void Overflow_reserves_the_top_slot_for_more()
    {
        var plan = PopupLayout.Plan(new Rectangle(0, 0, 1920, 1040), Popup, 1f, 9);
        Assert.Equal(6, plan.PersonSlots.Count);
        Assert.Equal(new Rectangle(1588, 35, 320, 135), plan.MoreSlot);
        Assert.Equal(3, plan.MoreCount);
    }

    [Fact]
    public void Exactly_capacity_needs_no_more_slot()
    {
        var plan = PopupLayout.Plan(new Rectangle(0, 0, 1920, 1040), Popup, 1f, 7);
        Assert.Equal(7, plan.PersonSlots.Count);
        Assert.Null(plan.MoreSlot);
        Assert.Equal(0, plan.MoreCount);
    }

    [Fact]
    public void A_tiny_working_area_still_shows_one_person_and_the_more_popup()
    {
        var plan = PopupLayout.Plan(new Rectangle(0, 0, 400, 100), Popup, 1f, 3);
        Assert.Single(plan.PersonSlots);
        Assert.NotNull(plan.MoreSlot);
        Assert.Equal(2, plan.MoreCount);
    }
}
