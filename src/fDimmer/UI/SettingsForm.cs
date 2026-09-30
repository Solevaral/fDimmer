using fDimmer.Core;
using fDimmer.UI.Controls;

namespace fDimmer.UI;

/// <summary>
/// Второстепенные настройки. Яркость, режим и мониторы живут в главном окне, здесь —
/// то, что меняют редко. Все изменения применяются сразу, без кнопки «Применить».
/// </summary>
internal sealed class SettingsForm : Form
{
    private readonly DimController _controller;
    private readonly Settings _settings;

    private readonly SegmentedControl _language = new();
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
        LoadValues();
    }

    /// <summary>Пользователь включил или выключил обработку колеса над треем.</summary>
    public event EventHandler<bool>? TrayWheelToggled;

    /// <summary>Выбран другой язык интерфейса.</summary>
    public event EventHandler<AppLanguage>? LanguageChanged;

    private void BuildLayout()
    {
        var y = 16;

        Controls.Add(Section(Strings.SectionBrightness, ref y));

        Controls.Add(Label(Strings.NeverDarkerThan, 16, y + 3));
        _minBrightness.SetBounds(244, y, 76, 26);
        _minBrightness.Minimum = Settings.HardFloor;
        _minBrightness.Maximum = 90;
        _minBrightness.ValueChanged += (_, _) =>
        {
            if (_loading) return;
            _settings.MinBrightness = (int)_minBrightness.Value;
            _controller.SetBrightness(_settings.Brightness);
        };
        Controls.Add(_minBrightness);
        Controls.Add(Hint(Strings.NeverDarkerHint, 16, y + 30));
        y += 62;

        Controls.Add(Label(Strings.RampLabel, 16, y + 3));
        _ramp.SetBounds(244, y, 76, 26);
        _ramp.Minimum = 0;
        _ramp.Maximum = 2000;
        _ramp.Increment = 20;
        _ramp.ValueChanged += (_, _) => { if (!_loading) _settings.RampMilliseconds = (int)_ramp.Value; };
        Controls.Add(_ramp);
        y += 44;

        Controls.Add(Section(Strings.SectionControls, ref y));

        _trayWheel.SetBounds(16, y, 420, 24);
        _trayWheel.Text = Strings.TrayWheelOption;
        _trayWheel.CheckedChanged += (_, _) =>
        {
            if (_loading) return;
            _settings.EnableTrayWheel = _trayWheel.Checked;
            TrayWheelToggled?.Invoke(this, _trayWheel.Checked);
        };
        Controls.Add(_trayWheel);
        y += 32;

        Controls.Add(Label(Strings.WheelStep, 16, y + 3));
        _wheelStep.SetBounds(244, y, 76, 26);
        _wheelStep.Minimum = 1;
        _wheelStep.Maximum = 25;
        _wheelStep.ValueChanged += (_, _) => { if (!_loading) _settings.WheelStep = (int)_wheelStep.Value; };
        Controls.Add(_wheelStep);
        y += 36;

        _showOsd.SetBounds(16, y, 420, 24);
        _showOsd.Text = Strings.ShowOsdOption;
        _showOsd.CheckedChanged += (_, _) => { if (!_loading) _settings.ShowOsd = _showOsd.Checked; };
        Controls.Add(_showOsd);
        y += 40;

        Controls.Add(Section(Strings.SectionSystem, ref y));

        Controls.Add(Label(Strings.LanguageLabel, 16, y + 8));
        _language.SetBounds(120, y, 320, 34);
        _language.Items = [Strings.LanguageAuto, "English", "Русский"];
        _language.SelectionChanged += (_, _) =>
        {
            if (_loading) return;
            LanguageChanged?.Invoke(this, (AppLanguage)_language.SelectedIndex);
        };
        Controls.Add(_language);
        y += 46;

        _autoStart.SetBounds(16, y, 420, 24);
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
        y += 44;

        var close = new Button { Text = Strings.Close, DialogResult = DialogResult.OK };
        close.SetBounds(340, y, 100, 32);
        close.Click += (_, _) => Close();
        Controls.Add(close);
        AcceptButton = close;

        ClientSize = new Size(456, y + 48);
    }

    private static Label Label(string text, int x, int y) =>
        new() { Text = text, AutoSize = true, Left = x, Top = y, ForeColor = Theme.Text };

    private static Label Hint(string text, int x, int y) =>
        new() { Text = text, AutoSize = true, Left = x, Top = y, ForeColor = Theme.TextDim };

    private static Label Section(string text, ref int y)
    {
        var label = new Label
        {
            Text = text,
            AutoSize = true,
            Left = 14,
            Top = y,
            Font = Theme.SemiBold(10.5f),
            ForeColor = Theme.AccentCyan,
        };
        y += 30;
        return label;
    }

    private void LoadValues()
    {
        _loading = true;
        try
        {
            _minBrightness.Value = _settings.MinBrightness;
            _language.SelectedIndex = (int)_settings.Language;
            _wheelStep.Value = _settings.WheelStep;
            _ramp.Value = _settings.RampMilliseconds;
            _showOsd.Checked = _settings.ShowOsd;
            _trayWheel.Checked = _settings.EnableTrayWheel;
            _autoStart.Checked = AutoStart.IsEnabled;
        }
        finally
        {
            _loading = false;
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _settings.Save();
        base.OnFormClosed(e);
    }
}
