using System.Text.Json;
using System.Text.Json.Serialization;

namespace fDimmer.Core;

public sealed class Settings
{
    /// <summary>Ниже этого значения яркость не опускается ни при каких настройках.</summary>
    public const int HardFloor = 5;

    public bool Enabled { get; set; } = true;

    /// <summary>Целевая яркость в процентах: 100 — без затемнения.</summary>
    public int Brightness { get; set; } = 60;

    /// <summary>Пользовательский нижний предел, защита от «погасил экран и не вижу трей».</summary>
    public int MinBrightness { get; set; } = 15;

    /// <summary>Шаг изменения колесом мыши и пунктами меню.</summary>
    public int WheelStep { get; set; } = 5;

    public EngineKind Engine { get; set; } = EngineKind.Magnification;

    /// <summary>DeviceName мониторов для оверлей-движка. Пустой список — все мониторы.</summary>
    public List<string> OverlayMonitors { get; set; } = [];

    public bool ShowOsd { get; set; } = true;

    public bool EnableTrayWheel { get; set; } = true;

    /// <summary>Длительность плавного перехода к новому уровню, мс. 0 — мгновенно.</summary>
    public int RampMilliseconds { get; set; } = 180;

    // ---- загрузка и сохранение ----

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "fDimmer", "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.Settings);
                if (loaded is not null)
                {
                    loaded.Normalize();
                    return loaded;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Битый или недоступный файл — стартуем со значений по умолчанию.
        }

        var fresh = new Settings();
        fresh.Normalize();
        return fresh;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var json = JsonSerializer.Serialize(this, SettingsJsonContext.Default.Settings);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Настройки не критичны: продолжаем работать с текущими значениями в памяти.
        }
    }

    public void Normalize()
    {
        MinBrightness = Math.Clamp(MinBrightness, HardFloor, 90);
        Brightness = Math.Clamp(Brightness, MinBrightness, 100);
        WheelStep = Math.Clamp(WheelStep, 1, 25);
        RampMilliseconds = Math.Clamp(RampMilliseconds, 0, 2000);
        OverlayMonitors ??= [];
    }

    /// <summary>Приводит уровень к допустимому диапазону с учётом пола яркости.</summary>
    public int ClampBrightness(int value) =>
        Math.Clamp(value, Math.Max(HardFloor, MinBrightness), 100);
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(Settings))]
internal partial class SettingsJsonContext : JsonSerializerContext;
