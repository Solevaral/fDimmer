namespace fDimmer.UI.Controls;

/// <summary>Переключатель вкл/выкл в стиле Windows 11.</summary>
internal sealed class ToggleSwitch : ThemedControl
{
    private bool _checked;

    public ToggleSwitch()
    {
        Cursor = Cursors.Hand;
        Size = new Size(48, 26);
        TabStop = true;
    }

    /// <summary>Пользователь переключил — не срабатывает при программной установке.</summary>
    public event EventHandler? Toggled;

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value) return;
            _checked = value;
            Invalidate();
        }
    }

    protected override void OnClick(EventArgs e)
    {
        Checked = !Checked;
        Toggled?.Invoke(this, EventArgs.Empty);
        base.OnClick(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Space or Keys.Enter) OnClick(EventArgs.Empty);
        base.OnKeyDown(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Prepare(g);

        var track = new RectangleF(1, 1, Width - 2, Height - 2);
        using var path = Theme.Rounded(track, track.Height / 2);

        if (_checked)
        {
            using var fill = new System.Drawing.Drawing2D.LinearGradientBrush(track, Theme.Accent, Theme.AccentCyan, 0f);
            g.FillPath(fill, path);
        }
        else
        {
            using var fill = new SolidBrush(Hovered ? Theme.SurfaceHover : Theme.Surface);
            using var border = new Pen(Theme.TextDim, 1.2f * S);
            g.FillPath(fill, path);
            g.DrawPath(border, path);
        }

        var knob = track.Height - 8 * S;
        var x = _checked ? track.Right - 4 * S - knob : track.X + 4 * S;
        using var knobBrush = new SolidBrush(_checked ? Color.White : Theme.TextDim);
        g.FillEllipse(knobBrush, x, track.Y + 4 * S, knob, knob);

        if (Focused && ShowFocusCues)
        {
            using var focus = new Pen(Theme.AccentCyan, 1f);
            using var ring = Theme.Rounded(new RectangleF(0, 0, Width - 1, Height - 1), Height / 2f);
            g.DrawPath(focus, ring);
        }
    }
}
