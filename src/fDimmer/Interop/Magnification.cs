using System.Runtime.InteropServices;

namespace fDimmer.Interop;

/// <summary>
/// P/Invoke к magnification.dll. Полноэкранный цветовой эффект применяется к итоговой
/// композиции DWM, поэтому затрагивает всё содержимое рабочего стола: окна любых процессов
/// (в том числе запущенных от администратора), Alt+Tab, меню «Пуск», панель задач,
/// уведомления и все мониторы сразу.
/// </summary>
internal static class Magnification
{
    private const string Dll = "magnification.dll";

    /// <summary>Цветовая матрица 5x5 в порядке row-major (RGBA + смещение).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct MAGCOLOREFFECT
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 25)]
        public float[] transform;
    }

    [DllImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool MagInitialize();

    [DllImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool MagUninitialize();

    [DllImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool MagSetFullscreenColorEffect(ref MAGCOLOREFFECT pEffect);

    [DllImport(Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool MagGetFullscreenColorEffect(ref MAGCOLOREFFECT pEffect);

    /// <summary>Единичная матрица — исходные цвета экрана.</summary>
    internal static MAGCOLOREFFECT Identity() => Scale(1.0);

    /// <summary>
    /// Матрица равномерного масштабирования RGB. <paramref name="k"/> = 1 — без изменений,
    /// 0 — чёрный экран. Альфа и смещение остаются нетронутыми.
    /// </summary>
    internal static MAGCOLOREFFECT Scale(double k)
    {
        var f = (float)k;
        var m = new float[25];
        m[0] = f;   // R -> R
        m[6] = f;   // G -> G
        m[12] = f;  // B -> B
        m[18] = 1f; // A -> A
        m[24] = 1f; // однородная координата
        return new MAGCOLOREFFECT { transform = m };
    }
}
