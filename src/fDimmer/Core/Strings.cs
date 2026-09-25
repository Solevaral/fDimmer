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
    public static string Mode => S("Mode", "Режим");
    public static string ModeGlobalMenu => S("Common — one level for everything, system UI included",
                                             "Общий — одна яркость на всё, включая системные окна");
    public static string ModePerMonitorMenu => S("Per monitor — each its own level (experimental)",
                                                 "По мониторам — у каждого своя (экспериментально)");
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
    public static string MonitorNumber(int n) => S($"Monitor {n}", $"Монитор {n}");
    public static string OsdDimmingOff => S("Dimming is off", "Затемнение выключено");
    public static string ModeShortGlobal => S("common", "общий");
    public static string ModeShortPerMonitor => S("per monitor", "по мониторам");
    public static string TooltipOff => S("fDimmer — dimming is off", "fDimmer — затемнение выключено");
    public static string Tooltip(int level, string engine) =>
        S($"fDimmer — {level}% ({engine})", $"fDimmer — {level} % ({engine})");

    // ---- окно настроек ----

    public static string SettingsTitle => S("fDimmer — Settings", "fDimmer — настройки");
    public static string SectionBrightness => S("Brightness", "Яркость");
    public static string NeverDarkerThan => S("Never darker than, %:", "Не темнее, чем, %:");
    public static string NeverDarkerHint => S("Keeps the screen from going fully dark.",
                                              "Защита от полностью погасшего экрана.");
    public static string SectionMode => S("Mode", "Режим");
    public static string ModeGlobalItem => S("Common — one level, Alt+Tab, Start menu and taskbar included",
                                             "Общий — одна яркость, включая Alt+Tab, «Пуск», панель задач");
    public static string ModePerMonitorItem => S("Per monitor — each monitor its own level (experimental)",
                                                 "По мониторам — у каждого своя яркость (экспериментально)");
    public static string SectionPerMonitor => S("Per-monitor brightness", "Яркость по мониторам");
    public static string PerMonitorOnlyHint => S("Available in the per-monitor mode.",
                                                 "Работает в режиме «По мониторам».");
    public static string PerMonitorWarning(int floor) => S(
        $"Experimental, may misbehave. Down to about {floor}% a monitor is dimmed through its gamma,\n" +
        "system UI included; darker than that is added by a window on top, which does not cover\n" +
        "Alt+Tab or the Start menu. Conflicts with Night Light and does nothing in HDR.",
        $"Экспериментально, может работать некорректно. До ~{floor} % монитор темнеет через гамму,\n" +
        "включая системные окна; темнее — окном поверх, которое не перекрывает Alt+Tab и «Пуск».\n" +
        "Конфликтует с «Ночным светом», в HDR не работает.");
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

    public static string FellBackToPerMonitor(string? reason) =>
        S($"The common mode is unavailable, per-monitor mode enabled instead. {reason}",
          $"Общий режим недоступен, включён режим «По мониторам». {reason}");

    public static string SwitchedToPerMonitor(string? reason) =>
        S($"Switched to per-monitor mode: {reason}", $"Переключение на режим «По мониторам»: {reason}");

    public static string PerMonitorNotice => S(
        "Per-monitor mode is experimental and may misbehave: below about 50% Alt+Tab and the Start menu stop getting darker.",
        "Режим «По мониторам» экспериментальный и может работать некорректно: ниже ~50 % Alt+Tab и «Пуск» дальше не темнеют.");

    public static string MagInitFailed(int code) =>
        S($"MagInitialize failed (error {code}). The screen magnifier is disabled by policy or unavailable.",
          $"MagInitialize не удался (код {code}). Экранный увеличитель отключён политикой или недоступен.");

    public static string MagSetFailed(int code) =>
        S($"MagSetFullscreenColorEffect returned FALSE (error {code}).",
          $"MagSetFullscreenColorEffect вернул FALSE (код {code}).");

    public static string UnexpectedError(object details) =>
        S($"Unexpected error:\n\n{details}", $"Непредвиденная ошибка:\n\n{details}");
}
