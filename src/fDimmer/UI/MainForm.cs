using fDimmer.Core;
using fDimmer.UI.Controls;
using Microsoft.Win32;

namespace fDimmer.UI;

/// <summary>
/// Главное окно: вкл/выкл, режим, карта мониторов как в параметрах дисплея Windows,
/// ползунок выбранного монитора и быстрые уровни. Всё применяется сразу.
/// </summary>
internal sealed class MainForm : Form
{
    private static readonly int[] Quick = [100, 90, 80, 70, 60, 50, 40, 30, 20];

    private readonly DimController _controller;
    private readonly Settings _settings;

    private readonly ToggleSwitch _power = new();
    private readonly SegmentedControl _mode = new();
    private readonly MonitorMap _map = new();
    private readonly Label _target = new();
    private readonly Label _value = new();
    private readonly DimSlider _slider = new();
    private readonly ChipBar _chips = new();
    private readonly Label _banner = new();
    private readonly FlatButton _identify = new();
    private readonly FlatButton _schedule = new();
    private readonly FlatButton _settingsButton = new();

    private readonly Bitmap? _logo;
    private readonly Font _titleFont = Theme.Font(17f, FontStyle.Bold);
    private readonly Font _statusFont = Theme.Font(9.5f);

    private string? _selected;
    private string _status = string.Empty;
    private bool _refreshing;

    public MainForm(DimController controller)
    {
        _controller = controller;
        _settings = controller.Settings;

        Text = "fDimmer";
        Icon = AppIcon.Default;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = Theme.Font(9.5f);
        ClientSize = new Size(560, 628);
        DoubleBuffered = true;
        Theme.ApplyWindowChrome(this);

        _logo = AppIcon.Large(64);
        _selected = Screen.PrimaryScreen?.DeviceName;

        BuildLayout();
        RebuildTiles();
        RefreshState();

        _controller.StateChanged += OnControllerStateChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public event EventHandler? ScheduleRequested;

    public event EventHandler? SettingsRequested;

    /// <summary>Пользователь только что включил режим «по мониторам» — повод предупредить.</summary>
    public event EventHandler? PerMonitorEntered;

    private void BuildLayout()
    {
        _power.SetBounds(492, 30, 48, 26);
        _power.Toggled += (_, _) => _controller.SetEnabled(_power.Checked);

        _mode.SetBounds(20, 88, 520, 38);
        _mode.Items = [Strings.ModeCommon, Strings.ModePerMonitor];
        _mode.SelectionChanged += (_, _) =>
        {
            var perMonitor = _mode.SelectedIndex == 1;
            _controller.SetMode(perMonitor ? EngineKind.PerMonitor : EngineKind.Magnification);
            if (perMonitor) PerMonitorEntered?.Invoke(this, EventArgs.Empty);
        };

        _map.SetBounds(20, 140, 520, 206);
        _map.MonitorClicked += (_, device) => SelectMonitor(device);

        _target.SetBounds(20, 362, 340, 30);
        _target.Font = Theme.SemiBold(12f);
        _target.ForeColor = Theme.Text;
        _target.TextAlign = ContentAlignment.MiddleLeft;

        _value.SetBounds(380, 356, 160, 40);
        _value.Font = Theme.Font(22f, FontStyle.Bold);
        _value.ForeColor = Theme.Text;
        _value.TextAlign = ContentAlignment.MiddleRight;

        _slider.SetBounds(20, 400, 520, 46);
        _slider.ValueChangedByUser += (_, _) => ApplyLevel(_slider.Value, animate: false);

        _chips.SetBounds(20, 452, 520, 30);
        _chips.Values = Quick;
        _chips.ChipClicked += (_, level) => ApplyLevel(level, animate: true);

        _banner.SetBounds(20, 498, 520, 52);
        _banner.Padding = new Padding(12, 0, 12, 0);
        _banner.TextAlign = ContentAlignment.MiddleLeft;
        _banner.Font = Theme.Font(8.75f);

        _identify.SetBounds(20, 570, 164, 38);
        _identify.Text = Strings.Identify;
        _identify.Click += (_, _) => IdentifyForm.ShowAll(_map.Tiles.Select(t => (t.Number, t.Bounds)));

        _schedule.SetBounds(198, 570, 164, 38);
        _schedule.Text = Strings.ScheduleButton;
        _schedule.Click += (_, _) => ScheduleRequested?.Invoke(this, EventArgs.Empty);

        _settingsButton.SetBounds(376, 570, 164, 38);
        _settingsButton.Text = Strings.SettingsButton;
        _settingsButton.Click += (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty);

        Controls.AddRange([_power, _mode, _map, _target, _value, _slider, _chips, _banner,
                           _identify, _schedule, _settingsButton]);
    }

    // ---- действия пользователя ----

    private void SelectMonitor(string device)
    {
        _selected = device;

        // Клик по конкретному монитору в общем режиме — значит, хотят настроить его отдельно.
        if (!_controller.IsPerMonitor)
        {
            _controller.SetMode(EngineKind.PerMonitor);
            PerMonitorEntered?.Invoke(this, EventArgs.Empty);
        }

        RefreshState();
    }

    private void ApplyLevel(int level, bool animate)
    {
        if (_controller.IsPerMonitor && _selected is not null)
        {
            _controller.SetMonitorBrightness(_selected, level);
        }
        else
        {
            _controller.SetBrightness(level, animate);
        }
    }

    // ---- отображение состояния ----

    private void RebuildTiles()
    {
        var screens = Screen.AllScreens;
        _map.SetTiles(screens.Select((s, i) => new MonitorTile(s.DeviceName, s.Bounds, i + 1, s.Primary)));

        if (_selected is null || screens.All(s => s.DeviceName != _selected))
        {
            _selected = (screens.FirstOrDefault(s => s.Primary) ?? screens[0]).DeviceName;
        }
    }

    private void RefreshState()
    {
        _refreshing = true;
        try
        {
            var perMonitor = _controller.IsPerMonitor;
            var enabled = _controller.IsEnabled;

            _power.Checked = enabled;
            _mode.SelectedIndex = perMonitor ? 1 : 0;

            foreach (var tile in _map.Tiles)
            {
                tile.Level = perMonitor ? _controller.LevelOf(tile.Device) : _settings.ClampBrightness(_settings.Brightness);
                tile.Preview = enabled ? tile.Level : 100;
            }
            _map.AllSelected = !perMonitor;
            _map.SelectedDevice = _selected;

            var selectedTile = _map.Tiles.FirstOrDefault(t => t.Device == _selected);
            var level = perMonitor && selectedTile is not null
                ? selectedTile.Level
                : _settings.ClampBrightness(_settings.Brightness);

            _target.Text = perMonitor && selectedTile is not null
                ? TrayApplicationContext.MonitorCaption(Screen.AllScreens.First(s => s.DeviceName == selectedTile.Device))
                : Strings.AllMonitors;
            _value.Text = $"{level}%";
            _value.ForeColor = enabled ? Theme.Text : Theme.TextDim;

            _slider.Minimum = Math.Max(Settings.HardFloor, _settings.MinBrightness);
            _slider.Value = level;
            _chips.Values = Quick.Where(q => q >= _slider.Minimum).ToArray();
            _chips.Current = enabled ? level : null;

            if (perMonitor && _selected is not null)
            {
                var floor = _controller.GammaFloor(_selected);
                _slider.Marker = floor < 100 ? floor : null;
                _slider.MarkerLabel = Strings.GammaMarker(floor);
                _banner.Text = Strings.PerMonitorBanner(floor);
                _banner.ForeColor = Theme.Warning;
                _banner.BackColor = Theme.WarningSurface;
            }
            else
            {
                _slider.Marker = null;
                _banner.Text = Strings.CommonModeInfo;
                _banner.ForeColor = Theme.TextDim;
                _banner.BackColor = Theme.Surface;
            }

            _status = enabled
                ? Strings.StatusOn(perMonitor ? Strings.ModePerMonitor : Strings.ModeCommon)
                : Strings.StatusOff;
            Invalidate(new Rectangle(0, 0, ClientSize.Width, 80));
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void OnControllerStateChanged(object? sender, EventArgs e)
    {
        if (_refreshing || IsDisposed || !IsHandleCreated) return;
        BeginInvoke(RefreshState);
    }

    // SystemEvents приходят на своём потоке — возвращаемся в UI-поток.
    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (IsDisposed || !IsHandleCreated) return;
        BeginInvoke(() =>
        {
            RebuildTiles();
            RefreshState();
        });
    }

    // ---- шапка ----

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

        var s = DeviceDpi / 96f;
        if (_logo is not null)
        {
            g.DrawImage(_logo, new RectangleF(20 * s, 18 * s, 52 * s, 52 * s));
        }

        TextRenderer.DrawText(g, "fDimmer", _titleFont, new Point((int)(84 * s), (int)(16 * s)), Theme.Text);
        TextRenderer.DrawText(g, _status, _statusFont, new Point((int)(86 * s), (int)(50 * s)),
            _controller.IsEnabled ? Theme.AccentCyan : Theme.TextDim);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _controller.StateChanged -= OnControllerStateChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _settings.Save();
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _logo?.Dispose();
            _titleFont.Dispose();
            _statusFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
