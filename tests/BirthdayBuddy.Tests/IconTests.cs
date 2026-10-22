using System.Drawing;
using BirthdayBuddy.Ui;

namespace BirthdayBuddy.Tests;

public class IconTests
{
    [Fact]
    public void Drawn_icon_has_requested_size_transparent_corners_and_solid_cake()
    {
        using var bmp = IconArt.Draw(32);
        Assert.Equal(32, bmp.Width);
        Assert.Equal(0, bmp.GetPixel(0, 0).A);
        Assert.Equal(255, bmp.GetPixel(16, 24).A);
    }

    [Fact]
    public void Ico_round_trips_through_System_Drawing()
    {
        var images = new[] { IconArt.Draw(16), IconArt.Draw(32) };
        using var stream = new MemoryStream();
        IcoWriter.Write(stream, images);

        stream.Position = 0;
        using (var small = new Icon(stream, 16, 16)) Assert.Equal(16, small.Width);

        stream.Position = 0;
        using var large = new Icon(stream, 32, 32);
        Assert.Equal(32, large.Width);
        using var bmp = large.ToBitmap();
        Assert.Equal(255, bmp.GetPixel(16, 24).A);

        foreach (var image in images) image.Dispose();
    }

    [Fact]
    public void Embedded_icon_loads_at_tray_size()
    {
        using var icon = AppIcon.Load(new Size(16, 16));
        Assert.Equal(16, icon.Width);
    }
}

