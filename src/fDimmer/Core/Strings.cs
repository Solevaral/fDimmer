using System.Globalization;

namespace fDimmer.Core;

public enum AppLanguage
{
    /// <summary>Язык интерфейса Windows. / Windows UI language.</summary>
    Auto,
    English,
    Russian,
}

/// <summary>
/// Строки интерфейса. Пар всего пара десятков, поэтому вместо ресурсных сборок —
/// прямые пары «английский / русский»: их видно рядом и не расходятся при правках.
/// </summary>
public static class Strings
{
    private static bool _ru;

    public static AppLanguage Language { get; private set; } = AppLanguage.Auto;

    public static void Use(AppLanguage language)
    {
        Language = language;
        _ru = language switch
        {
            AppLanguage.Russian => true,
            AppLanguage.English => false,
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
                     .Equals("ru", StringComparison.OrdinalIgnoreCase),
        };
    }

    private static string S(string en, string ru) => _ru ? ru : en;

    // ---- меню трея ----

    public static string DimmingEnabled => S("Dimming enabled", "Затемнение включено");
    public static string NoDimming => S("100% — no dimming", "100 % — без затемнения");
    public static string Engine => S("Engine", "Движок");
    public static string EngineGlobalMenu => S("Global — every window, system UI included",
                                               "Глобально — все окна, включая системные");
    public static string EngineOverlayMenu => S("Overlay — selected monitors",
                                                "Оверлей — выбранные мониторы");
    public static string OverlayMonitors => S("Overlay monitors", "Мониторы для оверлея");
    public static string AllMonitors => S("All monitors", "Все мониторы");
    public static string PrimaryMonitor => S("(primary)", "(основной)");
    public static string SettingsMenu => S("Settings…", "Настройки…");
    public static string StartWithWindows => S("Start with Windows", "Запускать с Windows");
    public static string ScheduleMenu => S("Schedule…", "Расписание…");
    public static string ScheduleEnabled => S("Schedule enabled", "Расписание включено");
    public static string LanguageMenu => S("Language", "Язык");
    public static string LanguageAuto => S("System language", "Язык системы");
    public static string Exit => S("Exit", "Выход");

    // ---- индикатор и подсказка ----

    public static string OsdBrightness => S("Screen brightness", "Яркость экрана");
    public static string OsdBrightnessOverlay => S("Brightness (overlay)", "Яркость (оверлей)");
    public static string OsdDimmingOff => S("Dimming is off", "Затемнение выключено");
    public static string EngineShortGlobal => S("global", "глобально");
    public static string EngineShortOverlay => S("overlay", "оверлей");
    public static string TooltipOff => S("fDimmer — dimming is off", "fDimmer — затемнение выключено");
    public static string Tooltip(int level, string engine) =>
        S($"fDimmer — {level}% ({engine})", $"fDimmer — {level} % ({engine})");

    // ---- окно настроек ----

    public static string SettingsTitle => S("fDimmer — Settings", "fDimmer — настройки");
    public static string SectionBrightness => S("Brightness", "Яркость");
    public static string NeverDarkerThan => S("Never darker than, %:", "Не темнее, чем, %:");
    public static string NeverDarkerHint => S("Keeps the screen from going fully dark.",
                                              "Защита от полностью погасшего экрана.");
    public static string SectionEngine => S("Dimming engine", "Движок затемнения");
    public static string EngineGlobalItem => S("Global — Alt+Tab, Start menu and taskbar included",
                                               "Глобально — включая Alt+Tab, «Пуск», панель задач");
    public static string EngineOverlayItem => S("Overlay — selected monitors only",
                                                "Оверлей — только выбранные мониторы");
    public static string EngineHint => S(
        "Only the global engine dims system UI: it applies a color matrix to the whole\n" +
        "desktop composition instead of drawing a window on top of it.",
        "Системные окна гасит только глобальный движок: он применяет цветовую\n" +
        "матрицу ко всей композиции рабочего стола, а не рисует окно поверх.");
    public static string OverlayMonitorsLabel => S("Overlay monitors (none selected = all):",
                                                   "Мониторы для оверлея (ни одного — все):");
    public static string SectionControls => S("Controls", "Управление");
    public static string TrayWheelOption => S("Mouse wheel over the tray icon changes brightness",
                                              "Колесо мыши над иконкой в трее меняет яркость");
    public static string WheelStep => S("Wheel step, %:", "Шаг колеса, %:");
    public static string ShowOsdOption => S("Show the on-screen level indicator",
                                            "Показывать индикатор уровня на экране");
    public static string RampLabel => S("Transition time, ms:", "Плавность перехода, мс:");
    public static string LanguageLabel => S("Language:", "Язык:");
    public static string Close => S("Close", "Закрыть");

    // ---- расписание ----

    public static string ScheduleTitle => S("fDimmer — Schedule", "fDimmer — расписание");
    public static string ScheduleSection => S("Schedule", "Расписание");
    public static string ScheduleUseIt => S("Change brightness on a schedule",
                                            "Менять яркость по расписанию");
    public static string ScheduleOpen => S("Schedule…", "Расписание…");
    public static string ScheduleHint => S(
        "Every point stays in effect until the next one, wrapping around midnight.\n" +
        "A manual change holds until the next point is due.",
        "Каждая точка действует до следующей, с переходом через полночь.\n" +
        "Ручное изменение держится до наступления следующей точки.");
    public static string ColumnTime => S("Time", "Время");
    public static string ColumnBrightness => S("Brightness", "Яркость");
    public static string ScheduleAdd => S("Add / update", "Добавить / обновить");
    public static string ScheduleRemove => S("Remove", "Удалить");
    public static string ScheduleOffValue => S("100% (no dimming)", "100 % (без затемнения)");
    public static string ScheduleActiveNow => S("in effect now", "действует сейчас");

    // ---- сообщения ----

    public static string AutoStartFailed(string? error) =>
        S($"Could not change autostart: {error}", $"Не удалось изменить автозапуск: {error}");

    public static string FellBackToOverlay(string? reason) =>
        S($"The global engine is unavailable, overlay enabled instead. {reason}",
          $"Глобальный движок недоступен, включён оверлей. {reason}");

    public static string SwitchedToOverlay(string? reason) =>
        S($"Switched to overlay: {reason}", $"Переключение на оверлей: {reason}");

    public static string MagInitFailed(int code) =>
        S($"MagInitialize failed (error {code}). The screen magnifier is disabled by policy or unavailable.",
          $"MagInitialize не удался (код {code}). Экранный увеличитель отключён политикой или недоступен.");

    public static string MagSetFailed(int code) =>
        S($"MagSetFullscreenColorEffect returned FALSE (error {code}).",
          $"MagSetFullscreenColorEffect вернул FALSE (код {code}).");

    public static string UnexpectedError(object details) =>
        S($"Unexpected error:\n\n{details}", $"Непредвиденная ошибка:\n\n{details}");
}
