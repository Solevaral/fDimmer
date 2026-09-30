namespace fDimmer.UI.Controls;

/// <summary>Несколько взаимоисключающих вариантов в одной плашке — как переключатель режима.</summary>
internal sealed class SegmentedControl : ThemedControl
{
    private string[] _items = [];
    private int _selected;
    private int _hover = -1;

    public SegmentedControl()
    {
        Cursor = Cursors.Hand;
        Font = Theme.SemiBold(9.5f);
    }

    /// <summary>Пользователь выбрал вариант — не срабатывает при программной установке.</summary>
    public event EventHandler? SelectionChanged;

    public string[] Items
    {
        get => _items;
        set { _items = value; Invalidate(); }
    }

    public int SelectedIndex
    {
        get => _selected;
        set
        {
            if (_selected == value) return;
            _selected = value;
            Invalidate();
        }
    }

    private RectangleF Segment(int index)
    {
        var pad = 3 * S;
        var width = (Width - pad * 2) / Math.Max(1, _items.Length);
        return new RectangleF(pad + width * index, pad, width, Height - pad * 2);
    }

    private int HitTest(Point p)
    {
        for (var i = 0; i < _items.Length; i++)
        {
            if (Segment(i).Contains(p)) return i;
        }
        return -1;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var hit = HitTest(e.Location);
        if (hit != _hover)
        {
            _hover = hit;
            Invalidate();
        }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = -1;
        base.OnMouseLeave(e);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        var hit = HitTest(e.Location);
        if (hit >= 0 && hit != _selected)
        {
            SelectedIndex = hit;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
        base.OnMouseClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Prepare(g);

        using (var back = Theme.Rounded(new RectangleF(0, 0, Width - 1, Height - 1), 10 * S))
        using (var fill = new SolidBrush(Theme.Surface))
        using (var border = new Pen(Theme.Border, S))
        {
            g.FillPath(fill, back);
            g.DrawPath(border, back);
        }

        for (var i = 0; i < _items.Length; i++)
        {
            var r = Segment(i);
            using var path = Theme.Rounded(r, 8 * S);

            if (i == _selected)
            {
                using var accent = new System.Drawing.Drawing2D.LinearGradientBrush(r,
                    Theme.Accent, Theme.Blend(Theme.Accent, Theme.AccentCyan, 0.55), 0f);
                g.FillPath(accent, path);
            }
            else if (i == _hover)
            {
                using var hover = new SolidBrush(Theme.SurfaceHover);
                g.FillPath(hover, path);
            }

            DrawCentered(g, _items[i], Font, i == _selected ? Color.White : Theme.TextDim, r);
        }
    }
}
