namespace fDimmer.UI.Controls;

/// <summary>Скруглённая кнопка в цветах темы.</summary>
internal sealed class FlatButton : ThemedControl
{
    private bool _pressed;

    public FlatButton()
    {
        Cursor = Cursors.Hand;
        Font = Theme.SemiBold(9.5f);
        TabStop = true;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        _pressed = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter) OnClick(EventArgs.Empty);
        base.OnKeyDown(e);
    }

    protected override void OnTextChanged(EventArgs e)
    {
        Invalidate();
        base.OnTextChanged(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Prepare(g);

        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Theme.Rounded(r, 9 * S);
        var fill = _pressed ? Theme.Border : Hovered ? Theme.SurfaceHover : Theme.Surface;

        using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
        using (var pen = new Pen(Focused && ShowFocusCues ? Theme.AccentCyan : Theme.Border, S)) g.DrawPath(pen, path);

        DrawCentered(g, Text, Font, Enabled ? Theme.Text : Theme.TextDim, r);
    }
}
