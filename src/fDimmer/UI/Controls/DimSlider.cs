namespace fDimmer.UI.Controls;

/// <summary>
/// Ползунок яркости с шагом 1 %. Можно тянуть мышью, крутить колесом, двигать стрелками
/// (PageUp/PageDown — по 10 %). Умеет показывать отметку порога гаммы.
/// </summary>
internal sealed class DimSlider : ThemedControl
{
    private int _minimum = 5;
    private int _maximum = 100;
    private int _value = 100;
    private int? _marker;
    private bool _dragging;

    public DimSlider()
    {
        Cursor = Cursors.Hand;
        TabStop = true;
        Font = Theme.Font(8f);
    }

    /// <summary>Значение изменил пользователь — не срабатывает при программной установке.</summary>
    public event EventHandler? ValueChangedByUser;

    public int Minimum
    {
        get => _minimum;
        set { _minimum = value; _value = Math.Max(_value, value); Invalidate(); }
    }

    public int Maximum
    {
        get => _maximum;
        set { _maximum = value; Invalidate(); }
    }

    public int Value
    {
        get => _value;
        set
        {
            var clamped = Math.Clamp(value, _minimum, _maximum);
            if (clamped == _value) return;
            _value = clamped;
            Invalidate();
        }
    }

    /// <summary>Отметка на шкале (порог гаммы) и её подпись. null — без отметки.</summary>
    public int? Marker
    {
        get => _marker;
        set { _marker = value; Invalidate(); }
    }

    public string MarkerLabel { get; set; } = string.Empty;

    private float TrackLeft => 12 * S;
    private float TrackRight => Width - 12 * S;
    private float TrackY => 14 * S;

    private float XOf(int value) =>
        TrackLeft + (TrackRight - TrackLeft) * (value - _minimum) / Math.Max(1, _maximum - _minimum);

    private int ValueAt(int x)
    {
        var t = (x - TrackLeft) / Math.Max(1, TrackRight - TrackLeft);
        return (int)Math.Round(_minimum + t * (_maximum - _minimum));
    }

    private void SetByUser(int value)
    {
        var clamped = Math.Clamp(value, _minimum, _maximum);
        if (clamped == _value) return;
        _value = clamped;
        Invalidate();
        Update(); // перерисовать сразу, пока тянут — иначе ползунок отстаёт от курсора
        ValueChangedByUser?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && Enabled)
        {
            Focus();
            _dragging = true;
            Capture = true;
            SetByUser(ValueAt(e.X));
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging) SetByUser(ValueAt(e.X));
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        _dragging = false;
        Capture = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (Enabled) SetByUser(_value + Math.Sign(e.Delta));
        base.OnMouseWheel(e);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Left or Keys.Down: SetByUser(_value - 1); break;
            case Keys.Right or Keys.Up: SetByUser(_value + 1); break;
            case Keys.PageDown: SetByUser(_value - 10); break;
            case Keys.PageUp: SetByUser(_value + 10); break;
            case Keys.Home: SetByUser(_minimum); break;
            case Keys.End: SetByUser(_maximum); break;
        }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }

    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Prepare(g);

        var h = 8 * S;
        var track = new RectangleF(TrackLeft, TrackY - h / 2, TrackRight - TrackLeft, h);
        var x = XOf(_value);

        using (var path = Theme.Rounded(track, h / 2))
        using (var brush = new SolidBrush(Theme.Border))
        {
            g.FillPath(brush, path);
        }

        var filled = track with { Width = Math.Max(h, x - track.X) };
        using (var path = Theme.Rounded(filled, h / 2))
        using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                   new RectangleF(track.X, track.Y, track.Width + 1, track.Height),
                   Enabled ? Theme.Accent : Theme.TextDim,
                   Enabled ? Theme.AccentCyan : Theme.Border, 0f))
        {
            g.FillPath(brush, path);
        }

        if (_marker is { } marker && marker > _minimum && marker < _maximum)
        {
            var mx = XOf(marker);
            using var pen = new Pen(Theme.Warning, 2 * S);
            g.DrawLine(pen, mx, track.Y - 5 * S, mx, track.Bottom + 5 * S);

            if (MarkerLabel.Length > 0)
            {
                var size = TextRenderer.MeasureText(MarkerLabel, Font);
                var lx = Math.Clamp(mx - size.Width / 2f, 0, Width - size.Width);
                TextRenderer.DrawText(g, MarkerLabel, Font,
                    new Point((int)lx, (int)(track.Bottom + 7 * S)), Theme.Warning);
            }
        }

        var knob = (_dragging ? 22 : 20) * S;
        var knobRect = new RectangleF(x - knob / 2, TrackY - knob / 2, knob, knob);
        using (var glow = new SolidBrush(Color.FromArgb(Focused ? 90 : 50, Theme.AccentCyan)))
        {
            g.FillEllipse(glow, RectangleF.Inflate(knobRect, 4 * S, 4 * S));
        }
        using (var knobBrush = new SolidBrush(Enabled ? Color.White : Theme.TextDim))
        using (var ring = new Pen(Enabled ? Theme.Accent : Theme.Border, 2.5f * S))
        {
            g.FillEllipse(knobBrush, knobRect);
            g.DrawEllipse(ring, knobRect);
        }
    }
}
