namespace fDimmer.Core;

/// <summary>
/// Точка расписания: с указанного времени и до следующей точки держится заданная яркость.
/// Сутки замкнуты — до первой точки дня действует последняя точка расписания.
/// </summary>
public sealed class ScheduleEntry
{
    public TimeOnly Time { get; set; }

    /// <summary>Яркость в процентах: 100 — затемнение выключено.</summary>
    public int Brightness { get; set; } = 60;

    public bool SameAs(ScheduleEntry? other) =>
        other is not null && other.Time == Time && other.Brightness == Brightness;
}
