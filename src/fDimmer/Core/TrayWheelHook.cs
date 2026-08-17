using fDimmer.Interop;
using static fDimmer.Interop.NativeMethods;

namespace fDimmer.Core;

/// <summary>
/// Прокрутка колеса над иконкой в трее меняет яркость. Оболочка не пересылает
/// WM_MOUSEWHEEL иконке, поэтому событие ловится низкоуровневым хуком мыши.
/// Прямоугольник иконки обновляется по таймеру, а не внутри хука: Shell_NotifyIconGetRect —
/// вызов в процесс оболочки, а колбэк хука обязан отрабатывать мгновенно.
/// </summary>
public sealed class TrayWheelHook : IDisposable
{
    private readonly HookProc _proc;
    private readonly System.Windows.Forms.Timer _rectTimer;
    private readonly IntPtr _iconWindow;
    private readonly uint _iconId;

    private IntPtr _hook;
    private RECT _iconRect;
    private bool _rectValid;
    private bool _disposed;

    public TrayWheelHook(IntPtr iconWindow, uint iconId)
    {
        _iconWindow = iconWindow;
        _iconId = iconId;
        _proc = HookCallback;

        _rectTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _rectTimer.Tick += (_, _) => RefreshRect();
    }

    /// <summary>Положительное значение — колесо вверх (экран светлее).</summary>
    public event EventHandler<int>? Scrolled;

    public bool IsInstalled => _hook != IntPtr.Zero;

    public void Install()
    {
        if (_hook != IntPtr.Zero || _disposed) return;

        RefreshRect();
        _rectTimer.Start();
        _hook = SetWindowsHookEx(WH_MOUSE_LL, _proc, GetModuleHandle(null), 0);
    }

    public void Uninstall()
    {
        _rectTimer.Stop();
        if (_hook == IntPtr.Zero) return;
        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private void RefreshRect()
    {
        var id = new NOTIFYICONIDENTIFIER
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NOTIFYICONIDENTIFIER>(),
            hWnd = _iconWindow,
            uID = _iconId,
        };

        // S_OK == 0. Если иконка спрятана в переполнении, оболочка вернёт ошибку —
        // тогда колесо просто не обрабатывается.
        _rectValid = Shell_NotifyIconGetRect(ref id, out _iconRect) == 0;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (int)wParam == WM_MOUSEWHEEL && _rectValid)
        {
            var data = System.Runtime.InteropServices.Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            if (_iconRect.Contains(data.pt))
            {
                var delta = (short)(data.mouseData >> 16);
                Scrolled?.Invoke(this, Math.Sign(delta));
                return 1; // событие поглощено, под курсором ничего не прокрутится
            }
        }

        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Uninstall();
        _rectTimer.Dispose();
    }
}
