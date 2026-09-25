using fDimmer.Core;

namespace fDimmer.UI;

/// <summary>Окно настроек. Все изменения применяются сразу, без кнопки «Применить».</summary>
internal sealed class SettingsForm : Form
{
    private readonly DimController _controller;
    private readonly Settings _settings;

    private readonly TrackBar _brightness = new();
    private readonly Label _brightnessValue = new();
    private readonly ComboBox _mode = new();
    private readonly List<(string Device, TrackBar Slider, Label Value)> _monitorSliders = [];
    private readonly Label _perMonitorWarning = new();
    private readonly ComboBox _language = new();
    private readonly NumericUpDown _minBrightness = new();
    private readonly NumericUpDown _wheelStep = new();
    private readonly NumericUpDown _ramp = new();
    private readonly CheckBox _showOsd = new();
    private readonly CheckBox _trayWheel = new();
    private readonly CheckBox _autoStart = new();

    private bool _loading;

    public SettingsForm(DimController controller)
    {
        _controller = controller;
        _settings = controller.Settings;

        Text = Strings.SettingsTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9f);
        Icon = AppIcon.Default;

        BuildLayout();
        LoadValues();

        _controller.StateChanged += OnControllerStateChanged;
    }

    /// <summary>Пользователь включил или выключил обработку колеса над треем.</summary>
    public event EventHandler<bool>? TrayWheelToggled;

    /// <summary>Выбран другой язык интерфейса.</summary>
    public event EventHandler<AppLanguage>? LanguageChanged;

    /// <summary>Нужно открыть редактор расписания.</summary>
    public event EventHandler? ScheduleRequested;

    private void BuildLayout()
    {
        var y = 14;

        Controls.Add(Section(Strings.SectionBrightness, ref y));

        _brightness.Minimum = Settings.HardFloor;
        _brightness.Maximum = 100;
        _brightness.TickFrequency = 5;
        _brightness.SetBounds(14, y, 320, 45);
        _brightness.Scroll += (_, _) =>
        {
            if (_loading) return;
            _controller.SetBrightness(_brightness.Value);
            _brightnessValue.Text = $"{_controller.TargetBrightness}%";
        };
        Controls.Add(_brightness);

        _brightnessValue.SetBounds(344, y + 8, 70, 20);
        _brightnessValue.Font = new Font("Segoe UI Semibold", 10f);
        Controls.Add(_brightnessValue);
        y += 52;

        Controls.Add(Label(Strings.NeverDarkerThan, 14, y + 3));
        _minBrightness.SetBounds(220, y, 70, 24);
        _minBrightness.Minimum = Settings.HardFloor;
        _minBrightness.Maximum = 90;
        _minBrightness.ValueChanged += (_, _) =>
        {
            if (_loading) return;
            _settings.MinBrightness = (int)_minBrightness.Value;
            _controller.SetBrightness(_settings.Brightness);
            LoadValues();
        };
        Controls.Add(_minBrightness);
        Controls.Add(Hint(Strings.NeverDarkerHint, 14, y + 28));
        y += 52;

        Controls.Add(Section(Strings.SectionMode, ref y));

        _mode.SetBounds(14, y, 400, 24);
        _mode.DropDownStyle = ComboBoxStyle.DropDownList;
        _mode.Items.AddRange([Strings.ModeGlobalItem, Strings.ModePerMonitorItem]);
        _mode.SelectedIndexChanged += (_, _) =>
        {
            if (_loading) return;
            _controller.SetMode(_mode.SelectedIndex == 0 ? EngineKind.Magnification : EngineKind.PerMonitor);
            LoadValues();
        };
        Controls.Add(_mode);
        y += 34;

        Controls.Add(Section(Strings.SectionPerMonitor, ref y));

        foreach (var screen in Screen.AllScreens)
        {
            var device = screen.DeviceName;
            Controls.Add(Label(TrayApplicationContext.MonitorCaption(screen), 14, y + 8));

            var slider = new TrackBar { Maximum = 100, TickFrequency = 5 };
            slider.SetBounds(150, y, 190, 45);
            var value = new Label { Font = new Font("Segoe UI Semibold", 10f) };
            value.SetBounds(350, y + 8, 64, 20);

            slider.Scroll += (_, _) =>
            {
                if (_loading) return;
                _controller.SetMonitorBrightness(device, slider.Value);
                value.Text = $"{_controller.TargetFor(device)}%";
            };

            Controls.Add(slider);
            Controls.Add(value);
            _monitorSliders.Add((device, slider, value));
            y += 44;
        }

        _perMonitorWarning.AutoSize = true;
        _perMonitorWarning.Left = 14;
        _perMonitorWarning.Top = y;
        _perMonitorWarning.ForeColor = Color.FromArgb(176, 96, 0);
        Controls.Add(_perMonitorWarning);
        y += 74;

        Controls.Add(Section(Strings.ScheduleSection, ref y));

        var scheduleButton = new Button { Text = Strings.ScheduleOpen };
        scheduleButton.SetBounds(14, y - 2, 140, 27);
        scheduleButton.Click += (_, _) => ScheduleRequested?.Invoke(this, EventArgs.Empty);
        Controls.Add(scheduleButton);
        Controls.Add(Hint(Strings.ScheduleUseIt, 166, y + 4));
        y += 38;

        Controls.Add(Section(Strings.SectionControls, ref y));

        _trayWheel.SetBounds(14, y, 400, 22);
        _trayWheel.Text = Strings.TrayWheelOption;
        _trayWheel.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            _settings.EnableTrayWheel = _trayWheel.Checked;
            TrayWheelToggled?.Invoke(this, _trayWheel.Checked);
        };
        Controls.Add(_trayWheel);
        y += 26;

        Controls.Add(Label(Strings.WheelStep, 14, y + 3));
        _wheelStep.SetBounds(220, y, 70, 24);
        _wheelStep.Minimum = 1;
        _wheelStep.Maximum = 25;
        _wheelStep.ValueChanged += (_, _) => { if (!_loading) _settings.WheelStep = (int)_wheelStep.Value; };
        Controls.Add(_wheelStep);
        y += 32;

        _showOsd.SetBounds(14, y, 400, 22);
        _showOsd.Text = Strings.ShowOsdOption;
        _showOsd.CheckedChanged += (_, _) => { if (!_loading) _settings.ShowOsd = _showOsd.Checked; };
        Controls.Add(_showOsd);
        y += 26;

        Controls.Add(Label(Strings.RampLabel, 14, y + 3));
        _ramp.SetBounds(220, y, 70, 24);
        _ramp.Minimum = 0;
        _ramp.Maximum = 2000;
        _ramp.Increment = 20;
        _ramp.ValueChanged += (_, _) => { if (!_loading) _settings.RampMilliseconds = (int)_ramp.Value; };
        Controls.Add(_ramp);
        y += 32;

        Controls.Add(Label(Strings.LanguageLabel, 14, y + 3));
        _language.SetBounds(220, y, 194, 24);
        _language.DropDownStyle = ComboBoxStyle.DropDownList;
        _language.Items.AddRange([Strings.LanguageAuto, "English", "Русский"]);
        _language.SelectedIndexChanged += (_, _) =>
        {
            if (_loading) return;
            LanguageChanged?.Invoke(this, (AppLanguage)_language.SelectedIndex);
        };
        Controls.Add(_language);
        y += 32;

        _autoStart.SetBounds(14, y, 400, 22);
        _autoStart.Text = Strings.StartWithWindows;
        _autoStart.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            if (!AutoStart.TrySet(_autoStart.Checked, out var error))
            {
                MessageBox.Show(this, Strings.AutoStartFailed(error), "fDimmer",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _loading = true;
                _autoStart.Checked = AutoStart.IsEnabled;
                _loading = false;
            }
        };
        Controls.Add(_autoStart);
        y += 34;

        var close = new Button { Text = Strings.Close, DialogResult = DialogResult.OK };
        close.SetBounds(324, y, 90, 28);
        close.Click += (_, _) => Close();
        Controls.Add(close);
        AcceptButton = close;

        ClientSize = new Size(430, y + 44);
    }

    private static Label Label(string text, int x, int y) =>
        new() { Text = text, AutoSize = true, Left = x, Top = y };

    private static Label Hint(string text, int x, int y) =>
        new()
        {
            Text = text,
            AutoSize = true,
            Left = x,
            Top = y,
            ForeColor = SystemColors.GrayText,
        };

    private static Label Section(string text, ref int y)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            Left = 12,
            Top = y,
            Font = new Font("Segoe UI Semibold", 9.5f),
        };
        y += 24;
        return label;
    }

    private void LoadValues()
    {
        _loading = true;
        try
        {
            _brightness.Minimum = Math.Max(Settings.HardFloor, _settings.MinBrightness);
            _brightness.Value = Math.Clamp(_controller.TargetBrightness, _brightness.Minimum, _brightness.Maximum);
            _brightnessValue.Text = $"{_controller.TargetBrightness}%";
            _minBrightness.Value = _settings.MinBrightness;
            _mode.SelectedIndex = _controller.IsPerMonitor ? 1 : 0;
            _language.SelectedIndex = (int)_settings.Language;
            _wheelStep.Value = _settings.WheelStep;
            _ramp.Value = _settings.RampMilliseconds;
            _showOsd.Checked = _settings.ShowOsd;
            _trayWheel.Checked = _settings.EnableTrayWheel;
            _autoStart.Checked = AutoStart.IsEnabled;

            var perMonitor = _controller.IsPerMonitor;
            foreach (var (device, slider, value) in _monitorSliders)
            {
                slider.Minimum = Math.Max(Settings.HardFloor, _settings.MinBrightness);
                slider.Value = Math.Clamp(_controller.LevelOf(device), slider.Minimum, slider.Maximum);
                slider.Enabled = perMonitor;
                value.Text = perMonitor ? $"{_controller.TargetFor(device)}%" : "—";
            }

            // Порог гаммы узнаём только в самом режиме: проба ненадолго трогает гамму монитора.
            var floor = perMonitor
                ? Screen.AllScreens.Min(s => _controller.GammaFloor(s.DeviceName))
                : 50;
            _perMonitorWarning.Text = perMonitor
                ? Strings.PerMonitorWarning(floor)
                : Strings.PerMonitorOnlyHint + "\n" + Strings.PerMonitorWarning(floor);
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnControllerStateChanged(object? sender, EventArgs e)
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(LoadValues);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _controller.StateChanged -= OnControllerStateChanged;
        _settings.Save();
        base.OnFormClosed(e);
    }
}
