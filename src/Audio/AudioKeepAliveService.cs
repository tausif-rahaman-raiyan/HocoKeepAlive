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
    private DateTime? _startedAt;
    private DateTime? _stoppedAt;
    private string? _lastError;

    public int SampleRate { get; set; } = 48000;
    public int Channels { get; set; } = 2;
    public int LatencyMs { get; set; } = 150;

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
            if (_state == AudioEngineState.Running || _state == AudioEngineState.Starting)
            {
                return true;
            }

            SetState(AudioEngineState.Starting, "Initializing audio pipeline...");
            return StartStreamInternal();
        }
    }

    public void Stop()
    {
        lock (_syncRoot)
        {
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

            if (!AudioDeviceService.IsDeviceAvailable(_targetDeviceNumber))
            {
                string msg = "Selected playback device is unavailable or disconnected.";
                _lastError = msg;
                LogService.Log(LogSeverity.Warning, "AudioEngine", msg);
                SetState(AudioEngineState.DeviceUnavailable, msg);
                return false;
            }

            _provider = new SilentWaveProvider(SampleRate, Channels);
            _output = new WaveOutEvent
            {
                DeviceNumber = _targetDeviceNumber,
                DesiredLatency = LatencyMs,
                NumberOfBuffers = 3
            };

            _output.PlaybackStopped += OnPlaybackStopped;
            _output.Init(_provider);
            _output.Play();

            _startedAt = DateTime.UtcNow;
            _lastError = null;
            LogService.Log(LogSeverity.Info, "AudioEngine", $"Silent stream active @ {SampleRate} Hz, {Channels} ch, latency {LatencyMs} ms on device {_targetDeviceNumber}.");
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
                LogService.Log(LogSeverity.Error, "AudioEngine", "Audio playback stopped unexpectedly.", e.Exception.Message);
                SetState(AudioEngineState.Error, e.Exception.Message);
            }
            else
            {
                SetState(AudioEngineState.Stopped);
            }
        }
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

        // One faulty UI/plugin subscriber must not break audio lifecycle transitions.
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
                        "A state-change subscriber failed; the audio engine will continue.", ex.ToString());
                }
                catch
                {
                    // Diagnostics must never interrupt audio playback or cleanup.
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            SetState(AudioEngineState.Exiting);
            CleanupOutput();
        }
    }
}
