using Microsoft.Win32;

namespace fDimmer.Core;

/// <summary>
/// Применяет расписание. Уровень выставляется только в момент смены действующей точки,
/// поэтому ручное изменение яркости живёт до следующей точки, а не затирается через секунду.
/// </summary>
public sealed class Scheduler : IDisposable
{
    private readonly Settings _settings;
    private readonly DimController _controller;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly SynchronizationContext _ui;

    private ScheduleEntry? _applied;
    private bool _disposed;

    public Scheduler(Settings settings, DimController controller)
    {
        _settings = settings;
        _controller = controller;
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _timer = new System.Windows.Forms.Timer { Interval = 20_000 };
        _timer.Tick += (_, _) => Refresh();

        SystemEvents.TimeChanged += OnTimeChanged;
    }

    public void Start()
    {
        _timer.Enabled = _settings.ScheduleEnabled;
        Refresh();
    }

    /// <summary>Вызывается после правки расписания или переключения флажка.</summary>
    public void Reload()
    {
        _applied = null;
        _timer.Enabled = _settings.ScheduleEnabled;
        Refresh();
    }

    /// <summary>Точка, действующая в указанный момент суток, либо null для пустого расписания.</summary>
    public static ScheduleEntry? Resolve(IReadOnlyList<ScheduleEntry> entries, TimeOnly now)
    {
        if (entries.Count == 0) return null;

        var sorted = entries.OrderBy(e => e.Time).ToList();
        ScheduleEntry? active = null;
        foreach (var entry in sorted)
        {
            if (entry.Time <= now) active = entry;
        }

        // До первой точки суток продолжает действовать последняя точка расписания.
        return active ?? sorted[^1];
    }

    private void Refresh()
    {
        if (_disposed || !_settings.ScheduleEnabled) return;

        var entry = Resolve(_settings.Schedule, TimeOnly.FromDateTime(DateTime.Now));
        if (entry is null || entry.SameAs(_applied)) return;

        _applied = entry;

        if (entry.Brightness >= 100) _controller.SetEnabled(false);
        else _controller.SetBrightness(entry.Brightness);
    }

    private void OnTimeChanged(object? sender, EventArgs e) =>
        _ui.Post(_ => { if (!_disposed) Refresh(); }, null);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        SystemEvents.TimeChanged -= OnTimeChanged;
        _timer.Stop();
        _timer.Dispose();
    }
}
