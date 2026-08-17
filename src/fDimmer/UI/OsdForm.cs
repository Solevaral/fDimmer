using System.Drawing.Drawing2D;
using fDimmer.Interop;

namespace fDimmer.UI;

/// <summary>
/// Плашка с текущим уровнем яркости. Появляется внизу монитора под курсором и гаснет сама.
/// Глобальный движок затемняет и её тоже, но контраст при равномерном масштабировании
/// каналов сохраняется, поэтому плашка остаётся читаемой.
/// </summary>
internal sealed class OsdForm : Form
{
    private const int HoldMilliseconds = 1100;
    private const int FadeMilliseconds = 350;

    private readonly System.Windows.Forms.Timer _timer;
    private int _brightness = 100;
    private string _caption = string.Empty;
    private DateTime _shownAt;

    public OsdForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        TopMost = true;
        BackColor = Color.FromArgb(12, 12, 14);
        Opacity = 0;
        DoubleBuffered = true;

        _timer = new System.Windows.Forms.Timer { Interval = 30 };
        _timer.Tick += OnTick;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_LAYERED
                        | NativeMethods.WS_EX_TRANSPARENT
                        | NativeMethods.WS_EX_NOACTIVATE
                        | NativeMethods.WS_EX_TOOLWINDOW
                        | NativeMethods.WS_EX_TOPMOST;
            return cp;
        }
    }

    public void ShowLevel(int brightness, string caption)
    {
        _brightness = Math.Clamp(brightness, 0, 100);
        _caption = caption;
        _shownAt = DateTime.UtcNow;

        PositionOnCursorScreen();
        Opacity = 0.92;
        Invalidate();

        if (!Visible) Show();
        BringToTop();
        _timer.Start();
    }

    private void PositionOnCursorScreen()
    {
        var screen = Screen.FromPoint(Cursor.Position);
        var dpi = IsHandleCreated ? NativeMethods.GetDpiForWindow(Handle) : 96u;
        var scale = Math.Max(1.0, dpi / 96.0);

        var w = (int)(300 * scale);
        var h = (int)(84 * scale);
        var area = screen.WorkingArea;
        var x = area.Left + (area.Width - w) / 2;
        var y = area.Bottom - h - (int)(80 * scale);

        NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST, x, y, w, h,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }

    private void BringToTop() =>
        NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);

    private void OnTick(object? sender, EventArgs e)
    {
        var elapsed = (DateTime.UtcNow - _shownAt).TotalMilliseconds;
        if (elapsed < HoldMilliseconds) return;

        var fade = (elapsed - HoldMilliseconds) / FadeMilliseconds;
        if (fade >= 1)
        {
            _timer.Stop();
            Opacity = 0;
            Hide();
            return;
        }

        Opacity = 0.92 * (1 - fade);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.Clear(BackColor);

        var scale = Math.Max(1.0f, Width / 300f);
        var pad = 16 * scale;

        using var titleFont = new Font("Segoe UI", 10.5f * scale, FontStyle.Regular, GraphicsUnit.Point);
        using var valueFont = new Font("Segoe UI Semibold", 13f * scale, FontStyle.Regular, GraphicsUnit.Point);
        using var textBrush = new SolidBrush(Color.FromArgb(240, 240, 245));
        using var dimBrush = new SolidBrush(Color.FromArgb(150, 150, 160));

        g.DrawString(_caption, titleFont, dimBrush, pad, pad - 4 * scale);

        var value = $"{_brightness}%";
        var valueSize = g.MeasureString(value, valueFont);
        g.DrawString(value, valueFont, textBrush, Width - pad - valueSize.Width, pad - 6 * scale);

        // Полоса уровня.
        var barY = Height - pad - 10 * scale;
        var barH = 8 * scale;
        var barRect = new RectangleF(pad, barY, Width - 2 * pad, barH);

        using var track = new SolidBrush(Color.FromArgb(46, 46, 52));
        using var fill = new SolidBrush(Color.FromArgb(235, 235, 240));
        FillRounded(g, track, barRect, barH / 2);

        var filled = barRect.Width * (_brightness / 100f);
        if (filled > barH)
        {
            FillRounded(g, fill, barRect with { Width = filled }, barH / 2);
        }

        using var border = new Pen(Color.FromArgb(70, 70, 78), 1f * scale);
        g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
    }

    private static void FillRounded(Graphics g, Brush brush, RectangleF r, float radius)
    {
        using var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
