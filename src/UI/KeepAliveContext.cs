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
    private DateTime _lastRunningToastUtc = DateTime.MinValue;
    private DateTime _lastUnavailableToastUtc = DateTime.MinValue;
    private DateTime _lastErrorToastUtc = DateTime.MinValue;
    private DateTime _lastStopToastUtc = DateTime.MinValue;
    private AudioEngineState _previousEngineState = AudioEngineState.Stopped;

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
            AutoPauseOnOtherAudio = _settings.AutoPauseOnOtherAudio,
            AlwaysKeepStreamActive = _settings.AlwaysKeepStreamActive,
            CallProtectionMode = _settings.CallProtectionMode,
            PreferBluetoothSoundbox = _settings.PreferBluetoothSoundbox,
            ResumeDelayMilliseconds = _settings.ResumeDelayMilliseconds,
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
        menu.Items.Add(new ToolStripMenuItem("Open Dashboard", null, (_, _) => ShowPage("Dashboard")));

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
            Text = "Keep Alive — Stopped",
            ContextMenuStrip = menu,
            Visible = true
        };

        // Single left-click or double-click on the hidden system-tray icon opens Dashboard immediately
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowPage("Dashboard");
            }
        };
        _tray.DoubleClick += (_, _) => ShowPage("Dashboard");

        _audioService.StateChanged += OnAudioStateChanged;

        if (_settings.ShowNotifications)
        {
            _tray.ShowBalloonTip(2000, "Keep Alive", "Ready in notification area. Right-click or double-click to start.", ToolTipIcon.Info);
        }

        if (_settings.AutoStartStreamOnLaunch)
        {
            _audioService.Start();
        }

        if (!startMinimized && !_settings.StartMinimizedToTray)
        {
            ShowPage("Dashboard");
        }

        LogService.Log(LogSeverity.Info, "Lifecycle", "Keep Alive context initialized.");
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

        bool stateChanged = (_previousEngineState != state);
        var now = DateTime.UtcNow;

        switch (state)
        {
            case AudioEngineState.Running:
                _toggleItem.Text = "Stop Keep-Alive";
                _tray.Text = "Keep Alive — Running";
                // Only notify when transitioning from Stopped, not when resuming from normal media playback or standby,
                // and rate limit to at most once every 60 seconds to prevent balloon spam.
                if (_settings.ShowNotifications && _settings.NotifyOnStartStop)
                {
                    if (_previousEngineState == AudioEngineState.Stopped && (now - _lastRunningToastUtc).TotalSeconds > 60)
                    {
                        _lastRunningToastUtc = now;
                        _tray.ShowBalloonTip(1500, "Keep Alive", "Silent stream active. Bluetooth audio connection is maintained.", ToolTipIcon.Info);
                    }
                }
                break;

            case AudioEngineState.PausedByOtherAudio:
                _toggleItem.Text = "Stop Keep-Alive";
                _tray.Text = "Keep Alive — Running (Media active)";
                // Never show disruptive toast when media starts or pauses
                break;

            case AudioEngineState.PausedByCallOrMic:
                _toggleItem.Text = "Stop Keep-Alive";
                _tray.Text = "Keep Alive — Standby (Call/Mic active)";
                break;

            case AudioEngineState.Recovering:
                _toggleItem.Text = "Stop Keep-Alive";
                _tray.Text = "Keep Alive — Auto-reconnecting";
                break;

            case AudioEngineState.DeviceUnavailable:
                _toggleItem.Text = "Start Keep-Alive";
                _tray.Text = "Keep Alive — Device Disconnected";
                if (_settings.ShowNotifications && _settings.NotifyOnError && stateChanged)
                {
                    if ((now - _lastUnavailableToastUtc).TotalSeconds > 60)
                    {
                        _lastUnavailableToastUtc = now;
                        _tray.ShowBalloonTip(2000, "Keep Alive", "Bluetooth speaker disconnected. Auto-reconnect is monitoring...", ToolTipIcon.Warning);
                    }
                }
                break;

            case AudioEngineState.Error:
                _toggleItem.Text = "Start Keep-Alive";
                _tray.Text = "Keep Alive — Error";
                if (_settings.ShowNotifications && _settings.NotifyOnError && stateChanged)
                {
                    if ((now - _lastErrorToastUtc).TotalSeconds > 60)
                    {
                        _lastErrorToastUtc = now;
                        _tray.ShowBalloonTip(2500, "Keep Alive", $"Audio error: {message ?? "Unknown error"}", ToolTipIcon.Error);
                    }
                }
                break;

            default:
                _toggleItem.Text = "Start Keep-Alive";
                _tray.Text = "Keep Alive — Stopped";
                if (_settings.ShowNotifications && _settings.NotifyOnStartStop && state == AudioEngineState.Stopped && stateChanged)
                {
                    if ((now - _lastStopToastUtc).TotalSeconds > 30)
                    {
                        _lastStopToastUtc = now;
                        _tray.ShowBalloonTip(1200, "Keep Alive", "Silent stream stopped.", ToolTipIcon.Info);
                    }
                }
                break;
        }

        _previousEngineState = state;
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
