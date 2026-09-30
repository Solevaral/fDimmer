namespace fDimmer.UI.Controls;

/// <summary>Основа собственных контролов: двойная буферизация, фон родителя, масштаб DPI.</summary>
internal abstract class ThemedControl : Control
{
    protected ThemedControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
                 | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw
                 | ControlStyles.UserPaint, true);
        ForeColor = Theme.Text;
    }

    /// <summary>Множитель для толщин и отступов, заданных в пикселях при 96 DPI.</summary>
    protected float S => DeviceDpi / 96f;

    protected bool Hovered { get; private set; }

    protected override void OnMouseEnter(EventArgs e)
    {
        Hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        Hovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaintBackground(PaintEventArgs e) =>
        e.Graphics.Clear(Parent?.BackColor ?? Theme.Background);

    protected static void Prepare(Graphics g)
    {
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
    }

    protected static void DrawCentered(Graphics g, string text, Font font, Color color, RectangleF area)
    {
        TextRenderer.DrawText(g, text, font, Rectangle.Round(area), color,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter
            | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}
