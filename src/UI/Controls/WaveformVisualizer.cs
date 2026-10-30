using System.Drawing.Drawing2D;

namespace HocoKeepAlive.UI.Controls;

public sealed class WaveformVisualizer : Control
{
    private readonly System.Windows.Forms.Timer _animTimer;
    private float _phase;
    private bool _isActive;
    private bool _isCallStandby;
    private string _audioContextText = "Bluetooth Wireless Speaker · Low-Overhead Silent Stream (48,000 Hz)";

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                Invalidate();
            }
        }
    }

    public bool IsCallStandby
    {
        get => _isCallStandby;
        set
        {
            if (_isCallStandby != value)
            {
                _isCallStandby = value;
                Invalidate();
            }
        }
    }

    public string AudioContextText
    {
        get => _audioContextText;
        set
        {
            _audioContextText = value;
            Invalidate();
        }
    }

    public WaveformVisualizer()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.ResizeRedraw, true);

        DoubleBuffered = true;
        BackColor = Color.FromArgb(12, 13, 18); // #0C0D12 deep obsidian

        _animTimer = new System.Windows.Forms.Timer { Interval = 33 }; // ~30 FPS
        _animTimer.Tick += (_, _) =>
        {
            _phase += 0.055f;
            if (_phase > MathF.PI * 4f)
            {
                _phase -= MathF.PI * 4f;
            }
            Invalidate();
        };
        _animTimer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animTimer.Stop();
            _animTimer.Dispose();
        }
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        int w = ClientSize.Width;
        int h = ClientSize.Height;
        if (w <= 10 || h <= 10) return;

        // Dark rounded canvas background
        using var bgBrush = new SolidBrush(Color.FromArgb(14, 15, 22));
        using var borderPen = new Pen(Color.FromArgb(32, 35, 48), 1f);
        using var path = CreateRoundedRectanglePath(new Rectangle(0, 0, w - 1, h - 1), 12);
        g.FillPath(bgBrush, path);
        g.DrawPath(borderPen, path);

        float midY = h * 0.52f;

        // Subtle center guideline
        using (var guidePen = new Pen(Color.FromArgb(24, 28, 40), 1f) { DashStyle = DashStyle.Dash })
        {
            g.DrawLine(guidePen, 24, midY, w - 24, midY);
        }

        if (_isCallStandby)
        {
            // WhatsApp Call Mode: Gentle amber/rose pulsing wave with clear indicator
            float pulse = (MathF.Sin(_phase * 2f) + 1f) * 0.5f;
            int waveAlpha = (int)(90 + pulse * 110);

            using var callGlowPen = new Pen(Color.FromArgb(waveAlpha / 3, 239, 68, 68), 6f);
            using var callPen = new Pen(Color.FromArgb(waveAlpha, 251, 146, 60), 2.5f);
            DrawSinWave(g, callGlowPen, w, midY, 18f, 0.015f, _phase);
            DrawSinWave(g, callPen, w, midY, 18f, 0.015f, _phase);

            // Center Call Indicator Badge
            string callMsg = "● WHATSAPP VIDEO CALL ACTIVE · MICROPHONE FREED FOR TALKING";
            using var font = new Font("Segoe UI", 9F, FontStyle.Bold);
            var sz = g.MeasureString(callMsg, font);
            var pillRect = new RectangleF((w - sz.Width - 28) / 2f, midY - 14, sz.Width + 28, 28);

            using var pillBg = new SolidBrush(Color.FromArgb(220, 36, 20, 26));
            using var pillBorder = new Pen(Color.FromArgb(239, 68, 68), 1.2f);
            using var pillPath = CreateRoundedRectanglePath(Rectangle.Round(pillRect), 14);
            g.FillPath(pillBg, pillPath);
            g.DrawPath(pillBorder, pillPath);

            using var textBrush = new SolidBrush(Color.FromArgb(254, 202, 202));
            g.DrawString(callMsg, font, textBrush, pillRect.X + 14, pillRect.Y + 6);
            return;
        }

        if (!_isActive)
        {
            // Resting idle state: calm gentle ribbon wave
            using var idleGlowPen = new Pen(Color.FromArgb(30, 201, 213, 255), 4f);
            using var idlePen = new Pen(Color.FromArgb(70, 160, 175, 210), 1.8f);
            DrawSinWave(g, idleGlowPen, w, midY, 8f, 0.012f, _phase * 0.4f);
            DrawSinWave(g, idlePen, w, midY, 8f, 0.012f, _phase * 0.4f);

            // Center Idle Text
            string idleMsg = "SILENT KEEP-ALIVE IDLE · CLICK START TO MAINTAIN BLUETOOTH SPEAKER CONNECTION";
            using var font = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            var sz = g.MeasureString(idleMsg, font);
            using var textBrush = new SolidBrush(Color.FromArgb(120, 130, 155));
            g.DrawString(idleMsg, font, textBrush, (w - sz.Width) / 2f, midY - sz.Height / 2f);
            return;
        }

        // Active Keep-Alive State: Multi-layered glowing sinusoidal ribbon waves
        // Directly inspired by the reference image's glowing red/crimson/lavender ribbon streams!
        float baseAmp = Math.Clamp(h * 0.28f, 24f, 52f);

        // 1. Wide Crimson Atmosphere Glow
        using (var penGlow = new Pen(Color.FromArgb(35, 230, 57, 86), 8f))
        {
            DrawCompoundSinWave(g, penGlow, w, midY, baseAmp * 1.1f, 0.014f, 0.024f, _phase);
        }

        // 2. Secondary Harmonic Wave (Soft Lavender / Coral)
        using (var penHarmonic = new Pen(Color.FromArgb(130, 201, 213, 255), 2.2f))
        {
            DrawCompoundSinWave(g, penHarmonic, w, midY, baseAmp * 0.75f, 0.019f, 0.031f, _phase * 1.25f + 1.2f);
        }

        // 3. Deep Coral Wave
        using (var penCoral = new Pen(Color.FromArgb(180, 255, 95, 125), 2.8f))
        {
            DrawCompoundSinWave(g, penCoral, w, midY, baseAmp * 0.95f, 0.015f, 0.027f, _phase * 0.9f + 2.4f);
        }

        // 4. Primary Vivid Glowing Crimson Ribbon (Top focal wave)
        using (var penCrimson = new Pen(Color.FromArgb(235, 255, 65, 105), 3.4f))
        {
            DrawCompoundSinWave(g, penCrimson, w, midY, baseAmp, 0.016f, 0.028f, _phase);
        }

        // Top Left Status Indicator
        using var statusFont = new Font("Segoe UI", 8F, FontStyle.Bold);
        using var statusBrush = new SolidBrush(Color.FromArgb(160, 255, 120, 140));
        g.DrawString("● 48 kHz CONTINUOUS STREAM", statusFont, statusBrush, 18, 14);

        // Top Right Telemetry
        using var rightFont = new Font("Segoe UI", 8F);
        using var rightBrush = new SolidBrush(Color.FromArgb(140, 150, 175));
        string rightText = "NAudio Low-Overhead Callback · 0% CPU";
        var rSize = g.MeasureString(rightText, rightFont);
        g.DrawString(rightText, rightFont, rightBrush, w - rSize.Width - 18, 14);
    }

    private static void DrawSinWave(Graphics g, Pen pen, int width, float midY, float amp, float freq, float phase)
    {
        var points = new List<PointF>(width / 4 + 2);
        for (int x = 12; x <= width - 12; x += 4)
        {
            float y = midY + MathF.Sin(x * freq + phase) * amp;
            points.Add(new PointF(x, y));
        }

        if (points.Count > 1)
        {
            g.DrawCurve(pen, points.ToArray(), 0.5f);
        }
    }

    private static void DrawCompoundSinWave(Graphics g, Pen pen, int width, float midY, float amp, float freq1, float freq2, float phase)
    {
        var points = new List<PointF>(width / 4 + 2);
        for (int x = 12; x <= width - 12; x += 4)
        {
            // Superposition of two sine waves gives the elegant organic ribbon look
            float y = midY + (MathF.Sin(x * freq1 + phase) * 0.7f + MathF.Sin(x * freq2 - phase * 0.7f) * 0.3f) * amp;
            points.Add(new PointF(x, y));
        }

        if (points.Count > 1)
        {
            g.DrawCurve(pen, points.ToArray(), 0.5f);
        }
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
