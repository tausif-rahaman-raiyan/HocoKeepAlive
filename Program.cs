using System.Drawing;
using System.Windows.Forms;
using NAudio.Wave;

namespace HocoKeepAlive;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new KeepAliveContext());
    }
}

internal sealed class KeepAliveContext : ApplicationContext
{
    private readonly NotifyIcon tray;
    private readonly ToolStripMenuItem toggleItem;
    private WaveOutEvent? output;
    private SilentWaveProvider? provider;
    private bool running;
    private bool exiting;

    public KeepAliveContext()
    {
        var menu = new ContextMenuStrip();
        toggleItem = new ToolStripMenuItem("Start silent keep-alive", null, (_, _) => Toggle());
        menu.Items.Add(new ToolStripMenuItem("HOCO KeepAlive 1.0.0") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(toggleItem);
        menu.Items.Add(new ToolStripMenuItem("Battery-friendly guidance", null, (_, _) =>
            MessageBox.Show("A continuous silent audio stream can keep Bluetooth active, but it may use more speaker battery than an idle connection. Stop keep-alive whenever you do not need it. No software can guarantee zero battery drain while maintaining an active Bluetooth audio link.",
                "HOCO KeepAlive", MessageBoxButtons.OK, MessageBoxIcon.Information)));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => Exit()));
        tray = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "HOCO KeepAlive — stopped",
            ContextMenuStrip = menu,
            Visible = true
        };
        tray.DoubleClick += (_, _) => Toggle();
        tray.ShowBalloonTip(2500, "HOCO KeepAlive", "Running in the notification area. Right-click the icon for options.", ToolTipIcon.Info);
    }

    private static Icon LoadIcon()
    {
        try
        {
            // The icon is embedded in the EXE at build time, so no sidecar icon file is required.
            using var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (icon is not null) return (Icon)icon.Clone();
        }
        catch { }
        return SystemIcons.Application;
    }

    private void Toggle()
    {
        if (running) StopAudio();
        else StartAudio();
    }

    private void StartAudio()
    {
        try
        {
            provider = new SilentWaveProvider();
            output = new WaveOutEvent { DesiredLatency = 150, NumberOfBuffers = 3 };
            output.Init(provider);
            output.Play();
            running = true;
            toggleItem.Text = "Stop silent keep-alive";
            tray.Text = "HOCO KeepAlive — running silently";
            tray.ShowBalloonTip(1500, "HOCO KeepAlive", "Silent stream started. Speaker battery use may increase while Bluetooth audio stays active.", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            StopAudio();
            MessageBox.Show("Could not start the silent audio stream. Select HOCO HC22 as the Windows playback output and try again.\n\n" + ex.Message,
                "HOCO KeepAlive", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void StopAudio()
    {
        try { output?.Stop(); } catch { }
        output?.Dispose();
        output = null;
        provider = null;
        running = false;
        toggleItem.Text = "Start silent keep-alive";
        tray.Text = "HOCO KeepAlive — stopped";
    }

    private void Exit()
    {
        if (exiting) return;
        exiting = true;
        StopAudio();
        tray.Visible = false;
        tray.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopAudio();
            tray.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal sealed class SilentWaveProvider : IWaveProvider
{
    // Small, steady callback workload; the Windows audio engine may resample this to the device format.
    public WaveFormat WaveFormat { get; } = new WaveFormat(48000, 16, 2);

    public int Read(byte[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);
        return count;
    }
}