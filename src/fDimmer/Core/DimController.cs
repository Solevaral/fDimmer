using System.Diagnostics;
using Microsoft.Win32;

namespace fDimmer.Core;

/// <summary>
/// Состояние затемнения и плавный переход между уровнями. Держит оба движка,
/// переключает их, следит за системными событиями и откатывается на оверлей,
/// если полноэкранный цветовой эффект недоступен.
/// </summary>
public sealed class DimController : IDisposable
{
    private readonly Settings _settings;
    private readonly MagnificationEngine _magnification;
    private readonly OverlayEngine _overlay;
    private readonly System.Windows.Forms.Timer _rampTimer;
    private readonly Stopwatch _rampClock = new();
    private readonly SynchronizationContext _ui;

    private IDimEngine _engine;
    private double _current = 100;
    private double _rampFrom = 100;
    private double _rampTo = 100;
    private bool _disposed;

    public DimController(Settings settings)
    {
        _settings = settings;
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _magnification = new MagnificationEngine();
        _overlay = new OverlayEngine();
        _overlay.SetMonitors(_settings.OverlayMonitors);

        _engine = Resolve(_settings.Engine);

        _rampTimer = new System.Windows.Forms.Timer { Interval = 15 };
        _rampTimer.Tick += OnRampTick;

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    /// <summary>Сообщение для пользователя (например, об откате на другой движок).</summary>
    public event EventHandler<string>? Notice;

    /// <summary>Изменилась цель — повод показать OSD и обновить меню.</summary>
    public event EventHandler? StateChanged;

    public Settings Settings => _settings;

    public IDimEngine ActiveEngine => _engine;

    /// <summary>Уровень, к которому идёт затемнение (100 — экран не затемнён).</summary>
    public int TargetBrightness => _settings.Enabled ? _settings.ClampBrightness(_settings.Brightness) : 100;

    public bool IsEnabled => _settings.Enabled;

    public void Start() => ApplyTarget(animate: false);

    public void SetEnabled(bool enabled)
    {
        if (_settings.Enabled == enabled) return;
        _settings.Enabled = enabled;
        ApplyTarget(animate: true);
        Changed();
    }

    public void Toggle() => SetEnabled(!_settings.Enabled);

    public void SetBrightness(int value, bool animate = true)
    {
        var clamped = _settings.ClampBrightness(value);
        if (clamped == _settings.Brightness && _settings.Enabled) return;

        _settings.Brightness = clamped;
        _settings.Enabled = true;
        ApplyTarget(animate);
        Changed();
    }

    /// <summary>Изменение на шаг: положительное значение делает экран светлее.</summary>
    public void Nudge(int delta)
    {
        var basis = _settings.Enabled ? _settings.Brightness : 100;
        SetBrightness(basis + delta);
    }

    public void SetEngine(EngineKind kind)
    {
        if (_engine.Kind == kind && _engine.IsAvailable) return;

        _engine.Reset();
        _settings.Engine = kind;
        _engine = Resolve(kind);
        _current = 100;
        ApplyTarget(animate: false);
        Changed();
    }

    public void SetOverlayMonitors(IEnumerable<string> deviceNames)
    {
        _settings.OverlayMonitors = deviceNames.ToList();
        _overlay.SetMonitors(_settings.OverlayMonitors);
        if (_engine.Kind == EngineKind.Overlay) ApplyTarget(animate: false);
        Changed();
    }

    /// <summary>Переприменяет текущий уровень — после разблокировки или смены дисплеев.</summary>
    public void Reapply()
    {
        _current = 100;
        ApplyTarget(animate: false);
    }

    public void ResetScreen()
    {
        _rampTimer.Stop();
        _magnification.Reset();
        _overlay.Reset();
        _current = 100;
    }

    private IDimEngine Resolve(EngineKind kind)
    {
        if (kind == EngineKind.Magnification)
        {
            if (_magnification.IsAvailable) return _magnification;

            _settings.Engine = EngineKind.Overlay;
            Notice?.Invoke(this, Strings.FellBackToOverlay(_magnification.UnavailableReason));
        }

        return _overlay;
    }

    private void ApplyTarget(bool animate)
    {
        var target = TargetBrightness;

        if (!animate || _settings.RampMilliseconds == 0 || Math.Abs(_current - target) < 0.5)
        {
            _rampTimer.Stop();
            _current = target;
            Push(target);
            return;
        }

        _rampFrom = _current;
        _rampTo = target;
        _rampClock.Restart();
        _rampTimer.Start();
    }

    private void OnRampTick(object? sender, EventArgs e)
    {
        var t = Math.Clamp(_rampClock.Elapsed.TotalMilliseconds / _settings.RampMilliseconds, 0, 1);
        // Плавное замедление к концу перехода.
        var eased = 1 - Math.Pow(1 - t, 3);
        _current = _rampFrom + (_rampTo - _rampFrom) * eased;
        Push(_current);

        if (t >= 1)
        {
            _rampTimer.Stop();
            _rampClock.Stop();
            _current = _rampTo;
            Push(_rampTo);
        }
    }

    private void Push(double brightness)
    {
        _engine.Apply(brightness);

        // Движок мог отвалиться на ходу (конфликт с цветовыми фильтрами, смена сеанса).
        if (_engine.IsAvailable) return;

        var reason = _engine.UnavailableReason;
        _engine.Reset();
        _settings.Engine = EngineKind.Overlay;
        _engine = _overlay;
        _engine.Apply(brightness);
        Notice?.Invoke(this, Strings.SwitchedToOverlay(reason));
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
        _overlay.Dispose();
    }
}
