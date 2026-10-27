using NAudio.Wave;

namespace HocoKeepAlive.Audio;

/// <summary>
/// Provides a continuous 48,000 Hz, 16-bit stereo stream of digital silence (zeroed PCM buffers).
/// Extremely low CPU and memory overhead; does not allocate buffers dynamically in the callback.
/// </summary>
public sealed class SilentWaveProvider : IWaveProvider
{
    public WaveFormat WaveFormat { get; }

    public SilentWaveProvider(int sampleRate = 48000, int channels = 2)
    {
        WaveFormat = new WaveFormat(sampleRate, 16, channels);
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        if (offset < 0 || count < 0 || offset + count > buffer.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), "Invalid buffer offset or count specified.");
        }

        // Fill buffer slice with digital silence (zeros)
        Array.Clear(buffer, offset, count);
        return count;
    }
}
