namespace BirthdayBuddy.Ui;

/// <summary>How a control is coloured by <see cref="Widgets.ApplyPalette"/>; stored in Control.Tag.</summary>
public enum Role { Default, Subtle, Error, Accent, Border, PrimaryButton, SecondaryButton, DangerButton }

public static class Widgets
{
    public static Button Button(string text, Role role = Role.SecondaryButton) => new()
    {
        Text = text,
        Tag = role,
        AutoSize = true,
        FlatStyle = FlatStyle.Flat,
        Font = Theme.Body,
        Padding = new Padding(10, 2, 10, 2),
        Cursor = Cursors.Hand,
        UseVisualStyleBackColor = false,
    };

    public static Label Label(string text = "", Role role = Role.Default, Font? font = null) => new()
    {
        Text = text,
        Tag = role,
        AutoSize = true,
        Font = font ?? Theme.Body,
        UseMnemonic = false,
    };

    /// <summary>Colours <paramref name="root"/> and its children by type and <see cref="Role"/>.</summary>
    public static void ApplyPalette(Control root, Palette p)
    {
        var role = root.Tag as Role? ?? Role.Default;
        switch (root)
        {
            case Button button:
                StyleButton(button, role, p);
                break;
            case PeopleList list:
                list.ApplyPalette(p);
                break;
            case TextBox or ComboBox or NumericUpDown:
                root.BackColor = p.Surface;
                root.ForeColor = p.Text;
                return; // don't recurse into their internal parts
            default:
                root.BackColor = role == Role.Border ? p.Border : p.Window;
                root.ForeColor = role switch
                {
                    Role.Subtle => p.SubtleText,
                    Role.Error => p.Danger,
                    Role.Accent => p.Accent,
                    _ => p.Text,
                };
                break;
        }

        foreach (Control child in root.Controls) ApplyPalette(child, p);
    }

    static void StyleButton(Button b, Role role, Palette p)
    {
        b.FlatAppearance.BorderSize = role == Role.SecondaryButton ? 1 : 0;
        b.FlatAppearance.BorderColor = p.Border;
        (b.BackColor, b.ForeColor) = role switch
        {
            Role.PrimaryButton => (p.Accent, p.AccentText),
            Role.DangerButton => (p.Window, p.Danger),
            _ => (p.Surface, p.Text),
        };
        b.FlatAppearance.MouseOverBackColor = role == Role.PrimaryButton ? ControlPaint.Light(p.Accent, 0.25f) : p.SurfaceHover;
        b.FlatAppearance.MouseDownBackColor = b.FlatAppearance.MouseOverBackColor;
    }
}
