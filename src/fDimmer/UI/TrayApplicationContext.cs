using fDimmer.Core;

namespace fDimmer.UI;

/// <summary>Склейка: иконка в трее, меню, хук колеса, OSD и контроллер затемнения.</summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private static readonly int[] Presets = [100, 75, 50, 35, 20];

    private readonly Settings _settings;
    private readonly DimController _controller;
    private readonly TrayIcon _tray;
    private readonly TrayWheelHook _wheel;
    private readonly OsdForm _osd = new();
    private readonly ContextMenuStrip _menu = new();

    private SettingsForm? _settingsForm;
    private bool _disposed;

    public TrayApplicationContext()
    {
        _settings = Settings.Load();
        _controller = new DimController(_settings);
        _controller.Notice += OnNotice;
        _controller.StateChanged += (_, _) => UpdateTray();

        _tray = new TrayIcon();
        _tray.LeftClick += (_, _) => ShowMenu();
        _tray.RightClick += (_, _) => ShowMenu();

        _wheel = new TrayWheelHook(_tray.Handle, _tray.Uid);
        _wheel.Scrolled += OnWheelScrolled;
        if (_settings.EnableTrayWheel) _wheel.Install();

        _menu.Opening += (_, _) => BuildMenu();
        _menu.Font = new Font("Segoe UI", 9f);

        _controller.Start();
        UpdateTray();
    }

    // ---- реакция на ввод ----

    private void OnWheelScrolled(object? sender, int direction)
    {
        _controller.Nudge(direction * _settings.WheelStep);
        ShowOsd();
    }

    private void ShowMenu()
    {
        // Без вывода окна на передний план меню не закроется по клику мимо него.
        Interop.NativeMethods.SetForegroundWindow(_tray.Handle);
        _menu.Show(Cursor.Position);
    }

    private void BuildMenu()
    {
        _menu.Items.Clear();

        var toggle = new ToolStripMenuItem("Затемнение включено")
        {
            Checked = _controller.IsEnabled,
            CheckOnClick = true,
        };
        toggle.Click += (_, _) => { _controller.Toggle(); ShowOsd(); };
        _menu.Items.Add(toggle);

        _menu.Items.Add(new ToolStripSeparator());

        foreach (var preset in Presets)
        {
            var item = new ToolStripMenuItem(preset == 100 ? "100 % — без затемнения" : $"{preset} %")
            {
                Checked = _controller.IsEnabled && _controller.TargetBrightness == preset,
                Enabled = preset >= _settings.MinBrightness || preset == 100,
            };
            var value = preset;
            item.Click += (_, _) =>
            {
                if (value == 100) _controller.SetEnabled(false);
                else _controller.SetBrightness(value);
                ShowOsd();
            };
            _menu.Items.Add(item);
        }

        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(BuildEngineMenu());
        _menu.Items.Add(BuildMonitorsMenu());

        _menu.Items.Add(new ToolStripSeparator());

        var settings = new ToolStripMenuItem("Настройки…");
        settings.Click += (_, _) => OpenSettings();
        _menu.Items.Add(settings);

        var autoStart = new ToolStripMenuItem("Запускать с Windows")
        {
            Checked = AutoStart.IsEnabled,
            CheckOnClick = true,
        };
        autoStart.Click += (_, _) =>
        {
            if (!AutoStart.TrySet(autoStart.Checked, out var error))
            {
                _tray.ShowBalloon("fDimmer", $"Не удалось изменить автозапуск: {error}", warning: true);
            }
        };
        _menu.Items.Add(autoStart);

        _menu.Items.Add(new ToolStripSeparator());

        var exit = new ToolStripMenuItem("Выход");
        exit.Click += (_, _) => ExitThread();
        _menu.Items.Add(exit);
    }

    private ToolStripMenuItem BuildEngineMenu()
    {
        var root = new ToolStripMenuItem("Движок");

        var global = new ToolStripMenuItem("Глобально — все окна, включая системные")
        {
            Checked = _controller.ActiveEngine.Kind == EngineKind.Magnification,
        };
        global.Click += (_, _) => _controller.SetEngine(EngineKind.Magnification);
        root.DropDownItems.Add(global);

        var overlay = new ToolStripMenuItem("Оверлей — выбранные мониторы")
        {
            Checked = _controller.ActiveEngine.Kind == EngineKind.Overlay,
        };
        overlay.Click += (_, _) => _controller.SetEngine(EngineKind.Overlay);
        root.DropDownItems.Add(overlay);

        return root;
    }

    private ToolStripMenuItem BuildMonitorsMenu()
    {
        var root = new ToolStripMenuItem("Мониторы для оверлея")
        {
            Enabled = _controller.ActiveEngine.Kind == EngineKind.Overlay,
        };

        var all = new ToolStripMenuItem("Все мониторы")
        {
            Checked = _settings.OverlayMonitors.Count == 0,
        };
        all.Click += (_, _) => _controller.SetOverlayMonitors([]);
        root.DropDownItems.Add(all);
        root.DropDownItems.Add(new ToolStripSeparator());

        foreach (var screen in Screen.AllScreens)
        {
            var name = screen.DeviceName;
            var item = new ToolStripMenuItem(
                $"{name}  {screen.Bounds.Width}×{screen.Bounds.Height}{(screen.Primary ? "  (основной)" : "")}")
            {
                Checked = _settings.OverlayMonitors.Contains(name),
            };
            item.Click += (_, _) =>
            {
                var selected = new List<string>(_settings.OverlayMonitors);
                if (!selected.Remove(name)) selected.Add(name);
                _controller.SetOverlayMonitors(selected);
            };
            root.DropDownItems.Add(item);
        }

        return root;
    }

    private void OpenSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Activate();
            return;
        }

        _settingsForm = new SettingsForm(_controller);
        _settingsForm.TrayWheelToggled += (_, enabled) =>
        {
            if (enabled) _wheel.Install();
            else _wheel.Uninstall();
        };
        _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    // ---- обратная связь ----

    private void ShowOsd()
    {
        if (!_settings.ShowOsd) return;

        var caption = _controller.IsEnabled
            ? _controller.ActiveEngine.Kind == EngineKind.Magnification ? "Яркость экрана" : "Яркость (оверлей)"
            : "Затемнение выключено";
        _osd.ShowLevel(_controller.TargetBrightness, caption);
    }

    private void UpdateTray()
    {
        var level = _controller.TargetBrightness;
        var engine = _controller.ActiveEngine.Kind == EngineKind.Magnification ? "глобально" : "оверлей";
        var tip = _controller.IsEnabled
            ? $"fDimmer — {level} % ({engine})"
            : "fDimmer — затемнение выключено";
        _tray.Update(level, tip);
    }

    private void OnNotice(object? sender, string message) =>
        _tray.ShowBalloon("fDimmer", message, warning: true);

    // ---- завершение ----

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;

            _settings.Save();
            _wheel.Dispose();
            _controller.ResetScreen();
            _controller.Dispose();
            _tray.Dispose();
            _menu.Dispose();
            _osd.Dispose();
            _settingsForm?.Dispose();
        }

        base.Dispose(disposing);
    }
}
