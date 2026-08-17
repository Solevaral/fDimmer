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
    private readonly ListView _list = new();
    private readonly DateTimePicker _time = new();
    private readonly NumericUpDown _brightness = new();

    private bool _loading;

    public ScheduleForm(Settings settings)
    {
        _settings = settings;

        Text = Strings.ScheduleTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9f);

        BuildLayout();
        LoadEntries();
    }

    /// <summary>Расписание изменилось — планировщик должен перечитать его.</summary>
    public event EventHandler? ScheduleChanged;

    private void BuildLayout()
    {
        var y = 14;

        _enabled.SetBounds(14, y, 400, 22);
        _enabled.Text = Strings.ScheduleUseIt;
        _enabled.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            _settings.ScheduleEnabled = _enabled.Checked;
            _list.Enabled = _enabled.Checked;
            Commit();
        };
        Controls.Add(_enabled);
        y += 28;

        var hint = new Label
        {
            Text = Strings.ScheduleHint,
            AutoSize = true,
            Left = 14,
            Top = y,
            ForeColor = SystemColors.GrayText,
        };
        Controls.Add(hint);
        y += 42;

        _list.SetBounds(14, y, 400, 190);
        _list.View = View.Details;
        _list.FullRowSelect = true;
        _list.MultiSelect = false;
        _list.HideSelection = false;
        _list.Columns.Add(Strings.ColumnTime, 90);
        _list.Columns.Add(Strings.ColumnBrightness, 285);
        _list.SelectedIndexChanged += OnSelectionChanged;
        Controls.Add(_list);
        y += 200;

        _time.SetBounds(14, y, 90, 24);
        _time.Format = DateTimePickerFormat.Custom;
        _time.CustomFormat = "HH:mm";
        _time.ShowUpDown = true;
        Controls.Add(_time);

        _brightness.SetBounds(114, y, 70, 24);
        _brightness.Minimum = Settings.HardFloor;
        _brightness.Maximum = 100;
        _brightness.Value = 60;
        Controls.Add(_brightness);

        Controls.Add(new Label { Text = "%", AutoSize = true, Left = 190, Top = y + 4 });

        var add = new Button { Text = Strings.ScheduleAdd };
        add.SetBounds(216, y - 1, 130, 27);
        add.Click += (_, _) => AddOrUpdate();
        Controls.Add(add);

        var remove = new Button { Text = Strings.ScheduleRemove };
        remove.SetBounds(352, y - 1, 62, 27);
        remove.Click += (_, _) => RemoveSelected();
        Controls.Add(remove);
        y += 38;

        var close = new Button { Text = Strings.Close, DialogResult = DialogResult.OK };
        close.SetBounds(324, y, 90, 28);
        close.Click += (_, _) => Close();
        Controls.Add(close);
        AcceptButton = close;

        ClientSize = new Size(430, y + 44);
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (_loading || _list.SelectedItems.Count == 0) return;
        if (_list.SelectedItems[0].Tag is not ScheduleEntry entry) return;

        _time.Value = DateTime.Today.Add(entry.Time.ToTimeSpan());
        _brightness.Value = Math.Clamp(entry.Brightness, _brightness.Minimum, _brightness.Maximum);
    }

    private void AddOrUpdate()
    {
        var time = TimeOnly.FromDateTime(_time.Value);
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
        if (_list.SelectedItems.Count == 0) return;
        if (_list.SelectedItems[0].Tag is not ScheduleEntry entry) return;

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
            _list.Enabled = _settings.ScheduleEnabled;

            var active = Scheduler.Resolve(_settings.Schedule, TimeOnly.FromDateTime(DateTime.Now));

            _list.Items.Clear();
            foreach (var entry in _settings.Schedule.OrderBy(e => e.Time))
            {
                var value = entry.Brightness >= 100 ? Strings.ScheduleOffValue : $"{entry.Brightness}%";
                if (entry.SameAs(active)) value += $"   — {Strings.ScheduleActiveNow}";

                _list.Items.Add(new ListViewItem([entry.Time.ToString("HH\\:mm"), value]) { Tag = entry });
            }
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
