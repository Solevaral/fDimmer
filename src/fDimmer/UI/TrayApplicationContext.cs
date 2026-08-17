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

    private readonly Scheduler _scheduler;

    private SettingsForm? _settingsForm;
    private ScheduleForm? _scheduleForm;
    private bool _disposed;

    public TrayApplicationContext()
    {
        _settings = Settings.Load();
        Strings.Use(_settings.Language);

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

        _scheduler = new Scheduler(_settings, _controller);

        _controller.Start();
        _scheduler.Start();
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

        var toggle = new ToolStripMenuItem(Strings.DimmingEnabled)
        {
            Checked = _controller.IsEnabled,
            CheckOnClick = true,
        };
        toggle.Click += (_, _) => { _controller.Toggle(); ShowOsd(); };
        _menu.Items.Add(toggle);

        _menu.Items.Add(new ToolStripSeparator());

        foreach (var preset in Presets)
        {
            var item = new ToolStripMenuItem(preset == 100 ? Strings.NoDimming : $"{preset}%")
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

        var schedule = new ToolStripMenuItem(Strings.ScheduleEnabled)
        {
            Checked = _settings.ScheduleEnabled,
            CheckOnClick = true,
        };
        schedule.Click += (_, _) =>
        {
            _settings.ScheduleEnabled = schedule.Checked;
            _settings.Save();
            _scheduler.Reload();
        };
        _menu.Items.Add(schedule);

        var scheduleEditor = new ToolStripMenuItem(Strings.ScheduleMenu);
        scheduleEditor.Click += (_, _) => OpenSchedule();
        _menu.Items.Add(scheduleEditor);

        _menu.Items.Add(new ToolStripSeparator());

        var settings = new ToolStripMenuItem(Strings.SettingsMenu);
        settings.Click += (_, _) => OpenSettings();
        _menu.Items.Add(settings);

        _menu.Items.Add(BuildLanguageMenu());

        var autoStart = new ToolStripMenuItem(Strings.StartWithWindows)
        {
            Checked = AutoStart.IsEnabled,
            CheckOnClick = true,
        };
        autoStart.Click += (_, _) =>
        {
            if (!AutoStart.TrySet(autoStart.Checked, out var error))
            {
                _tray.ShowBalloon("fDimmer", Strings.AutoStartFailed(error), warning: true);
            }
        };
        _menu.Items.Add(autoStart);

        _menu.Items.Add(new ToolStripSeparator());

        var exit = new ToolStripMenuItem(Strings.Exit);
        exit.Click += (_, _) => ExitThread();
        _menu.Items.Add(exit);
    }

    private ToolStripMenuItem BuildEngineMenu()
    {
        var root = new ToolStripMenuItem(Strings.Engine);

        var global = new ToolStripMenuItem(Strings.EngineGlobalMenu)
        {
            Checked = _controller.ActiveEngine.Kind == EngineKind.Magnification,
        };
        global.Click += (_, _) => _controller.SetEngine(EngineKind.Magnification);
        root.DropDownItems.Add(global);

        var overlay = new ToolStripMenuItem(Strings.EngineOverlayMenu)
        {
            Checked = _controller.ActiveEngine.Kind == EngineKind.Overlay,
        };
        overlay.Click += (_, _) => _controller.SetEngine(EngineKind.Overlay);
        root.DropDownItems.Add(overlay);

        return root;
    }

    private ToolStripMenuItem BuildMonitorsMenu()
    {
        var root = new ToolStripMenuItem(Strings.OverlayMonitors)
        {
            Enabled = _controller.ActiveEngine.Kind == EngineKind.Overlay,
        };

        var all = new ToolStripMenuItem(Strings.AllMonitors)
        {
            Checked = _settings.OverlayMonitors.Count == 0,
        };
        all.Click += (_, _) => _controller.SetOverlayMonitors([]);
        root.DropDownItems.Add(all);
        root.DropDownItems.Add(new ToolStripSeparator());

        foreach (var screen in Screen.AllScreens)
        {
            var name = screen.DeviceName;
            var item = new ToolStripMenuItem(MonitorCaption(screen))
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

    internal static string MonitorCaption(Screen screen) =>
        $"{screen.DeviceName}  {screen.Bounds.Width}×{screen.Bounds.Height}" +
        (screen.Primary ? $"  {Strings.PrimaryMonitor}" : string.Empty);

    private ToolStripMenuItem BuildLanguageMenu()
    {
        var root = new ToolStripMenuItem(Strings.LanguageMenu);

        foreach (var (language, caption) in new[]
        {
            (AppLanguage.Auto, Strings.LanguageAuto),
            (AppLanguage.English, "English"),
            (AppLanguage.Russian, "Русский"),
        })
        {
            var item = new ToolStripMenuItem(caption) { Checked = _settings.Language == language };
            var value = language;
            item.Click += (_, _) => ApplyLanguage(value);
            root.DropDownItems.Add(item);
        }

        return root;
    }

    private void ApplyLanguage(AppLanguage language)
    {
        if (_settings.Language == language) return;

        _settings.Language = language;
        Strings.Use(language);
        _settings.Save();
        UpdateTray();

        // Меню пересобирается при открытии, а окна построены разом — открываем заново.
        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.Close();
            OpenSettings();
        }

        if (_scheduleForm is { IsDisposed: false })
        {
            _scheduleForm.Close();
            OpenSchedule();
        }
    }

    private void OpenSchedule()
    {
        if (_scheduleForm is { IsDisposed: false })
        {
            _scheduleForm.Activate();
            return;
        }

        _scheduleForm = new ScheduleForm(_settings);
        _scheduleForm.ScheduleChanged += (_, _) => _scheduler.Reload();
        _scheduleForm.FormClosed += (_, _) => _scheduleForm = null;
        _scheduleForm.Show();
        _scheduleForm.Activate();
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
        _settingsForm.LanguageChanged += (_, language) => ApplyLanguage(language);
        _settingsForm.ScheduleRequested += (_, _) => OpenSchedule();
        _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        _settingsForm.Show();
        _settingsForm.Activate();
    }

    // ---- обратная связь ----

    private void ShowOsd()
    {
        if (!_settings.ShowOsd) return;

        var caption = _controller.IsEnabled
            ? _controller.ActiveEngine.Kind == EngineKind.Magnification
                ? Strings.OsdBrightness
                : Strings.OsdBrightnessOverlay
            : Strings.OsdDimmingOff;
        _osd.ShowLevel(_controller.TargetBrightness, caption);
    }

    private void UpdateTray()
    {
        var level = _controller.TargetBrightness;
        var engine = _controller.ActiveEngine.Kind == EngineKind.Magnification
            ? Strings.EngineShortGlobal
            : Strings.EngineShortOverlay;
        var tip = _controller.IsEnabled ? Strings.Tooltip(level, engine) : Strings.TooltipOff;
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
            _scheduler.Dispose();
            _wheel.Dispose();
            _controller.ResetScreen();
            _controller.Dispose();
            _tray.Dispose();
            _menu.Dispose();
            _osd.Dispose();
            _settingsForm?.Dispose();
            _scheduleForm?.Dispose();
        }

        base.Dispose(disposing);
    }
}
