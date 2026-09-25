namespace fDimmer.Core;

public enum EngineKind
{
    /// <summary>Общий режим: цветовой эффект DWM, одна яркость на всё, включая системные окна.</summary>
    Magnification,

    /// <summary>Устаревшее значение из версий до 1.1: при загрузке настроек превращается в PerMonitor.</summary>
    Overlay,

    /// <summary>У каждого монитора своя яркость. Экспериментально.</summary>
    PerMonitor,
}

/// <summary>Способ затемнения. Реализации взаимозаменяемы и переключаются на лету.</summary>
public interface IDimEngine : IDisposable
{
    EngineKind Kind { get; }

    /// <summary>False, если механизм недоступен в этой системе (политика, отсутствие API).</summary>
    bool IsAvailable { get; }

    /// <summary>Причина недоступности для показа пользователю, иначе null.</summary>
    string? UnavailableReason { get; }

    /// <summary>Применить одну яркость ко всему: 100 — норма, 0 — чёрный экран.</summary>
    void Apply(double brightness);

    /// <summary>Вернуть экран к исходному виду.</summary>
    void Reset();
}
