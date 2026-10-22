namespace BirthdayBuddy.Ui;

public static class AppIcon
{
    /// <summary>Loads the embedded cake icon at the closest available size.</summary>
    public static Icon Load(Size size)
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("BirthdayBuddy.app.ico")
                           ?? throw new InvalidOperationException("app.ico is not embedded.");
        return new Icon(stream, size);
    }
}
