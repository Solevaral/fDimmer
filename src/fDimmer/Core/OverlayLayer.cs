using fDimmer.UI;

namespace fDimmer.Core;

/// <summary>
/// Чёрные click-through окна поверх мониторов, у каждого своя прозрачность. Системные окна
/// (Alt+Tab, «Пуск», панель задач) такое окно не перекрывает, поэтому слой используется
/// только как добавка: дотемнить монитор там, куда не дотягивается гамма.
/// </summary>
internal sealed class OverlayLayer : IDisposable
{
    private readonly Dictionary<string, DimOverlayForm> _overlays = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Windows.Forms.Timer _topmostTimer;
    private bool _disposed;

    public OverlayLayer()
    {
        // Чужие topmost-окна периодически перекрывают оверлей — возвращаем его наверх.
        _topmostTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _topmostTimer.Tick += (_, _) =>
        {
            foreach (var overlay in _overlays.Values) overlay.BringToTop();
        };
    }

    /// <summary>Яркость по мониторам (DeviceName → 0..100). 100 и отсутствующие — без окна.</summary>
    public void Apply(IReadOnlyDictionary<string, double> brightnessByDevice)
    {
        if (_disposed) return;

        var screens = Screen.AllScreens.ToDictionary(s => s.DeviceName, StringComparer.OrdinalIgnoreCase);

        foreach (var device in _overlays.Keys.ToList())
        {
            var keep = screens.ContainsKey(device)
                       && brightnessByDevice.TryGetValue(device, out var b) && b < 99.5;
            if (keep) continue;

            _overlays[device].Dispose();
            _overlays.Remove(device);
        }

        foreach (var (device, brightness) in brightnessByDevice)
        {
            if (brightness >= 99.5 || !screens.TryGetValue(device, out var screen)) continue;

            if (!_overlays.TryGetValue(device, out var overlay))
            {
                overlay = new DimOverlayForm(screen.Bounds);
                overlay.Show();
                _overlays[device] = overlay;
            }
            else
            {
                overlay.SetBounds(screen.Bounds);
            }

            overlay.SetDimAmount(1.0 - Math.Clamp(brightness, 0, 100) / 100.0);
        }

        _topmostTimer.Enabled = _overlays.Count > 0;
    }

    public void Reset()
    {
        _topmostTimer.Enabled = false;
        foreach (var overlay in _overlays.Values)
        {
            overlay.Hide();
            overlay.Dispose();
        }
        _overlays.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Reset();
        _topmostTimer.Dispose();
    }
}
