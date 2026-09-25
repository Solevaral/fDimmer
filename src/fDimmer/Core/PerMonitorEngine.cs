namespace fDimmer.Core;

/// <summary>
/// Режим «по мониторам»: у каждого монитора своя яркость. Основной инструмент — гамма-таблица
/// монитора: она затемняет весь вывод, включая системные окна. Windows не даёт опустить гамму
/// ниже некоторого порога (обычно около 50 %), поэтому глубже монитор дотемняется окном поверх —
/// и эта часть затемнения системные окна уже не затрагивает. Экспериментально.
/// </summary>
public sealed class PerMonitorEngine : IDimEngine
{
    private readonly Dictionary<string, ushort[]> _baselines = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _floors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, double> _gammaApplied = new(StringComparer.OrdinalIgnoreCase);
    private readonly OverlayLayer _overlay = new();
    private bool _disposed;

    public EngineKind Kind => EngineKind.PerMonitor;

    // Оверлей работает всегда, так что режим доступен даже без гаммы — просто хуже.
    public bool IsAvailable => true;

    public string? UnavailableReason => null;

    public void Apply(double brightness) =>
        ApplyLevels(Screen.AllScreens.ToDictionary(s => s.DeviceName, _ => brightness));

    public void ApplyLevels(IReadOnlyDictionary<string, double> levels)
    {
        if (_disposed) return;

        var overlayLevels = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var (device, level) in levels)
        {
            var gamma = Math.Max(level, GammaFloor(device));

            if (gamma < 100 || _gammaApplied.ContainsKey(device))
            {
                if (TryApplyGamma(device, gamma)) _gammaApplied[device] = gamma;
                else gamma = 100; // гамма отказала — всё затемнение уходит в оверлей
            }

            // Итог = гамма × оверлей, значит оверлей доводит оставшуюся часть.
            overlayLevels[device] = level / gamma * 100;
        }

        _overlay.Apply(overlayLevels);
    }

    /// <summary>
    /// Самый тёмный уровень, который Windows разрешает задать гаммой на этом мониторе.
    /// 100 — гамма недоступна совсем. Определяется пробой и кэшируется.
    /// </summary>
    public int GammaFloor(string device)
    {
        if (_floors.TryGetValue(device, out var cached)) return cached;
        if (!EnsureBaseline(device, out var baseline)) return _floors[device] = 100;

        var floor = 100;
        for (var pct = Settings.HardFloor; pct < 100; pct++)
        {
            if (GammaRamp.TryWrite(device, GammaRamp.Scaled(baseline, pct / 100.0)))
            {
                floor = pct;
                break;
            }
        }

        // Проба могла сбить текущий уровень — возвращаем его.
        var current = _gammaApplied.TryGetValue(device, out var applied) ? applied : 100;
        GammaRamp.TryWrite(device, GammaRamp.Scaled(baseline, current / 100.0));

        return _floors[device] = floor;
    }

    public void Reset()
    {
        foreach (var device in _gammaApplied.Keys)
        {
            if (_baselines.TryGetValue(device, out var baseline)) GammaRamp.TryWrite(device, baseline);
        }

        _gammaApplied.Clear();
        _baselines.Clear(); // при следующем включении снимем свежие: «Ночной свет» мог поменяться
        GammaRamp.DeleteSavedBaselines();
        _overlay.Reset();
    }

    private bool TryApplyGamma(string device, double level) =>
        EnsureBaseline(device, out var baseline)
        && GammaRamp.TryWrite(device, GammaRamp.Scaled(baseline, level / 100.0));

    private bool EnsureBaseline(string device, out ushort[] baseline)
    {
        if (_baselines.TryGetValue(device, out baseline!)) return true;
        if (!GammaRamp.TryRead(device, out baseline)) return false;

        _baselines[device] = baseline;
        GammaRamp.SaveBaselines(_baselines);
        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Reset();
        _disposed = true;
        _overlay.Dispose();
    }
}
