namespace fDimmer.UI.Controls;

/// <summary>Ряд быстрых уровней «100 % · 90 % · …»: один клик — и готово.</summary>
internal sealed class ChipBar : ThemedControl
{
    private int[] _values = [];
    private int? _current;
    private int _hover = -1;

    public ChipBar()
    {
        Cursor = Cursors.Hand;
        Font = Theme.SemiBold(9f);
    }

    public event EventHandler<int>? ChipClicked;

    public int[] Values
    {
        get => _values;
        set { _values = value; Invalidate(); }
    }

    /// <summary>Подсвечивается чип, совпадающий с текущим уровнем.</summary>
    public int? Current
    {
        get => _current;
        set
        {
            if (_current == value) return;
            _current = value;
            Invalidate();
        }
    }

    private RectangleF Chip(int index)
    {
        var gap = 6 * S;
        var width = (Width - gap * (_values.Length - 1)) / Math.Max(1, _values.Length);
        return new RectangleF(index * (width + gap), 0, width, Height - 1);
    }

    private int HitTest(Point p)
    {
        for (var i = 0; i < _values.Length; i++)
        {
            if (Chip(i).Contains(p)) return i;
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
        if (hit >= 0 && Enabled) ChipClicked?.Invoke(this, _values[hit]);
        base.OnMouseClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Prepare(g);

        for (var i = 0; i < _values.Length; i++)
        {
            var r = Chip(i);
            using var path = Theme.Rounded(r, r.Height / 2);
            var active = _current == _values[i];

            var fill = active ? Theme.Accent : i == _hover ? Theme.SurfaceHover : Theme.Surface;
            using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
            using (var pen = new Pen(active ? Theme.Accent : Theme.Border, S)) g.DrawPath(pen, path);

            DrawCentered(g, $"{_values[i]}%", Font, active ? Color.White : Theme.Text, r);
        }
    }
}
