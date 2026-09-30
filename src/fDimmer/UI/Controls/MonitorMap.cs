using System.Drawing.Drawing2D;

namespace fDimmer.UI.Controls;

/// <summary>Один монитор на карте.</summary>
internal sealed record MonitorTile(string Device, Rectangle Bounds, int Number, bool Primary)
{
    /// <summary>Уровень, который подписан на плитке.</summary>
    public int Level { get; set; } = 100;

    /// <summary>Насколько светлой рисовать плитку: при выключенном затемнении — 100.</summary>
    public int Preview { get; set; } = 100;
}

/// <summary>
/// Схема мониторов как в «Параметры → Дисплей»: прямоугольники в реальном взаимном
/// расположении, клик выбирает монитор. Плитка темнеет вместе с уровнем — видно сразу,
/// какой монитор насколько затемнён.
/// </summary>
internal sealed class MonitorMap : ThemedControl
{
    // «Картинка на экране» в плитке — цвета логотипа, чтобы затемнение было заметно.
    private static readonly Color ScreenTop = Color.FromArgb(96, 110, 220);
    private static readonly Color ScreenBottom = Color.FromArgb(70, 190, 225);

    private readonly List<MonitorTile> _tiles = [];
    private string? _selected;
    private string? _hover;

    public MonitorMap()
    {
        Cursor = Cursors.Hand;
        TabStop = true;
    }

    public event EventHandler<string>? MonitorClicked;

    public IReadOnlyList<MonitorTile> Tiles => _tiles;

    /// <summary>Выбран один монитор. В общем режиме подсвечиваются все — см. <see cref="AllSelected"/>.</summary>
    public string? SelectedDevice
    {
        get => _selected;
        set { _selected = value; Invalidate(); }
    }

    public bool AllSelected { get; set; }

    public void SetTiles(IEnumerable<MonitorTile> tiles)
    {
        _tiles.Clear();
        _tiles.AddRange(tiles);
        Invalidate();
    }

    /// <summary>Прямоугольники плиток в координатах контрола: вписываем общий охват экранов.</summary>
    private List<(MonitorTile Tile, RectangleF Rect)> TileRects()
    {
        var result = new List<(MonitorTile, RectangleF)>();
        if (_tiles.Count == 0) return result;

        var union = _tiles.Select(t => t.Bounds).Aggregate(Rectangle.Union);
        var pad = 18 * S;
        var scale = Math.Min((Width - pad * 2) / union.Width, (Height - pad * 2) / union.Height);
        var offsetX = (Width - union.Width * scale) / 2;
        var offsetY = (Height - union.Height * scale) / 2;
        var gap = 5 * S;

        foreach (var tile in _tiles)
        {
            var r = new RectangleF(
                offsetX + (tile.Bounds.X - union.X) * scale,
                offsetY + (tile.Bounds.Y - union.Y) * scale,
                tile.Bounds.Width * scale,
                tile.Bounds.Height * scale);
            result.Add((tile, RectangleF.Inflate(r, -gap / 2, -gap / 2)));
        }

        return result;
    }

    private string? HitTest(Point p) =>
        TileRects().FirstOrDefault(l => l.Rect.Contains(p)).Tile?.Device;

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
        _hover = null;
        base.OnMouseLeave(e);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && HitTest(e.Location) is { } device)
        {
            Focus();
            MonitorClicked?.Invoke(this, device);
        }
        base.OnMouseClick(e);
    }

    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Left or Keys.Right or Keys.Up or Keys.Down || base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Стрелки перебирают мониторы по порядку номеров.
        if (_tiles.Count > 0 && e.KeyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down)
        {
            var ordered = _tiles.OrderBy(t => t.Number).ToList();
            var index = ordered.FindIndex(t => t.Device == _selected);
            var step = e.KeyCode is Keys.Left or Keys.Up ? -1 : 1;
            var next = ordered[((index < 0 ? 0 : index + step) % ordered.Count + ordered.Count) % ordered.Count];
            MonitorClicked?.Invoke(this, next.Device);
        }
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }

    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        Prepare(g);

        using (var card = Theme.Rounded(new RectangleF(0, 0, Width - 1, Height - 1), 14 * S))
        using (var fill = new SolidBrush(Theme.Surface))
        using (var border = new Pen(Focused && ShowFocusCues ? Theme.AccentCyan : Theme.Border, S))
        {
            g.FillPath(fill, card);
            g.DrawPath(border, card);
        }

        foreach (var (tile, rect) in TileRects())
        {
            var selected = AllSelected || tile.Device == _selected;
            var k = tile.Preview / 100.0;
            var radius = 7 * S;

            using var path = Theme.Rounded(rect, radius);

            // Плитка — «экран»: его картинка темнеет ровно так же, как настоящий монитор.
            using (var screen = new LinearGradientBrush(rect,
                       Theme.Scale(ScreenTop, k), Theme.Scale(ScreenBottom, k), 55f))
            {
                g.FillPath(screen, path);
            }

            if (tile.Device == _hover && !selected)
            {
                using var hover = new SolidBrush(Color.FromArgb(30, Color.White));
                g.FillPath(hover, path);
            }

            if (selected)
            {
                using var glow = new Pen(Color.FromArgb(70, Theme.AccentCyan), 7 * S);
                g.DrawPath(glow, path);
                using var ring = new Pen(Color.White, 2.5f * S);
                g.DrawPath(ring, path);
            }
            else
            {
                using var ring = new Pen(Color.FromArgb(90, Color.White), S);
                g.DrawPath(ring, path);
            }

            // Номер и уровень — надписи, а не «картинка экрана», поэтому всегда читаемы.
            var numberSize = Math.Clamp(rect.Height / 3.2f / S, 12f, 30f);
            using var numberFont = Theme.Font(numberSize, FontStyle.Bold);
            using var levelFont = Theme.SemiBold(Math.Clamp(numberSize / 2.4f, 8.5f, 12f));

            var numberHeight = TextRenderer.MeasureText(tile.Number.ToString(), numberFont).Height;
            var levelHeight = TextRenderer.MeasureText("100%", levelFont).Height;
            var top = rect.Y + (rect.Height - numberHeight - levelHeight) / 2;

            DrawShadowed(g, tile.Number.ToString(), numberFont,
                new RectangleF(rect.X, top, rect.Width, numberHeight));
            DrawShadowed(g, $"{tile.Level}%", levelFont,
                new RectangleF(rect.X, top + numberHeight - 2 * S, rect.Width, levelHeight));

            if (tile.Primary)
            {
                // Точка в углу отмечает основной монитор.
                var dot = 6 * S;
                using var brush = new SolidBrush(Color.FromArgb(220, Color.White));
                g.FillEllipse(brush, rect.Right - dot - 7 * S, rect.Y + 7 * S, dot, dot);
            }
        }
    }

    private void DrawShadowed(Graphics g, string text, Font font, RectangleF area)
    {
        DrawCentered(g, text, font, Color.FromArgb(120, 0, 0, 0), area with { X = area.X + S, Y = area.Y + S });
        DrawCentered(g, text, font, Color.White, area);
    }
}
