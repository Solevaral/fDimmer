using fDimmer.Interop;

namespace fDimmer.UI;

/// <summary>
/// Крупный номер в углу каждого монитора на пару секунд — как кнопка «Определить»
/// в параметрах дисплея Windows. Помогает понять, какой экран на карте какой.
/// </summary>
internal sealed class IdentifyForm : Form
{
    private const int ShowMilliseconds = 2500;

    private readonly string _number;
    private readonly Rectangle _screen;

    private IdentifyForm(int number, Rectangle screen)
    {
        _number = number.ToString();
        _screen = screen;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        TopMost = true;
        BackColor = Theme.Background;
        DoubleBuffered = true;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW
                          | NativeMethods.WS_EX_TOPMOST | NativeMethods.WS_EX_TRANSPARENT;
            return cp;
        }
    }

    /// <summary>Показывает номера на всех мониторах. Номера совпадают с картой в главном окне.</summary>
    public static void ShowAll(IEnumerable<(int Number, Rectangle Bounds)> screens)
    {
        foreach (var (number, bounds) in screens)
        {
            var form = new IdentifyForm(number, bounds);
            form.Show();

            var timer = new System.Windows.Forms.Timer { Interval = ShowMilliseconds };
            timer.Tick += (_, _) =>
            {
                timer.Dispose();
                form.Close();
                form.Dispose();
            };
            timer.Start();
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        // Размер и позиция — в физических пикселях конкретного монитора.
        var size = (int)(160 * NativeMethods.GetDpiForWindow(Handle) / 96.0);
        var margin = size / 4;
        NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST,
            _screen.Left + margin, _screen.Top + margin, size, size,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);

        using var path = Theme.Rounded(new RectangleF(0, 0, size, size), size / 6f);
        Region = new Region(path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var rect = new RectangleF(0, 0, Width, Height);
        using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(rect,
                   Theme.Accent, Theme.Blend(Theme.Accent, Theme.AccentCyan, 0.7), 55f))
        {
            g.FillRectangle(brush, rect);
        }

        using var font = new Font(Theme.FontFamily, Height * 0.42f, FontStyle.Bold, GraphicsUnit.Pixel);
        TextRenderer.DrawText(g, _number, font, Rectangle.Round(rect), Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}
