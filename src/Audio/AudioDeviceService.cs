using NAudio.Wave;

namespace HocoKeepAlive.Audio;

public sealed record AudioDeviceInfo(int DeviceNumber, string Name, bool IsDefault);

public static class AudioDeviceService
{
    /// <summary>
    /// Enumerates all available WaveOut playback endpoints.
    /// </summary>
    public static List<AudioDeviceInfo> GetPlaybackDevices()
    {
        var devices = new List<AudioDeviceInfo>
        {
            new(-1, "Windows Default Playback Device", true)
        };

        try
        {
            int count = WaveOut.DeviceCount;
            for (int i = 0; i < count; i++)
            {
                var caps = WaveOut.GetCapabilities(i);
                string name = caps.ProductName;
                devices.Add(new AudioDeviceInfo(i, name, false));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to enumerate audio devices: {ex.Message}");
        }

        return devices;
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
    /// Looks for a matching device by name (e.g. "HOCO", "HC22", or preferred name).
    /// </summary>
    public static AudioDeviceInfo? FindDeviceByName(string? preferredName)
    {
        if (string.IsNullOrWhiteSpace(preferredName)) return null;

        var devices = GetPlaybackDevices();
        // Exact match
        var match = devices.FirstOrDefault(d => string.Equals(d.Name, preferredName, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;

        // Partial match
        return devices.FirstOrDefault(d => d.Name.Contains(preferredName, StringComparison.OrdinalIgnoreCase) ||
                                           (preferredName.Contains("HOCO", StringComparison.OrdinalIgnoreCase) && d.Name.Contains("HOCO", StringComparison.OrdinalIgnoreCase)));
    }
}
