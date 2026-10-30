using System.Text.Json;
using HocoKeepAlive.Diagnostics;
using Microsoft.Win32;

namespace HocoKeepAlive.Configuration;

public sealed class AppSettings
{
    private const string RunRegistrySubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppRegistryName = "Keep Alive";

    public int PreferredDeviceNumber { get; set; } = -1;
    public string PreferredDeviceName { get; set; } = "Windows Default Playback Device";
    public bool StartOnWindowsStartup { get; set; } = false;
    public bool AutoStartStreamOnLaunch { get; set; } = false;
    public bool StartMinimizedToTray { get; set; } = false;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;
    public bool NotifyOnStartStop { get; set; } = true;
    public bool NotifyOnError { get; set; } = true;
    public bool AutoReconnectOnDeviceAvailable { get; set; } = true;
    public bool AutoPauseOnOtherAudio { get; set; } = false;
    public bool AlwaysKeepStreamActive { get; set; } = true;
    public bool CallProtectionMode { get; set; } = true;
    public bool PreferBluetoothSoundbox { get; set; } = true;
    public string LastKnownBluetoothDevice { get; set; } = "Bluetooth Wireless Speaker";
    public int ResumeDelayMilliseconds { get; set; } = 1200;
    public int SampleRate { get; set; } = 48000;
    public int Channels { get; set; } = 2;
    public int LatencyMilliseconds { get; set; } = 150;
    public int ReconnectionRetryIntervalSeconds { get; set; } = 5;
    public int MaxReconnectAttempts { get; set; } = 5;
    public string Theme { get; set; } = "DarkNavy";

    private static string GetConfigFilePath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string folder = Path.Combine(appData, "KeepAlive");
        Directory.CreateDirectory(folder);

        string newConfigPath = Path.Combine(folder, "settings.json");
        if (!File.Exists(newConfigPath))
        {
            // Backwards compatibility migration from previous data directory if present
            string legacyFolder = Path.Combine(appData, "HocoKeepAlive");
            string legacyPath = Path.Combine(legacyFolder, "settings.json");
            if (File.Exists(legacyPath))
            {
                try { File.Copy(legacyPath, newConfigPath, true); } catch { }
            }
        }

        return newConfigPath;
    }

    public static AppSettings Load()
    {
        string path = GetConfigFilePath();
        if (!File.Exists(path))
        {
            var initial = new AppSettings();
            initial.Save();
            return initial;
        }

        try
        {
            string json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            if (settings != null) return settings;
        }
        catch (Exception ex)
        {
            LogService.Log(LogSeverity.Warning, "Settings", "Failed to deserialize settings. Restoring defaults.", ex.Message);
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            string path = GetConfigFilePath();
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            LogService.Log(LogSeverity.Info, "Settings", "Preferences saved to disk.");
        }
        catch (Exception ex)
        {
            LogService.Log(LogSeverity.Error, "Settings", "Failed to write settings file.", ex.Message);
        }

        UpdateWindowsStartupRegistry(StartOnWindowsStartup);
    }

    public static void UpdateWindowsStartupRegistry(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunRegistrySubKey, true);
            if (key == null) return;

            string exePath = Application.ExecutablePath;
            if (enable)
            {
                key.SetValue(AppRegistryName, $"\"{exePath}\" --minimized");
                LogService.Log(LogSeverity.Info, "Startup", "Registered Keep Alive in Windows Startup.");
            }
            else
            {
                if (key.GetValue(AppRegistryName) != null)
                {
                    key.DeleteValue(AppRegistryName, false);
                    LogService.Log(LogSeverity.Info, "Startup", "Removed Keep Alive from Windows Startup.");
                }
            }
        }
        catch (Exception ex)
        {
            LogService.Log(LogSeverity.Warning, "Startup", "Failed to modify Windows startup registry.", ex.Message);
        }
    }
}
