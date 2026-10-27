using System.Diagnostics;
using HocoKeepAlive.Diagnostics;
using NAudio.Wave;

namespace HocoKeepAlive.Audio;

public enum AudioEngineState
{
    Stopped,
    Starting,
    Running,
    DeviceUnavailable,
    Recovering,
    Error,
    Exiting
}

public sealed class AudioKeepAliveService : IDisposable
{
    private readonly object _syncRoot = new();
    private WaveOutEvent? _output;
    private SilentWaveProvider? _provider;
    private AudioEngineState _state = AudioEngineState.Stopped;
    private int _targetDeviceNumber = -1;
    private string _preferredDeviceName = "Windows Default Playback Device";
    private DateTime? _startedAt;
    private DateTime? _stoppedAt;
    private string? _lastError;

    // Automatic reconnection watchdog
    private System.Threading.Timer? _reconnectTimer;
    private bool _autoReconnect = true;
    private bool _userExplicitlyStopped = false;

    public int SampleRate { get; set; } = 48000;
    public int Channels { get; set; } = 2;
    public int LatencyMs { get; set; } = 150;

    public bool AutoReconnect
    {
        get => _autoReconnect;
        set
        {
            _autoReconnect = value;
            if (!value) StopReconnectionWatchdog();
        }
    }

    public string PreferredDeviceName
    {
        get => _preferredDeviceName;
        set => _preferredDeviceName = value ?? string.Empty;
    }

    public event Action<AudioEngineState, string?>? StateChanged;

    public AudioEngineState State
    {
        get
        {
            lock (_syncRoot) return _state;
        }
    }

    public DateTime? LastStartedAt
    {
        get
        {
            lock (_syncRoot) return _startedAt;
        }
    }

    public DateTime? LastStoppedAt
    {
        get
        {
            lock (_syncRoot) return _stoppedAt;
        }
    }

    public string? LastError
    {
        get
        {
            lock (_syncRoot) return _lastError;
        }
    }

    public int TargetDeviceNumber
    {
        get
        {
            lock (_syncRoot) return _targetDeviceNumber;
        }
        set
        {
            lock (_syncRoot)
            {
                if (_targetDeviceNumber == value) return;
                _targetDeviceNumber = value;
                LogService.Log(LogSeverity.Info, "AudioEngine", $"Target playback device set to ID: {value}");
                if (_state == AudioEngineState.Running)
                {
                    RestartStreamInternal();
                }
            }
        }
    }

    public TimeSpan? RunningDuration
    {
        get
        {
            lock (_syncRoot)
            {
                if (_state != AudioEngineState.Running || _startedAt == null) return null;
                return DateTime.UtcNow - _startedAt.Value;
            }
        }
    }

    public bool Start()
    {
        lock (_syncRoot)
        {
            _userExplicitlyStopped = false;
            StopReconnectionWatchdog();

            if (_state == AudioEngineState.Running || _state == AudioEngineState.Starting)
            {
                return true;
            }

            SetState(AudioEngineState.Starting, "Initializing audio pipeline...");
            bool ok = StartStreamInternal();
            if (!ok && _autoReconnect)
            {
                StartReconnectionWatchdog();
            }
            return ok;
        }
    }

    public void Stop()
    {
        lock (_syncRoot)
        {
            _userExplicitlyStopped = true;
            StopReconnectionWatchdog();

            if (_state == AudioEngineState.Stopped || _state == AudioEngineState.Exiting)
            {
                return;
            }

            CleanupOutput();
            _stoppedAt = DateTime.UtcNow;
            SetState(AudioEngineState.Stopped, "Silent stream stopped.");
        }
    }

    public void Toggle()
    {
        lock (_syncRoot)
        {
            if (_state == AudioEngineState.Running)
            {
                Stop();
            }
            else
            {
                Start();
            }
        }
    }

    private bool StartStreamInternal()
    {
        try
        {
            CleanupOutput();

            // Try resolving by device number or by name
            int deviceToUse = _targetDeviceNumber;
            if (!AudioDeviceService.IsDeviceAvailable(deviceToUse))
            {
                var match = AudioDeviceService.FindDeviceByName(_preferredDeviceName);
                if (match != null)
                {
                    deviceToUse = match.DeviceNumber;
                    _targetDeviceNumber = deviceToUse;
                }
            }

            if (!AudioDeviceService.IsDeviceAvailable(deviceToUse))
            {
                string msg = "Selected playback device is unavailable or disconnected. Auto-reconnect is monitoring...";
                _lastError = msg;
                LogService.Log(LogSeverity.Warning, "AudioEngine", msg);
                SetState(AudioEngineState.DeviceUnavailable, msg);
                return false;
            }

            _provider = new SilentWaveProvider(SampleRate, Channels);
            _output = new WaveOutEvent
            {
                DeviceNumber = deviceToUse,
                DesiredLatency = LatencyMs,
                NumberOfBuffers = 3
            };

            _output.PlaybackStopped += OnPlaybackStopped;
            _output.Init(_provider);
            _output.Play();

            _startedAt = DateTime.UtcNow;
            _lastError = null;
            LogService.Log(LogSeverity.Info, "AudioEngine", $"Silent stream active @ {SampleRate} Hz, {Channels} ch, latency {LatencyMs} ms on device {deviceToUse}.");
            SetState(AudioEngineState.Running);
            return true;
        }
        catch (Exception ex)
        {
            CleanupOutput();
            _lastError = ex.Message;
            LogService.Log(LogSeverity.Error, "AudioEngine", "Failed to start silent audio stream.", ex.ToString());
            SetState(AudioEngineState.Error, ex.Message);
            return false;
        }
    }

    private void RestartStreamInternal()
    {
        CleanupOutput();
        StartStreamInternal();
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        lock (_syncRoot)
        {
            if (_state == AudioEngineState.Exiting || _state == AudioEngineState.Stopped)
            {
                return;
            }

            if (e.Exception != null)
            {
                _lastError = e.Exception.Message;
                LogService.Log(LogSeverity.Warning, "AudioEngine", "Playback endpoint disconnected: " + e.Exception.Message);
                SetState(AudioEngineState.Recovering, "Device disconnected. Auto-reconnecting...");

                if (_autoReconnect && !_userExplicitlyStopped)
                {
                    StartReconnectionWatchdog();
                }
                else
                {
                    SetState(AudioEngineState.Error, e.Exception.Message);
                }
            }
            else
            {
                SetState(AudioEngineState.Stopped);
            }
        }
    }

    private void StartReconnectionWatchdog()
    {
        StopReconnectionWatchdog();
        LogService.Log(LogSeverity.Info, "AudioEngine", "Auto-reconnection watchdog active. Polling for device...");
        _reconnectTimer = new System.Threading.Timer(OnReconnectTimerTick, null, 1500, 2000);
    }

    private void StopReconnectionWatchdog()
    {
        try
        {
            _reconnectTimer?.Dispose();
            _reconnectTimer = null;
        }
        catch { }
    }

    private void OnReconnectTimerTick(object? state)
    {
        lock (_syncRoot)
        {
            if (_userExplicitlyStopped || _state == AudioEngineState.Running || _state == AudioEngineState.Exiting)
            {
                StopReconnectionWatchdog();
                return;
            }

            // Check if device is now available
            var match = AudioDeviceService.FindDeviceByName(_preferredDeviceName);
            bool isAvailable = match != null || AudioDeviceService.IsDeviceAvailable(_targetDeviceNumber);

            if (isAvailable)
            {
                if (match != null)
                {
                    _targetDeviceNumber = match.DeviceNumber;
                }

                LogService.Log(LogSeverity.Info, "AudioEngine", "Target audio device detected! Resuming keep-alive stream automatically.");
                bool started = StartStreamInternal();
                if (started)
                {
                    StopReconnectionWatchdog();
                }
            }
        }
    }

    /// <summary>
    /// Launches Windows Bluetooth settings to pair if needed, and fast-polls to auto-connect immediately.
    /// </summary>
    public void LaunchAutoPairAssistant()
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = "ms-settings:bluetooth", UseShellExecute = true });
            LogService.Log(LogSeverity.Info, "Bluetooth", "Opened Windows Bluetooth settings for pairing.");
        }
        catch (Exception ex)
        {
            LogService.Log(LogSeverity.Warning, "Bluetooth", "Could not open Bluetooth settings: " + ex.Message);
        }

        // Fast poll for the paired device
        _userExplicitlyStopped = false;
        StartReconnectionWatchdog();
    }

    private void CleanupOutput()
    {
        try
        {
            if (_output != null)
            {
                _output.PlaybackStopped -= OnPlaybackStopped;
                try { _output.Stop(); } catch { }
                _output.Dispose();
                _output = null;
            }
        }
        catch { }
        finally
        {
            _provider = null;
        }
    }

    private void SetState(AudioEngineState newState, string? message = null)
    {
        _state = newState;

        var handlers = StateChanged;
        if (handlers == null) return;

        foreach (Action<AudioEngineState, string?> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(newState, message);
            }
            catch (Exception ex)
            {
                try
                {
                    LogService.Log(LogSeverity.Warning, "AudioEngine",
                        "A state-change subscriber failed; audio engine will continue.", ex.ToString());
                }
                catch { }
            }
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            SetState(AudioEngineState.Exiting);
            StopReconnectionWatchdog();
            CleanupOutput();
        }
    }
}
