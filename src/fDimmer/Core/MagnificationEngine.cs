using System.Runtime.InteropServices;
using fDimmer.Interop;

namespace fDimmer.Core;

/// <summary>
/// Затемнение через полноэкранный цветовой эффект DWM. Это единственный способ погасить
/// поверхности, живущие в системных window bands: переключатель Alt+Tab, меню «Пуск»,
/// панель задач и её флайауты, всплывающие уведомления. Работает на всех мониторах разом
/// и не требует UIAccess.
/// </summary>
public sealed class MagnificationEngine : IDimEngine
{
    private bool _initialized;
    private bool _disposed;

    public MagnificationEngine()
    {
        // MagInitialize нужно звать на потоке с насосом сообщений — это UI-поток приложения.
        if (Magnification.MagInitialize())
        {
            _initialized = true;
        }
        else
        {
            UnavailableReason = Strings.MagInitFailed(Marshal.GetLastWin32Error());
        }
    }

    public EngineKind Kind => EngineKind.Magnification;

    public bool IsAvailable => _initialized && UnavailableReason is null;

    public string? UnavailableReason { get; private set; }

    public void Apply(double brightness)
    {
        if (!_initialized || _disposed) return;

        var k = Math.Clamp(brightness, 0, 100) / 100.0;
        var effect = Magnification.Scale(k);

        if (!Magnification.MagSetFullscreenColorEffect(ref effect))
        {
            // Эффект может отвалиться при смене сеанса или конфликте с «Цветовыми фильтрами».
            UnavailableReason = Strings.MagSetFailed(Marshal.GetLastWin32Error());
            return;
        }

        UnavailableReason = null;
    }

    public void Reset()
    {
        if (!_initialized || _disposed) return;

        var identity = Magnification.Identity();
        Magnification.MagSetFullscreenColorEffect(ref identity);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_initialized)
        {
            var identity = Magnification.Identity();
            Magnification.MagSetFullscreenColorEffect(ref identity);
            Magnification.MagUninitialize();
            _initialized = false;
        }
    }
}
