using NAudio.Wave;

namespace HocoKeepAlive.Audio;

public sealed record AudioDeviceInfo(int DeviceNumber, string Name, bool IsDefault, bool IsBluetooth = false);

public static class AudioDeviceService
{
    private static readonly string[] BluetoothKeywords =
    {
        "bluetooth", "wireless", "hands-free", "headset",
        "earbud", "soundbox", "speaker", "a2dp", "bt"
    };

    /// <summary>
    /// Checks whether a device name suggests a Bluetooth audio soundbox, speaker, or headset.
    /// </summary>
    public static bool IsBluetoothDevice(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return BluetoothKeywords.Any(k => name.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Enumerates all available WaveOut playback endpoints with Bluetooth classification.
    /// </summary>
    public static List<AudioDeviceInfo> GetPlaybackDevices()
    {
        var devices = new List<AudioDeviceInfo>
        {
            new(-1, "Windows Default Playback Device", true, false)
        };

        try
        {
            int count = WaveOut.DeviceCount;
            for (int i = 0; i < count; i++)
            {
                var caps = WaveOut.GetCapabilities(i);
                string name = caps.ProductName ?? $"WaveOut Device #{i}";
                bool isBt = IsBluetoothDevice(name);
                devices.Add(new AudioDeviceInfo(i, name, false, isBt));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to enumerate audio devices: {ex.Message}");
        }

        return devices;
    }

    /// <summary>
    /// Finds the best matching Bluetooth soundbox or speaker.
    /// </summary>
    public static AudioDeviceInfo? FindBluetoothSoundbox(string? preferredName = null)
    {
        var devices = GetPlaybackDevices().Where(d => d.DeviceNumber != -1).ToList();

        // 1. Try finding explicit preferred Bluetooth name if specified
        if (!string.IsNullOrWhiteSpace(preferredName))
        {
            var match = devices.FirstOrDefault(d => d.Name.Contains(preferredName, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
        }

        // 2. Try finding any Bluetooth soundbox / speaker
        var bt = devices.FirstOrDefault(d => d.IsBluetooth);
        if (bt != null) return bt;

        return null;
    }

    /// <summary>
    /// Checks if a device number is currently available.
    /// </summary>
    public static bool IsDeviceAvailable(int deviceNumber)
    {
        if (deviceNumber == -1) return true; // Default mapper always exists if audio subsystem is up
        try
        {
            return deviceNumber >= 0 && deviceNumber < WaveOut.DeviceCount;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Looks for a matching device by name (e.g. preferred name).
    /// </summary>
    public static AudioDeviceInfo? FindDeviceByName(string? preferredName)
    {
        if (string.IsNullOrWhiteSpace(preferredName)) return null;

        var devices = GetPlaybackDevices();
        // Exact match
        var match = devices.FirstOrDefault(d => string.Equals(d.Name, preferredName, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        // Partial match
        return devices.FirstOrDefault(d => d.Name.Contains(preferredName, StringComparison.OrdinalIgnoreCase));
    }
}

