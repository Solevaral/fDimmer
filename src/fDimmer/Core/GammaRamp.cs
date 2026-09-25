using fDimmer.Interop;

namespace fDimmer.Core;

/// <summary>
/// Чтение и запись гамма-таблицы монитора. В отличие от цветового эффекта DWM, гамма
/// переживает завершение процесса, поэтому исходные таблицы сохраняются на диск перед
/// первым изменением и восстанавливаются при следующем запуске, если прошлый сеанс
/// не успел вернуть их сам.
/// </summary>
internal static class GammaRamp
{
    public const int Length = 768; // 256 значений на каждый из каналов R, G, B

    private static string BaselineFile => Path.Combine(
        Path.GetDirectoryName(Settings.FilePath)!, "gamma-baseline.bin");

    public static bool TryRead(string device, out ushort[] ramp)
    {
        ramp = new ushort[Length];
        var hdc = NativeMethods.CreateDC("DISPLAY", device, null, IntPtr.Zero);
        if (hdc == IntPtr.Zero) return false;
        try { return NativeMethods.GetDeviceGammaRamp(hdc, ramp); }
        finally { NativeMethods.DeleteDC(hdc); }
    }

    public static bool TryWrite(string device, ushort[] ramp)
    {
        var hdc = NativeMethods.CreateDC("DISPLAY", device, null, IntPtr.Zero);
        if (hdc == IntPtr.Zero) return false;
        try { return NativeMethods.SetDeviceGammaRamp(hdc, ramp); }
        finally { NativeMethods.DeleteDC(hdc); }
    }

    /// <summary>Исходная таблица, умноженная на k. Сохраняет калибровку и «Ночной свет».</summary>
    public static ushort[] Scaled(ushort[] baseline, double k)
    {
        var result = new ushort[Length];
        for (var i = 0; i < Length; i++)
        {
            result[i] = (ushort)Math.Clamp(Math.Round(baseline[i] * k), 0, ushort.MaxValue);
        }
        return result;
    }

    public static void SaveBaselines(IReadOnlyDictionary<string, ushort[]> baselines)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BaselineFile)!);
            using var writer = new BinaryWriter(File.Create(BaselineFile));
            writer.Write(baselines.Count);
            foreach (var (device, ramp) in baselines)
            {
                writer.Write(device);
                foreach (var value in ramp) writer.Write(value);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Без файла восстановление после аварии не сработает, но само затемнение — да.
        }
    }

    public static void DeleteSavedBaselines()
    {
        try { File.Delete(BaselineFile); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Возвращает мониторам сохранённые исходные таблицы. Вызывается при старте и из
    /// аварийных обработчиков: если fDimmer убили, экран не останется затемнённым навсегда.
    /// </summary>
    public static void RestoreSavedBaselines()
    {
        try
        {
            if (!File.Exists(BaselineFile)) return;

            using (var reader = new BinaryReader(File.OpenRead(BaselineFile)))
            {
                var count = reader.ReadInt32();
                for (var n = 0; n < count; n++)
                {
                    var device = reader.ReadString();
                    var ramp = new ushort[Length];
                    for (var i = 0; i < Length; i++) ramp[i] = reader.ReadUInt16();
                    TryWrite(device, ramp);
                }
            }

            File.Delete(BaselineFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EndOfStreamException)
        {
            DeleteSavedBaselines();
        }
    }
}
