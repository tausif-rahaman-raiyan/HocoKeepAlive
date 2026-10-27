using HocoKeepAlive.Audio;
using Xunit;

namespace HocoKeepAlive.Tests;

public class SilentWaveProviderTests
{
    [Fact]
    public void WaveFormat_MatchesExpectedSpecs()
    {
        var provider = new SilentWaveProvider(48000, 2);

        Assert.Equal(48000, provider.WaveFormat.SampleRate);
        Assert.Equal(16, provider.WaveFormat.BitsPerSample);
        Assert.Equal(2, provider.WaveFormat.Channels);
    }

    [Fact]
    public void Read_FillsBufferWithDigitalZeros()
    {
        var provider = new SilentWaveProvider();
        byte[] buffer = new byte[1024];

        // Seed with non-zero bytes to ensure provider clears them
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (byte)(i % 255 + 1);
        }

        int bytesRead = provider.Read(buffer, 0, buffer.Length);

        Assert.Equal(buffer.Length, bytesRead);
        Assert.All(buffer, b => Assert.Equal(0, b));
    }

    [Fact]
    public void Read_RespectsOffsetAndCount()
    {
        var provider = new SilentWaveProvider();
        byte[] buffer = new byte[100];
        Array.Fill(buffer, (byte)0xFF);

        int offset = 20;
        int count = 60;

        int bytesRead = provider.Read(buffer, offset, count);

        Assert.Equal(count, bytesRead);

        // Bytes before offset should be untouched
        for (int i = 0; i < offset; i++)
        {
            Assert.Equal((byte)0xFF, buffer[i]);
        }

        // Bytes inside range should be cleared to 0
        for (int i = offset; i < offset + count; i++)
        {
            Assert.Equal(0, buffer[i]);
        }

        // Bytes after range should be untouched
        for (int i = offset + count; i < buffer.Length; i++)
        {
            Assert.Equal((byte)0xFF, buffer[i]);
        }
    }

    [Fact]
    public void Read_ThrowsOnInvalidParameters()
    {
        var provider = new SilentWaveProvider();
        byte[] buffer = new byte[50];

        Assert.Throws<ArgumentNullException>(() => provider.Read(null!, 0, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => provider.Read(buffer, -1, 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => provider.Read(buffer, 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => provider.Read(buffer, 45, 10)); // exceeds buffer
    }
}
