using System.Diagnostics;
using Microsoft.Win32;

namespace fDimmer.Core;

/// <summary>
/// Состояние затемнения и плавный переход между уровнями. Держит оба режима — общий
/// и «по мониторам», — переключает их и следит за системными событиями. Если общий
/// режим недоступен, откатывается на «по мониторам».
/// </summary>
public sealed class DimController : IDisposable
{
    /// <summary>Ключ единственного уровня в общем режиме.</summary>
    private const string Everything = "";

    private readonly Settings _settings;
    private readonly MagnificationEngine _magnification;
    private readonly PerMonitorEngine _perMonitor;
    private readonly System.Windows.Forms.Timer _rampTimer;
    private readonly Stopwatch _rampClock = new();
    private readonly SynchronizationContext _ui;

    private IDimEngine _engine;
    private Dictionary<string, double> _current = [];
    private Dictionary<string, double> _rampFrom = [];
    private Dictionary<string, double> _rampTo = [];
    private bool _disposed;

    public DimController(Settings settings)
    {
        _settings = settings;
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _magnification = new MagnificationEngine();
        _perMonitor = new PerMonitorEngine();
        _engine = Resolve(_settings.Engine);

        _rampTimer = new System.Windows.Forms.Timer { Interval = 15 };
        _rampTimer.Tick += OnRampTick;

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    /// <summary>Сообщение для пользователя (например, об откате на другой режим).</summary>
    public event EventHandler<string>? Notice;

    /// <summary>Изменилась цель — повод показать OSD и обновить меню.</summary>
    public event EventHandler? StateChanged;

    public Settings Settings => _settings;

    public EngineKind Mode => _engine.Kind;

    public bool IsPerMonitor => _engine.Kind == EngineKind.PerMonitor;

    public bool IsEnabled => _settings.Enabled;

    /// <summary>
    /// Уровень, к которому идёт затемнение (100 — экран не затемнён). В режиме
    /// «по мониторам» — среднее по мониторам: для иконки трея и индикатора.
    /// </summary>
    public int TargetBrightness =>
        !IsPerMonitor
            ? (_settings.Enabled ? _settings.ClampBrightness(_settings.Brightness) : 100)
            : (int)Math.Round(Screen.AllScreens.Average(s => TargetFor(s.DeviceName)));

    /// <summary>Текущая цель конкретного монитора в режиме «по мониторам».</summary>
    public int TargetFor(string device) =>
        _settings.Enabled ? LevelOf(device) : 100;

    /// <summary>Сохранённый уровень монитора — даже если затемнение сейчас выключено.</summary>
    public int LevelOf(string device) =>
        _settings.ClampBrightness(_settings.MonitorLevels.TryGetValue(device, out var level)
            ? level
            : _settings.Brightness);

    /// <summary>Ниже этого уровня монитор темнеет уже окном поверх, а не гаммой.</summary>
    public int GammaFloor(string device) => _perMonitor.GammaFloor(device);

    public void Start() => ApplyTarget(animate: false);

    public void SetEnabled(bool enabled)
    {
        if (_settings.Enabled == enabled) return;
        _settings.Enabled = enabled;
        ApplyTarget(animate: true);
        Changed();
    }

    public void Toggle() => SetEnabled(!_settings.Enabled);

    /// <summary>Один уровень. В режиме «по мониторам» выставляется всем мониторам сразу.</summary>
    public void SetBrightness(int value, bool animate = true)
    {
        var clamped = _settings.ClampBrightness(value);

        _settings.Brightness = clamped;
        if (IsPerMonitor)
        {
            foreach (var screen in Screen.AllScreens) _settings.MonitorLevels[screen.DeviceName] = clamped;
        }

        _settings.Enabled = true;
        ApplyTarget(animate);
        Changed();
    }

    /// <summary>
    /// Изменение на шаг: положительное значение делает экран светлее. В режиме
    /// «по мониторам» сдвигает все мониторы, сохраняя разницу между ними.
    /// </summary>
    public void Nudge(int delta)
    {
        if (!IsPerMonitor)
        {
            SetBrightness((_settings.Enabled ? _settings.Brightness : 100) + delta);
            return;
        }

        foreach (var screen in Screen.AllScreens)
        {
            var basis = _settings.Enabled ? LevelOf(screen.DeviceName) : 100;
            _settings.MonitorLevels[screen.DeviceName] = _settings.ClampBrightness(basis + delta);
        }

        _settings.Enabled = true;
        ApplyTarget(animate: true);
        Changed();
    }

    public void SetMonitorBrightness(string device, int value)
    {
        _settings.MonitorLevels[device] = _settings.ClampBrightness(value);
        _settings.Enabled = true;
        ApplyTarget(animate: false); // ползунок тянут руками — плавность только мешает
        Changed();
    }

    public void SetMode(EngineKind kind)
    {
        if (_engine.Kind == kind) return;

        // При первом переходе в «по мониторам» мониторы стартуют с текущего общего уровня.
        if (kind == EngineKind.PerMonitor && _settings.MonitorLevels.Count == 0)
        {
            foreach (var screen in Screen.AllScreens)
            {
                _settings.MonitorLevels[screen.DeviceName] = _settings.Brightness;
            }
        }

        _rampTimer.Stop();
        _engine.Reset();
        _settings.Engine = kind;
        _engine = Resolve(kind);
        _current = [];
        ApplyTarget(animate: false);
        Changed();
    }

    /// <summary>Переприменяет текущий уровень — после разблокировки или смены дисплеев.</summary>
    public void Reapply()
    {
        _current = [];
        ApplyTarget(animate: false);
    }

    public void ResetScreen()
    {
        _rampTimer.Stop();
        _magnification.Reset();
        _perMonitor.Reset();
        _current = [];
    }

    private IDimEngine Resolve(EngineKind kind)
    {
        if (kind == EngineKind.Magnification)
        {
            if (_magnification.IsAvailable) return _magnification;

            _settings.Engine = EngineKind.PerMonitor;
            Notice?.Invoke(this, Strings.FellBackToPerMonitor(_magnification.UnavailableReason));
        }

        return _perMonitor;
    }

    private Dictionary<string, double> Targets() =>
        IsPerMonitor
            ? Screen.AllScreens.ToDictionary(s => s.DeviceName, s => (double)TargetFor(s.DeviceName),
                StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, double> { [Everything] = TargetBrightness };

    private double CurrentOf(string key) => _current.TryGetValue(key, out var value) ? value : 100;

    private void ApplyTarget(bool animate)
    {
        var targets = Targets();

        if (!animate || _settings.RampMilliseconds == 0
            || targets.All(t => Math.Abs(CurrentOf(t.Key) - t.Value) < 0.5))
        {
            _rampTimer.Stop();
            _current = targets;
            Push(targets);
            return;
        }

        _rampFrom = targets.Keys.ToDictionary(k => k, CurrentOf, StringComparer.OrdinalIgnoreCase);
        _rampTo = targets;
        _rampClock.Restart();
        _rampTimer.Start();
    }

    private void OnRampTick(object? sender, EventArgs e)
    {
        var t = Math.Clamp(_rampClock.Elapsed.TotalMilliseconds / _settings.RampMilliseconds, 0, 1);
        // Плавное замедление к концу перехода.
        var eased = 1 - Math.Pow(1 - t, 3);

        _current = _rampTo.ToDictionary(
            p => p.Key,
            p => _rampFrom[p.Key] + (p.Value - _rampFrom[p.Key]) * eased,
            StringComparer.OrdinalIgnoreCase);
        Push(_current);

        if (t >= 1)
        {
            _rampTimer.Stop();
            _rampClock.Stop();
            _current = _rampTo;
            Push(_rampTo);
        }
    }

    private void Push(Dictionary<string, double> levels)
    {
        if (IsPerMonitor)
        {
            _perMonitor.ApplyLevels(levels);
            return;
        }

        var level = levels[Everything];
        _engine.Apply(level);

        // Общий режим мог отвалиться на ходу (конфликт с цветовыми фильтрами, смена сеанса).
        if (_engine.IsAvailable) return;

        var reason = _engine.UnavailableReason;
        _engine.Reset();
        _settings.Engine = EngineKind.PerMonitor;
        _engine = _perMonitor;
        _perMonitor.Apply(level);
        Notice?.Invoke(this, Strings.SwitchedToPerMonitor(reason));
        Changed();
    }

    private void Changed() => StateChanged?.Invoke(this, EventArgs.Empty);

    // SystemEvents приходят на своём потоке — возвращаемся в UI-поток.
    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        _ui.Post(_ => { if (!_disposed) Reapply(); }, null);

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason is not (SessionSwitchReason.SessionUnlock
                          or SessionSwitchReason.ConsoleConnect
                          or SessionSwitchReason.SessionLogon))
        {
            return;
        }

        _ui.Post(_ => { if (!_disposed) Reapply(); }, null);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;

        _rampTimer.Stop();
        _rampTimer.Dispose();
        _magnification.Dispose();
        _perMonitor.Dispose();
    }
}
