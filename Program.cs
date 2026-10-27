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
        menu.Items.Add(new ToolStripMenuItem("Exit", null, (_, _) => Exit()));
        tray = new NotifyIcon { Icon = LoadIcon(), Text = "HOCO KeepAlive — stopped", ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => Toggle();
        tray.ShowBalloonTip(2500, "HOCO KeepAlive", "Running quietly in the system tray. Right-click for options.", ToolTipIcon.Info);
    }

    private static Icon LoadIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "assets", "hoco-keepalive.ico");
        try { if (File.Exists(path)) return new Icon(path); } catch { }
        return SystemIcons.Information;
    }

    private void Toggle() { if (running) StopAudio(); else StartAudio(); }

    private void StartAudio()
    {
        try
        {
            provider = new SilentWaveProvider();
            output = new WaveOutEvent { DesiredLatency = 100, NumberOfBuffers = 2 };
            output.Init(provider);
            output.Play();
            running = true;
            toggleItem.Text = "Stop silent keep-alive";
            tray.Text = "HOCO KeepAlive — running silently";
            tray.ShowBalloonTip(1500, "HOCO KeepAlive", "Silent audio stream started.", ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            StopAudio();
            MessageBox.Show("Could not start the silent audio stream. Check that HOCO HC22 is connected and selected as the Windows output device.\n\n" + ex.Message,
                "HOCO KeepAlive", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void StopAudio()
    {
        try { output?.Stop(); } catch { }
        output?.Dispose(); output = null; provider = null; running = false;
        toggleItem.Text = "Start silent keep-alive";
        tray.Text = "HOCO KeepAlive — stopped";
    }

    private void Exit()
    {
        if (exiting) return;
        exiting = true; StopAudio(); tray.Visible = false; tray.Dispose(); Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { StopAudio(); tray.Dispose(); }
        base.Dispose(disposing);
    }
}

internal sealed class SilentWaveProvider : IWaveProvider
{
    public WaveFormat WaveFormat { get; } = new WaveFormat(48000, 16, 2);
    public int Read(byte[] buffer, int offset, int count)
    {
        Array.Clear(buffer, offset, count);
        return count;
    }
}