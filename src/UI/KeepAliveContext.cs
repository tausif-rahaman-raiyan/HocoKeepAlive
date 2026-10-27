using System.Reflection;
using HocoKeepAlive.Audio;
using HocoKeepAlive.Configuration;
using HocoKeepAlive.Diagnostics;

namespace HocoKeepAlive.UI;

public sealed class KeepAliveContext : ApplicationContext
{
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _toggleItem;
    private readonly AudioKeepAliveService _audioService;
    private readonly AppSettings _settings;
    private readonly Icon _appIcon;
    private readonly Control _uiDispatcher;
    private MainForm? _mainForm;
    private bool _isExiting;

    public KeepAliveContext(bool startMinimized)
    {
        _settings = AppSettings.Load();
        // Own a WinForms handle so audio callbacks can safely marshal tray updates to the UI thread.
        _uiDispatcher = new Control();
        _ = _uiDispatcher.Handle;
        _audioService = new AudioKeepAliveService
        {
            TargetDeviceNumber = _settings.PreferredDeviceNumber,
            PreferredDeviceName = _settings.PreferredDeviceName,
            AutoReconnect = _settings.AutoReconnectOnDeviceAvailable,
            SampleRate = _settings.SampleRate,
            Channels = _settings.Channels,
            LatencyMs = _settings.LatencyMilliseconds
        };

        _appIcon = LoadAppIcon();

        // System Tray Context Menu — Exactly three options:
        // 1. Open Dashboard
        // 2. Start Keep-Alive / Stop Keep-Alive
        // 3. Exit
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Open Dashboard", null, (_, _) => ShowPage("Overview")));

        _toggleItem = new ToolStripMenuItem("Start Keep-Alive", null, (_, _) => _audioService.Toggle())
        {
            Font = new Font(Control.DefaultFont, FontStyle.Bold)
        };
        menu.Items.Add(_toggleItem);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => ExitApp()));

        _tray = new NotifyIcon
        {
            Icon = _appIcon,
            Text = "HOCO KeepAlive — Stopped",
            ContextMenuStrip = menu,
            Visible = true
        };

        // Double click toggles keep-alive stream
        _tray.DoubleClick += (_, _) => _audioService.Toggle();

        _audioService.StateChanged += OnAudioStateChanged;

        if (_settings.ShowNotifications)
        {
            _tray.ShowBalloonTip(2000, "HOCO KeepAlive", "Ready in notification area. Right-click or double-click to start.", ToolTipIcon.Info);
        }

        if (_settings.AutoStartStreamOnLaunch)
        {
            _audioService.Start();
        }

        if (!startMinimized && !_settings.StartMinimizedToTray)
        {
            ShowPage("Overview");
        }

        LogService.Log(LogSeverity.Info, "Lifecycle", "HOCO KeepAlive context initialized.");
    }

    private static Icon LoadAppIcon()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("HocoKeepAlive.assets.hoco-keepalive.ico");
            if (stream != null) return new Icon(stream);

            var exeIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (exeIcon != null) return exeIcon;
        }
        catch { }

        return SystemIcons.Application;
    }

    public void ShowPage(string page)
    {
        if (_mainForm == null || _mainForm.IsDisposed)
        {
            _mainForm = new MainForm(_audioService, _settings, _appIcon, ExitApp);
        }

        _mainForm.NavigateTo(page);
        _mainForm.Show();
        _mainForm.WindowState = FormWindowState.Normal;
        _mainForm.BringToFront();
        _mainForm.Activate();
    }

    private void OnAudioStateChanged(AudioEngineState state, string? message)
    {
        if (_isExiting || _uiDispatcher.IsDisposed || !_uiDispatcher.IsHandleCreated) return;

        if (_uiDispatcher.InvokeRequired)
        {
            try
            {
                _uiDispatcher.BeginInvoke(new Action(() => OnAudioStateChanged(state, message)));
            }
            catch (InvalidOperationException) { /* The UI is shutting down. */ }
            return;
        }

        if (_isExiting) return;
        switch (state)
        {
            case AudioEngineState.Running:
                _toggleItem.Text = "Stop Keep-Alive";
                _tray.Text = "HOCO KeepAlive — Running";
                if (_settings.ShowNotifications && _settings.NotifyOnStartStop)
                {
                    _tray.ShowBalloonTip(1500, "HOCO KeepAlive", "Silent stream active. Bluetooth audio connection is maintained.", ToolTipIcon.Info);
                }
                break;

            case AudioEngineState.Recovering:
                _toggleItem.Text = "Stop Keep-Alive";
                _tray.Text = "HOCO KeepAlive — Auto-reconnecting";
                break;

            case AudioEngineState.DeviceUnavailable:
                _toggleItem.Text = "Start Keep-Alive";
                _tray.Text = "HOCO KeepAlive — Unavailable";
                if (_settings.ShowNotifications && _settings.NotifyOnError)
                {
                    _tray.ShowBalloonTip(2000, "HOCO KeepAlive", "Selected device is unavailable. Please check your Bluetooth connection.", ToolTipIcon.Warning);
                }
                break;

            case AudioEngineState.Error:
                _toggleItem.Text = "Start Keep-Alive";
                _tray.Text = "HOCO KeepAlive — Error";
                if (_settings.ShowNotifications && _settings.NotifyOnError)
                {
                    _tray.ShowBalloonTip(2500, "HOCO KeepAlive", $"Audio error: {message ?? "Unknown error"}", ToolTipIcon.Error);
                }
                break;

            default:
                _toggleItem.Text = "Start Keep-Alive";
                _tray.Text = "HOCO KeepAlive — Stopped";
                if (_settings.ShowNotifications && _settings.NotifyOnStartStop && state == AudioEngineState.Stopped)
                {
                    _tray.ShowBalloonTip(1200, "HOCO KeepAlive", "Silent stream stopped.", ToolTipIcon.Info);
                }
                break;
        }
    }

    private void ExitApp()
    {
        if (_isExiting) return;
        _isExiting = true;

        LogService.Log(LogSeverity.Info, "Lifecycle", "Application exiting cleanly.");

        _audioService.Stop();
        _audioService.Dispose();

        _audioService.StateChanged -= OnAudioStateChanged;
        _tray.Visible = false;
        _tray.Dispose();

        _mainForm?.Close();
        _mainForm?.Dispose();

        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _audioService.StateChanged -= OnAudioStateChanged;
            _audioService.Dispose();
            _uiDispatcher.Dispose();
            _tray.Dispose();
            _mainForm?.Dispose();
        }
        base.Dispose(disposing);
    }
}
