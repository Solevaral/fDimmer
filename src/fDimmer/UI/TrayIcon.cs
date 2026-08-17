using fDimmer.Interop;
using static fDimmer.Interop.NativeMethods;

namespace fDimmer.UI;

/// <summary>
/// Иконка в области уведомлений поверх Shell_NotifyIcon вместо WinForms NotifyIcon:
/// нужны собственные hWnd и uID, чтобы спрашивать у оболочки прямоугольник иконки
/// (<see cref="Shell_NotifyIconGetRect"/>) для обработки колеса мыши.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private const int WM_TRAYCALLBACK = 0x0400 + 1; // WM_APP + 1
    private const uint IconId = 1;

    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private const int WM_RBUTTONUP = 0x0205;

    private readonly MessageWindow _window;
    private readonly uint _taskbarCreated;
    private IntPtr _hIcon;
    private string _tip = "fDimmer";
    private bool _added;
    private bool _disposed;

    public TrayIcon()
    {
        _taskbarCreated = RegisterWindowMessage("TaskbarCreated");
        _window = new MessageWindow(OnMessage);
        _hIcon = TrayIconArt.CreateHIcon(100);
        Add();
    }

    public event EventHandler? LeftClick;
    public event EventHandler? DoubleClick;
    public event EventHandler? RightClick;

    public IntPtr Handle => _window.Handle;

    public uint Uid => IconId;

    /// <summary>Перерисовывает иконку под текущий уровень и обновляет подсказку.</summary>
    public void Update(int brightness, string tip)
    {
        if (_disposed) return;

        var old = _hIcon;
        _hIcon = TrayIconArt.CreateHIcon(brightness);
        _tip = tip.Length > 127 ? tip[..127] : tip;

        var data = BuildData(NIF_ICON | NIF_TIP);
        Shell_NotifyIcon(NIM_MODIFY, ref data);

        if (old != IntPtr.Zero) DestroyIcon(old);
    }

    public void ShowBalloon(string title, string text, bool warning = false)
    {
        if (_disposed) return;

        var data = BuildData(NIF_INFO);
        data.szInfoTitle = Truncate(title, 63);
        data.szInfo = Truncate(text, 255);
        data.dwInfoFlags = (uint)(warning ? NIIF_WARNING : NIIF_INFO);
        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    private void Add()
    {
        var data = BuildData(NIF_MESSAGE | NIF_ICON | NIF_TIP);
        _added = Shell_NotifyIcon(NIM_ADD, ref data);
    }

    private NOTIFYICONDATA BuildData(uint flags) => new()
    {
        cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NOTIFYICONDATA>(),
        hWnd = _window.Handle,
        uID = IconId,
        uFlags = flags,
        uCallbackMessage = WM_TRAYCALLBACK,
        hIcon = _hIcon,
        szTip = _tip,
        szInfo = string.Empty,
        szInfoTitle = string.Empty,
    };

    private static string Truncate(string value, int max) =>
        value.Length > max ? value[..max] : value;

    private void OnMessage(ref Message m)
    {
        if (m.Msg == WM_TRAYCALLBACK)
        {
            switch ((int)m.LParam)
            {
                case WM_LBUTTONUP: LeftClick?.Invoke(this, EventArgs.Empty); break;
                case WM_LBUTTONDBLCLK: DoubleClick?.Invoke(this, EventArgs.Empty); break;
                case WM_RBUTTONUP: RightClick?.Invoke(this, EventArgs.Empty); break;
            }
        }
        else if (_taskbarCreated != 0 && m.Msg == (int)_taskbarCreated)
        {
            // Explorer перезапустился — иконку нужно добавить заново.
            Add();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_added)
        {
            var data = BuildData(0);
            Shell_NotifyIcon(NIM_DELETE, ref data);
        }

        if (_hIcon != IntPtr.Zero)
        {
            DestroyIcon(_hIcon);
            _hIcon = IntPtr.Zero;
        }

        _window.Dispose();
    }

    /// <summary>Скрытое окно-приёмник сообщений оболочки.</summary>
    private sealed class MessageWindow : NativeWindow, IDisposable
    {
        public delegate void MessageHandler(ref Message m);

        private readonly MessageHandler _handler;

        public MessageWindow(MessageHandler handler)
        {
            _handler = handler;
            // Обычное невидимое окно, а не HWND_MESSAGE: оболочка надёжнее работает
            // с полноценным окном при доставке событий иконки.
            CreateHandle(new CreateParams
            {
                Caption = "fDimmer.TrayWindow",
                Style = 0,
                ExStyle = WS_EX_TOOLWINDOW,
                X = 0,
                Y = 0,
                Width = 0,
                Height = 0,
            });
        }

        protected override void WndProc(ref Message m)
        {
            _handler(ref m);
            base.WndProc(ref m);
        }

        public void Dispose() => DestroyHandle();
    }
}

/// <summary>Рисует иконку трея — кольцо, заполненное пропорционально яркости.</summary>
internal static class TrayIconArt
{
    public static IntPtr CreateHIcon(int brightness)
    {
        var size = Math.Max(16, GetSystemMetrics(SM_CXSMICON));
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var pad = Math.Max(1f, size * 0.08f);
            var rect = new RectangleF(pad, pad, size - 2 * pad, size - 2 * pad);
            var k = Math.Clamp(brightness, 0, 100) / 100f;

            // Тёмная половина — «сколько погашено», светлая — остаток яркости.
            using var dark = new SolidBrush(Color.FromArgb(230, 32, 32, 36));
            using var light = new SolidBrush(Color.FromArgb(230,
                (int)(60 + 195 * k), (int)(60 + 195 * k), (int)(70 + 185 * k)));

            g.FillEllipse(dark, rect);
            g.FillPie(light, rect, 90, -360 * k);

            using var pen = new Pen(Color.FromArgb(200, 210, 210, 220), Math.Max(1f, size / 16f));
            g.DrawEllipse(pen, rect);
        }

        return bmp.GetHicon();
    }
}
