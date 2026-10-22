using System.Globalization;
using BirthdayBuddy.Core;

namespace BirthdayBuddy.Ui;

/// <summary>Shows Windows notifications through the tray icon (rendered as native toasts on Windows 10/11).</summary>
public sealed class Notifier
{
    readonly NotifyIcon _icon;

    public event EventHandler? Clicked;

    public Notifier(NotifyIcon icon)
    {
        _icon = icon;
        _icon.BalloonTipClicked += (_, _) => Clicked?.Invoke(this, EventArgs.Empty);
    }

    public void Show(IReadOnlyList<Reminder> reminders)
    {
        if (reminders.Count == 0) return;
        var (title, body) = Wording.Notification(reminders, CultureInfo.CurrentCulture);
        ShowMessage(title, body);
    }

    public void ShowMessage(string title, string body)
    {
        _icon.BalloonTipTitle = title;
        _icon.BalloonTipText = body;
        _icon.BalloonTipIcon = ToolTipIcon.None;
        _icon.ShowBalloonTip(10_000);
    }
}
