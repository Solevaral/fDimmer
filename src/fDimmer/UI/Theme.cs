using System.Drawing.Drawing2D;
using fDimmer.Interop;

namespace fDimmer.UI;

/// <summary>
/// Тёмная тема в цветах логотипа: глубокий индиго и голубое свечение. Приложение живёт ночью,
/// поэтому светлое окно поверх затемнённого экрана резало бы глаза.
/// </summary>
internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(17, 17, 34);
    public static readonly Color Surface = Color.FromArgb(27, 27, 51);
    public static readonly Color SurfaceHover = Color.FromArgb(38, 38, 68);
    public static readonly Color Border = Color.FromArgb(50, 50, 86);
    public static readonly Color Text = Color.FromArgb(236, 236, 248);
    public static readonly Color TextDim = Color.FromArgb(148, 148, 188);
    public static readonly Color Accent = Color.FromArgb(124, 92, 255);
    public static readonly Color AccentCyan = Color.FromArgb(80, 214, 240);
    public static readonly Color Warning = Color.FromArgb(242, 182, 92);
    public static readonly Color WarningSurface = Color.FromArgb(52, 40, 26);

    public const string FontFamily = "Segoe UI";

    public static Font Font(float size, FontStyle style = FontStyle.Regular) =>
        new(FontFamily, size, style, GraphicsUnit.Point);

    public static Font SemiBold(float size) => new("Segoe UI Semibold", size, FontStyle.Regular, GraphicsUnit.Point);

    public static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        radius = Math.Max(0.5f, Math.Min(radius, Math.Min(r.Width, r.Height) / 2));
        var d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static Color Blend(Color a, Color b, double t) => Color.FromArgb(
        (int)(a.R + (b.R - a.R) * t),
        (int)(a.G + (b.G - a.G) * t),
        (int)(a.B + (b.B - a.B) * t));

    public static Color Scale(Color c, double k) => Color.FromArgb(c.A,
        (int)Math.Clamp(c.R * k, 0, 255), (int)Math.Clamp(c.G * k, 0, 255), (int)Math.Clamp(c.B * k, 0, 255));

    /// <summary>
    /// Тёмная рамка окна под цвет фона — заголовок сливается с содержимым. На системах без
    /// поддержки атрибутов DWM вызовы просто ничего не делают.
    /// </summary>
    public static void ApplyWindowChrome(Form form)
    {
        form.BackColor = Background;
        form.ForeColor = Text;
        form.HandleCreated += (_, _) =>
        {
            var dark = 1;
            NativeMethods.DwmSetWindowAttribute(form.Handle, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));

            var caption = ColorTranslator.ToWin32(Background);
            NativeMethods.DwmSetWindowAttribute(form.Handle, NativeMethods.DWMWA_CAPTION_COLOR, ref caption, sizeof(int));

            var border = ColorTranslator.ToWin32(Border);
            NativeMethods.DwmSetWindowAttribute(form.Handle, NativeMethods.DWMWA_BORDER_COLOR, ref border, sizeof(int));
        };
    }

    /// <summary>Перекрашивает стандартные контролы WinForms под тёмную тему.</summary>
    public static void ApplyToStandardControls(Control root)
    {
        foreach (Control control in root.Controls)
        {
            switch (control)
            {
                case NumericUpDown or TextBox or ListView:
                    control.BackColor = Surface;
                    control.ForeColor = Text;
                    if (control is NumericUpDown number) number.BorderStyle = BorderStyle.FixedSingle;
                    if (control is ListView list) list.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case ComboBox combo:
                    combo.FlatStyle = FlatStyle.Flat;
                    combo.BackColor = Surface;
                    combo.ForeColor = Text;
                    break;

                case Button button:
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderColor = Border;
                    button.FlatAppearance.MouseOverBackColor = SurfaceHover;
                    button.BackColor = Surface;
                    button.ForeColor = Text;
                    button.Cursor = Cursors.Hand;
                    break;

                case CheckBox checkBox:
                    checkBox.ForeColor = Text;
                    checkBox.BackColor = Color.Transparent;
                    break;

                case Label label when label.ForeColor == SystemColors.GrayText:
                    label.ForeColor = TextDim;
                    break;

                case Label label when label.ForeColor == SystemColors.ControlText:
                    label.ForeColor = Text;
                    break;
            }

            if (control.HasChildren) ApplyToStandardControls(control);
        }
    }
}
