using System.Diagnostics;
using HocoKeepAlive.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace HocoKeepAlive.Audio;

public enum AudioEngineState
{
    Stopped,
    Starting,
    Running,
    PausedByOtherAudio,
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

    // Smart auto-pause when user plays music / video (and auto-resume when paused)
    private System.Threading.Timer? _activityWatchdogTimer;
    private MMDeviceEnumerator? _mmEnumerator;
    private MMDevice? _currentMmDevice;
    private DateTime _lastOtherAudioDetectedUtc = DateTime.MinValue;
    private bool _autoPauseOnOtherAudio = true;
    private int _resumeDelayMilliseconds = 1200;

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

    public bool AutoPauseOnOtherAudio
    {
        get => _autoPauseOnOtherAudio;
        set
        {
            lock (_syncRoot)
            {
                _autoPauseOnOtherAudio = value;
                if (!value && _state == AudioEngineState.PausedByOtherAudio)
                {
                    // Resume immediately if auto-pause was toggled off while paused
                    ResumeKeepAliveStream();
                }
            }
        }
    }

    public int ResumeDelayMilliseconds
    {
        get => _resumeDelayMilliseconds;
        set => _resumeDelayMilliseconds = Math.Max(300, Math.Min(10000, value));
    }

    public string PreferredDeviceName
    {
        get => _preferredDeviceName;
        set
        {
            _preferredDeviceName = value ?? string.Empty;
            RefreshCurrentMmDevice();
        }
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
                RefreshCurrentMmDevice();
                LogService.Log(LogSeverity.Info, "AudioEngine", $"Target playback device set to ID: {value}");
                if (_state == AudioEngineState.Running || _state == AudioEngineState.PausedByOtherAudio)
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
                if ((_state != AudioEngineState.Running && _state != AudioEngineState.PausedByOtherAudio) || _startedAt == null)
                    return null;
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

            if (_state == AudioEngineState.Running || _state == AudioEngineState.PausedByOtherAudio || _state == AudioEngineState.Starting)
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
            StopActivityWatchdog();

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
            if (_state == AudioEngineState.Running || _state == AudioEngineState.PausedByOtherAudio)
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
            RefreshCurrentMmDevice();

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

            // Check if other audio is ALREADY playing before starting
            float initialPeak = GetMasterPeakValue();
            if (_autoPauseOnOtherAudio && initialPeak > 0.003f)
            {
                _startedAt = DateTime.UtcNow;
                _lastError = null;
                _lastOtherAudioDetectedUtc = DateTime.UtcNow;
                LogService.Log(LogSeverity.Info, "AudioEngine", $"Other audio currently active (peak {initialPeak:F3}). Placing keep-alive in standby.");
                SetState(AudioEngineState.PausedByOtherAudio, "Other audio detected. Keep-Alive standing by in background.");
            }
            else
            {
                _output.Play();
                _startedAt = DateTime.UtcNow;
                _lastError = null;
                LogService.Log(LogSeverity.Info, "AudioEngine", $"Silent stream active @ {SampleRate} Hz, {Channels} ch, latency {LatencyMs} ms on device {deviceToUse}.");
                SetState(AudioEngineState.Running);
            }

            StartActivityWatchdog();
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
            else if (_state != AudioEngineState.PausedByOtherAudio)
            {
                SetState(AudioEngineState.Stopped);
            }
        }
    }

    #region Smart Audio Activity Watchdog (Auto-Pause / Auto-Resume)

    private void StartActivityWatchdog()
    {
        StopActivityWatchdog();
        // Check audio peak every 200 ms with ultra-low CPU overhead
        _activityWatchdogTimer = new System.Threading.Timer(OnActivityWatchdogTick, null, 250, 200);
    }

    private void StopActivityWatchdog()
    {
        try
        {
            _activityWatchdogTimer?.Dispose();
            _activityWatchdogTimer = null;
        }
        catch { }
    }

    private void OnActivityWatchdogTick(object? state)
    {
        lock (_syncRoot)
        {
            if (_userExplicitlyStopped || _state == AudioEngineState.Stopped || _state == AudioEngineState.Exiting)
            {
                StopActivityWatchdog();
                return;
            }

            if (!_autoPauseOnOtherAudio)
            {
                if (_state == AudioEngineState.PausedByOtherAudio)
                {
                    ResumeKeepAliveStream();
                }
                return;
            }

            float peak = GetMasterPeakValue();

            // Threshold: 0.003f (0.3% amplitude). Digital silence from our provider is strictly 0.000f.
            if (peak > 0.003f)
            {
                _lastOtherAudioDetectedUtc = DateTime.UtcNow;

                if (_state == AudioEngineState.Running)
                {
                    try
                    {
                        _output?.Pause();
                    }
                    catch { }

                    LogService.Log(LogSeverity.Info, "AudioEngine", $"Other audio playing (peak: {peak:F3}). Paused background silent stream.");
                    SetState(AudioEngineState.PausedByOtherAudio, "Other audio detected. Background silent stream paused.");
                }
            }
            else
            {
                // Silence detected
                if (_state == AudioEngineState.PausedByOtherAudio)
                {
                    var silenceDurationMs = (DateTime.UtcNow - _lastOtherAudioDetectedUtc).TotalMilliseconds;
                    if (silenceDurationMs >= _resumeDelayMilliseconds)
                    {
                        // Other audio has stopped / paused! Resume silent keep-alive stream automatically
                        ResumeKeepAliveStream();
                    }
                }
            }
        }
    }

    private void ResumeKeepAliveStream()
    {
        try
        {
            if (_output != null)
            {
                _output.Play();
            }
            else
            {
                StartStreamInternal();
                return;
            }

            LogService.Log(LogSeverity.Info, "AudioEngine", "Other audio paused or stopped. Automatically resumed silent keep-alive stream.");
            SetState(AudioEngineState.Running, "Silent keep-alive stream resumed.");
        }
        catch (Exception ex)
        {
            LogService.Log(LogSeverity.Warning, "AudioEngine", "Could not resume audio stream: " + ex.Message);
            RestartStreamInternal();
        }
    }

    private float GetMasterPeakValue()
    {
        try
        {
            if (_currentMmDevice == null)
            {
                RefreshCurrentMmDevice();
            }

            return _currentMmDevice?.AudioMeterInformation?.MasterPeakValue ?? 0f;
        }
        catch
        {
            _currentMmDevice = null;
            return 0f;
        }
    }

    private void RefreshCurrentMmDevice()
    {
        try
        {
            _mmEnumerator ??= new MMDeviceEnumerator();
            if (_targetDeviceNumber == -1 || string.IsNullOrWhiteSpace(_preferredDeviceName) ||
                _preferredDeviceName.Contains("Default", StringComparison.OrdinalIgnoreCase))
            {
                _currentMmDevice = _mmEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                return;
            }

            var endpoints = _mmEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            foreach (var ep in endpoints)
            {
                if (ep.FriendlyName.Contains(_preferredDeviceName, StringComparison.OrdinalIgnoreCase))
                {
                    _currentMmDevice = ep;
                    return;
                }
            }

            _currentMmDevice = _mmEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        }
        catch
        {
            _currentMmDevice = null;
        }
    }

    #endregion

    #region Auto-Reconnection Watchdog

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
            if (_userExplicitlyStopped || _state == AudioEngineState.Running || _state == AudioEngineState.PausedByOtherAudio || _state == AudioEngineState.Exiting)
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

        _userExplicitlyStopped = false;
        StartReconnectionWatchdog();
    }

    #endregion

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
            StopActivityWatchdog();
            CleanupOutput();

            try
            {
                _currentMmDevice?.Dispose();
                _currentMmDevice = null;
                _mmEnumerator?.Dispose();
                _mmEnumerator = null;
            }
            catch { }
        }
    }
}
