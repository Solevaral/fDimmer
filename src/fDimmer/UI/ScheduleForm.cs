using fDimmer.Core;

namespace fDimmer.UI;

/// <summary>
/// Редактор расписания: список точек «время → яркость». Изменения сразу уходят
/// в настройки и перезапускают планировщик.
/// </summary>
internal sealed class ScheduleForm : Form
{
    private readonly Settings _settings;

    private readonly CheckBox _enabled = new();

    // Свой список с отрисовкой: ListView и DateTimePicker не перекрашиваются в тёмную тему.
    private readonly ListBox _list = new();
    private readonly NumericUpDown _hours = new();
    private readonly NumericUpDown _minutes = new();
    private readonly NumericUpDown _brightness = new();

    private ScheduleEntry? _active;
    private bool _loading;

    public ScheduleForm(Settings settings)
    {
        _settings = settings;

        Text = Strings.ScheduleTitle;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = Theme.Font(9.5f);
        Icon = AppIcon.Default;
        Theme.ApplyWindowChrome(this);

        BuildLayout();
        Theme.ApplyToStandardControls(this);
        LoadEntries();
    }

    /// <summary>Расписание изменилось — планировщик должен перечитать его.</summary>
    public event EventHandler? ScheduleChanged;

    private void BuildLayout()
    {
        var y = 16;

        _enabled.SetBounds(16, y, 440, 24);
        _enabled.Text = Strings.ScheduleUseIt;
        _enabled.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            _settings.ScheduleEnabled = _enabled.Checked;
            _list.Invalidate();
            Commit();
        };
        Controls.Add(_enabled);
        y += 32;

        Controls.Add(new Label
        {
            Text = Strings.ScheduleHint,
            AutoSize = true,
            Left = 16,
            Top = y,
            ForeColor = Theme.TextDim,
        });
        y += 46;

        _list.SetBounds(16, y, 444, 200);
        _list.DrawMode = DrawMode.OwnerDrawFixed;
        _list.ItemHeight = 34;
        _list.BorderStyle = BorderStyle.None;
        _list.BackColor = Theme.Surface;
        _list.IntegralHeight = false;
        _list.DrawItem += DrawEntry;
        _list.SelectedIndexChanged += OnSelectionChanged;
        Controls.Add(_list);
        y += 214;

        _hours.SetBounds(16, y, 50, 26);
        _hours.Maximum = 23;
        _hours.Value = 21;
        Controls.Add(_hours);

        Controls.Add(new Label { Text = ":", AutoSize = true, Left = 69, Top = y + 3 });

        _minutes.SetBounds(80, y, 50, 26);
        _minutes.Maximum = 59;
        _minutes.Increment = 5;
        Controls.Add(_minutes);

        _brightness.SetBounds(146, y, 56, 26);
        _brightness.Minimum = Settings.HardFloor;
        _brightness.Maximum = 100;
        _brightness.Value = 60;
        Controls.Add(_brightness);

        Controls.Add(new Label { Text = "%", AutoSize = true, Left = 205, Top = y + 3 });

        var add = new Button { Text = Strings.ScheduleAdd };
        add.SetBounds(228, y - 1, 148, 30);
        add.Click += (_, _) => AddOrUpdate();
        Controls.Add(add);

        var remove = new Button { Text = Strings.ScheduleRemove };
        remove.SetBounds(382, y - 1, 78, 30);
        remove.Click += (_, _) => RemoveSelected();
        Controls.Add(remove);
        y += 46;

        var close = new Button { Text = Strings.Close, DialogResult = DialogResult.OK };
        close.SetBounds(360, y, 100, 32);
        close.Click += (_, _) => Close();
        Controls.Add(close);
        AcceptButton = close;

        ClientSize = new Size(476, y + 48);
    }

    private void DrawEntry(object? sender, DrawItemEventArgs e)
    {
        var g = e.Graphics;
        var selected = (e.State & DrawItemState.Selected) != 0;

        using (var back = new SolidBrush(selected ? Theme.SurfaceHover : Theme.Surface))
        {
            g.FillRectangle(back, e.Bounds);
        }

        if (e.Index < 0 || _list.Items[e.Index] is not ScheduleEntry entry) return;

        if (selected)
        {
            using var mark = new SolidBrush(Theme.Accent);
            g.FillRectangle(mark, e.Bounds.X, e.Bounds.Y + 6, 3, e.Bounds.Height - 12);
        }

        // Пока расписание выключено, точки показываются приглушённо.
        var on = _settings.ScheduleEnabled;
        var textColor = on ? Theme.Text : Theme.TextDim;
        var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

        using var timeFont = Theme.SemiBold(10.5f);
        TextRenderer.DrawText(g, entry.Time.ToString("HH\\:mm"), timeFont,
            new Rectangle(e.Bounds.X + 16, e.Bounds.Y, 70, e.Bounds.Height), textColor, flags);

        var value = entry.Brightness >= 100 ? Strings.ScheduleOffValue : $"{entry.Brightness}%";
        TextRenderer.DrawText(g, value, _list.Font,
            new Rectangle(e.Bounds.X + 96, e.Bounds.Y, 200, e.Bounds.Height), textColor, flags);

        if (on && entry.SameAs(_active))
        {
            TextRenderer.DrawText(g, Strings.ScheduleActiveNow, _list.Font,
                new Rectangle(e.Bounds.X, e.Bounds.Y, e.Bounds.Width - 16, e.Bounds.Height),
                Theme.AccentCyan, flags | TextFormatFlags.Right);
        }
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (_loading || _list.SelectedItem is not ScheduleEntry entry) return;

        _hours.Value = entry.Time.Hour;
        _minutes.Value = entry.Time.Minute;
        _brightness.Value = Math.Clamp(entry.Brightness, _brightness.Minimum, _brightness.Maximum);
    }

    private void AddOrUpdate()
    {
        var time = new TimeOnly((int)_hours.Value, (int)_minutes.Value);
        var brightness = (int)_brightness.Value;

        // Одна точка на момент времени: повторное добавление правит существующую.
        var existing = _settings.Schedule.FirstOrDefault(e => e.Time == time);
        if (existing is not null) existing.Brightness = brightness;
        else _settings.Schedule.Add(new ScheduleEntry { Time = time, Brightness = brightness });

        _settings.Schedule = [.. _settings.Schedule.OrderBy(e => e.Time)];
        Commit();
        LoadEntries();
    }

    private void RemoveSelected()
    {
        if (_list.SelectedItem is not ScheduleEntry entry) return;

        _settings.Schedule.Remove(entry);
        Commit();
        LoadEntries();
    }

    private void LoadEntries()
    {
        _loading = true;
        try
        {
            _enabled.Checked = _settings.ScheduleEnabled;
            _active = Scheduler.Resolve(_settings.Schedule, TimeOnly.FromDateTime(DateTime.Now));

            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var entry in _settings.Schedule.OrderBy(e => e.Time)) _list.Items.Add(entry);
            _list.EndUpdate();
        }
        finally
        {
            _loading = false;
        }
    }

    private void Commit()
    {
        _settings.Save();
        ScheduleChanged?.Invoke(this, EventArgs.Empty);
    }
}
