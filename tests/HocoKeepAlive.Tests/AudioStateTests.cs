using HocoKeepAlive.Audio;
using Xunit;

namespace HocoKeepAlive.Tests;

public class AudioStateTests
{
    [Fact]
    public void Service_InitialStateIsStopped()
    {
        using var service = new AudioKeepAliveService();
        Assert.Equal(AudioEngineState.Stopped, service.State);
        Assert.Null(service.RunningDuration);
    }

    [Fact]
    public void Service_RepeatedStopCallsAreSafe()
    {
        using var service = new AudioKeepAliveService();
        service.Stop();
        service.Stop();
        service.Stop();
        Assert.Equal(AudioEngineState.Stopped, service.State);
    }

    [Fact]
    public void Service_DisposeTransitionsToExiting()
    {
        var service = new AudioKeepAliveService();
        service.Dispose();
        Assert.Equal(AudioEngineState.Exiting, service.State);
    }

    [Fact]
    public void Service_SetTargetDeviceUpdatesProperty()
    {
        using var service = new AudioKeepAliveService();
        service.TargetDeviceNumber = 3;
        Assert.Equal(3, service.TargetDeviceNumber);
    }
}
