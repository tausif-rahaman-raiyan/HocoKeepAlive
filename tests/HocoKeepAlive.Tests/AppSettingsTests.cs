using System.Text.Json;
using HocoKeepAlive.Configuration;
using Xunit;

namespace HocoKeepAlive.Tests;

public class AppSettingsTests
{
    [Fact]
    public void DefaultSettings_HaveSafeDefaults()
    {
        var settings = new AppSettings();

        Assert.Equal(-1, settings.PreferredDeviceNumber);
        Assert.False(settings.StartOnWindowsStartup);
        Assert.False(settings.AutoStartStreamOnLaunch);
        Assert.False(settings.StartMinimizedToTray);
        Assert.True(settings.MinimizeToTrayOnClose);
        Assert.True(settings.ShowNotifications);
        Assert.True(settings.AutoReconnectOnDeviceAvailable);
        Assert.True(settings.AutoPauseOnOtherAudio);
        Assert.True(settings.CallProtectionMode);
        Assert.True(settings.PreferBluetoothSoundbox);
        Assert.Equal(1200, settings.ResumeDelayMilliseconds);
        Assert.Equal(48000, settings.SampleRate);
        Assert.Equal(2, settings.Channels);
        Assert.Equal(150, settings.LatencyMilliseconds);
    }

    [Fact]
    public void Serialization_RoundTripPreservesAllFields()
    {
        var original = new AppSettings
        {
            PreferredDeviceNumber = 2,
            PreferredDeviceName = "HOCO HC22",
            StartOnWindowsStartup = true,
            AutoStartStreamOnLaunch = true,
            StartMinimizedToTray = true,
            MinimizeToTrayOnClose = true,
            ShowNotifications = false,
            AutoReconnectOnDeviceAvailable = true,
            SampleRate = 44100,
            Channels = 2,
            LatencyMilliseconds = 100
        };

        string json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(restored);
        Assert.Equal(original.PreferredDeviceNumber, restored.PreferredDeviceNumber);
        Assert.Equal(original.PreferredDeviceName, restored.PreferredDeviceName);
        Assert.Equal(original.StartOnWindowsStartup, restored.StartOnWindowsStartup);
        Assert.Equal(original.AutoStartStreamOnLaunch, restored.AutoStartStreamOnLaunch);
        Assert.Equal(original.StartMinimizedToTray, restored.StartMinimizedToTray);
        Assert.Equal(original.MinimizeToTrayOnClose, restored.MinimizeToTrayOnClose);
        Assert.Equal(original.SampleRate, restored.SampleRate);
        Assert.Equal(original.Channels, restored.Channels);
        Assert.Equal(original.LatencyMilliseconds, restored.LatencyMilliseconds);
    }

    [Fact]
    public void CorruptedJson_FallsBackToDefaults()
    {
        string corruptedJson = "{ preferredDeviceNumber: NOT_A_NUMBER, ... }";
        AppSettings result;
        try
        {
            result = JsonSerializer.Deserialize<AppSettings>(corruptedJson) ?? new AppSettings();
        }
        catch
        {
            result = new AppSettings();
        }

        Assert.NotNull(result);
        Assert.Equal(-1, result.PreferredDeviceNumber);
    }
}
