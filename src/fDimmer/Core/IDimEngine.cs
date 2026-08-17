namespace fDimmer.Core;

public enum EngineKind
{
    /// <summary>Цветовой эффект DWM — гасит вообще всё, включая системные окна.</summary>
    Magnification,

    /// <summary>Прозрачные чёрные окна поверх выбранных мониторов.</summary>
    Overlay,
}

/// <summary>Способ затемнения. Реализации взаимозаменяемы и переключаются на лету.</summary>
public interface IDimEngine : IDisposable
{
    EngineKind Kind { get; }

    string DisplayName { get; }

    /// <summary>False, если механизм недоступен в этой системе (политика, отсутствие API).</summary>
    bool IsAvailable { get; }

    /// <summary>Причина недоступности для показа пользователю, иначе null.</summary>
    string? UnavailableReason { get; }

    /// <summary>Применить яркость: 100 — норма, 0 — чёрный экран.</summary>
    void Apply(double brightness);

    /// <summary>Вернуть экран к исходному виду.</summary>
    void Reset();
}
