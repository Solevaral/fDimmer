using fDimmer.UI;

namespace fDimmer.Core;

/// <summary>
/// Классическое затемнение оверлеем: по одному чёрному click-through окну на выбранный
/// монитор. В отличие от <see cref="MagnificationEngine"/> не гасит системные окна
/// (Alt+Tab, «Пуск», панель задач), зато позволяет затемнить только часть мониторов.
/// </summary>
public sealed class OverlayEngine : IDimEngine
{
    private readonly Dictionary<string, DimOverlayForm> _overlays = [];
    private readonly System.Windows.Forms.Timer _topmostTimer;
    private IReadOnlyCollection<string> _monitors = [];
    private double _brightness = 100;
    private bool _active;
    private bool _disposed;

    public OverlayEngine()
    {
        // Чужие topmost-окна периодически перекрывают оверлей — возвращаем его наверх.
        _topmostTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _topmostTimer.Tick += (_, _) =>
        {
            foreach (var overlay in _overlays.Values) overlay.BringToTop();
        };
    }

    public EngineKind Kind => EngineKind.Overlay;

    public string DisplayName => "Оверлей по мониторам";

    public bool IsAvailable => true;

    public string? UnavailableReason => null;

    /// <summary>Имена мониторов (Screen.DeviceName). Пустой набор — все мониторы.</summary>
    public void SetMonitors(IEnumerable<string> deviceNames)
    {
        _monitors = deviceNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        Rebuild();
    }

    public void Apply(double brightness)
    {
        if (_disposed) return;

        _brightness = brightness;
        _active = brightness < 99.5;

        if (!_active)
        {
            Reset();
            return;
        }

        Rebuild();
        var dim = 1.0 - Math.Clamp(brightness, 0, 100) / 100.0;
        foreach (var overlay in _overlays.Values) overlay.SetDimAmount(dim);
        _topmostTimer.Enabled = true;
    }

    public void Reset()
    {
        _active = false;
        _brightness = 100;
        _topmostTimer.Enabled = false;

        foreach (var overlay in _overlays.Values)
        {
            overlay.Hide();
            overlay.Dispose();
        }
        _overlays.Clear();
    }

    private void Rebuild()
    {
        if (!_active || _disposed) return;

        var screens = Screen.AllScreens
            .Where(s => _monitors.Count == 0 || _monitors.Contains(s.DeviceName))
            .ToDictionary(s => s.DeviceName, StringComparer.OrdinalIgnoreCase);

        foreach (var gone in _overlays.Keys.Where(k => !screens.ContainsKey(k)).ToList())
        {
            _overlays[gone].Dispose();
            _overlays.Remove(gone);
        }

        foreach (var (name, screen) in screens)
        {
            if (_overlays.TryGetValue(name, out var existing))
            {
                existing.SetBounds(screen.Bounds);
            }
            else
            {
                var overlay = new DimOverlayForm(screen.Bounds);
                overlay.Show();
                overlay.SetDimAmount(1.0 - Math.Clamp(_brightness, 0, 100) / 100.0);
                _overlays[name] = overlay;
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Reset();
        _topmostTimer.Dispose();
    }
}
