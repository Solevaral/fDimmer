using fDimmer.Interop;

namespace fDimmer.UI;

/// <summary>
/// Чёрное click-through окно во весь монитор. Не активируется, не попадает в Alt+Tab
/// и в панель задач. Границы выставляются напрямую через SetWindowPos в физических
/// пикселях, чтобы обойти пересчёт координат по DPI в WinForms.
/// </summary>
internal sealed class DimOverlayForm : Form
{
    private Rectangle _targetBounds;

    public DimOverlayForm(Rectangle monitorBounds)
    {
        _targetBounds = monitorBounds;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        BackColor = Color.Black;
        Opacity = 0;
        TopMost = true;
        Cursor = Cursors.Default;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_LAYERED
                        | NativeMethods.WS_EX_TRANSPARENT
                        | NativeMethods.WS_EX_NOACTIVATE
                        | NativeMethods.WS_EX_TOOLWINDOW
                        | NativeMethods.WS_EX_TOPMOST;
            return cp;
        }
    }

    public void SetBounds(Rectangle monitorBounds)
    {
        _targetBounds = monitorBounds;
        ApplyBounds();
    }

    /// <summary>Возвращает окно наверх — иначе оно проваливается под чужие topmost-окна.</summary>
    public void BringToTop()
    {
        if (!IsHandleCreated) return;
        NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST, 0, 0, 0, 0,
            NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
    }

    public void SetDimAmount(double amount)
    {
        Opacity = Math.Clamp(amount, 0, 1);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyBounds();
    }

    private void ApplyBounds()
    {
        if (!IsHandleCreated) return;
        NativeMethods.SetWindowPos(Handle, NativeMethods.HWND_TOPMOST,
            _targetBounds.X, _targetBounds.Y, _targetBounds.Width, _targetBounds.Height,
            NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_SHOWWINDOW);
    }
}
