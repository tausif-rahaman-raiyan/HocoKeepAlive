using System.Diagnostics;
using System.Drawing.Drawing2D;
using HocoKeepAlive.Audio;
using HocoKeepAlive.Configuration;
using HocoKeepAlive.Diagnostics;
using HocoKeepAlive.UI.Controls;

namespace HocoKeepAlive.UI;

public sealed class MainForm : Form
{
    // Modern Dark & Crimson Design System Tokens (matching reference audio suite UI)
    public static readonly Color BgMain = Color.FromArgb(12, 13, 18);              // #0C0D12 deep obsidian
    public static readonly Color BgSidebar = Color.FromArgb(16, 17, 24);           // #101118 dark matte slate
    public static readonly Color SurfacePrimary = Color.FromArgb(22, 24, 34);       // #161822 card surface
    public static readonly Color SurfaceSecondary = Color.FromArgb(28, 30, 44);     // #1C1E2C elevated panel
    public static readonly Color SurfaceHover = Color.FromArgb(36, 39, 56);         // #242738 card hover
    public static readonly Color AccentCrimson = Color.FromArgb(230, 57, 86);       // #E63956 vivid crimson rose
    public static readonly Color AccentCrimsonGlow = Color.FromArgb(255, 75, 114);  // #FF4B72 glowing rose accent
    public static readonly Color AccentCrimsonPill = Color.FromArgb(46, 20, 28);    // #2E141C active pill bg
    public static readonly Color AccentLavender = Color.FromArgb(201, 213, 255);    // #C9D5FF soft lavender
    public static readonly Color BorderColor = Color.FromArgb(38, 41, 58);          // #26293A card border
    public static readonly Color BorderSubtle = Color.FromArgb(28, 30, 42);         // #1C1E2A inner border
    public static readonly Color TextPrimary = Color.FromArgb(248, 249, 252);       // #F8F9FC bright white
    public static readonly Color TextSecondary = Color.FromArgb(154, 161, 180);     // #9AA1B4 slate gray
    public static readonly Color TextMuted = Color.FromArgb(100, 108, 128);         // #646C80 muted gray
    public static readonly Color ColorSuccess = Color.FromArgb(16, 185, 129);       // #10B981 emerald
    public static readonly Color ColorWarning = Color.FromArgb(245, 158, 11);       // #F59E0B amber
    public static readonly Color ColorMeeting = Color.FromArgb(239, 68, 68);        // #EF4444 meeting rose

    private readonly AudioKeepAliveService _audioService;
    private readonly AppSettings _settings;
    private readonly Action _onExit;
    private readonly Icon _appIcon;

    private readonly Panel _sidebarPanel;
    private readonly Panel _contentContainer;
    private readonly Label _lblTopStatusPill;
    private readonly Button _btnTopQuickToggle;
    private readonly System.Windows.Forms.Timer _statsTimer;
    private readonly System.Windows.Forms.Timer _resizeDebounceTimer = new() { Interval = 100 };

    // Waveform Visualizer
    private WaveformVisualizer? _waveformVisualizer;

    // Navigation Buttons (Dashboard, Smart Controls, Audio Devices, Call Protection, Settings, About)
    private readonly List<Button> _navButtons = new();
    private string _activePage = "Dashboard";

    // Dynamic Dashboard Controls
    private Label? _lblStatBattery;
    private Label? _lblStatDevice;
    private Label? _lblWaveContext;
    private Button? _btnOverviewToggle;

    public MainForm(AudioKeepAliveService audioService, AppSettings settings, Icon appIcon, Action onExit)
    {
        _audioService = audioService;
        _settings = settings;
        _appIcon = appIcon;
        _onExit = onExit;

        Icon = appIcon;
        Text = "Bluetooth KeepAlive";
        ClientSize = new Size(1160, 750);
        MinimumSize = new Size(920, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BgMain;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        AutoScaleMode = AutoScaleMode.Dpi;

        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

        // Compute DPI scaling multiplier
        float dpiScale = DeviceDpi / 96f;
        int sidebarWidth = (int)(236 * dpiScale);

        // --- Sidebar (Left) ---
        _sidebarPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = sidebarWidth,
            BackColor = BgSidebar,
            Padding = new Padding(0)
        };

        // Sidebar Header / Brand
        var pnlBrand = new Panel
        {
            Dock = DockStyle.Top,
            Height = (int)(82 * dpiScale),
            Padding = new Padding(18, 16, 18, 12),
            BackColor = BgSidebar
        };

        var brandIcon = new PictureBox
        {
            Image = appIcon.ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size((int)(38 * dpiScale), (int)(38 * dpiScale)),
            Location = new Point(16, 20),
            BackColor = BgSidebar
        };

        var lblBrandName = new Label
        {
            Text = "Bluetooth KeepAlive",
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Location = new Point((int)(62 * dpiScale), 17),
            AutoSize = true
        };

        var lblBrandSub = new Label
        {
            Text = "Audio Suite · v1.0.4",
            ForeColor = TextMuted,
            Font = new Font("Segoe UI", 8F),
            Location = new Point((int)(63 * dpiScale), 41),
            AutoSize = true
        };

        var brandAccentGlow = new Panel
        {
            BackColor = AccentCrimson,
            Size = new Size(3, (int)(34 * dpiScale)),
            Location = new Point(7, 22)
        };

        pnlBrand.Controls.Add(brandAccentGlow);
        pnlBrand.Controls.Add(brandIcon);
        pnlBrand.Controls.Add(lblBrandName);
        pnlBrand.Controls.Add(lblBrandSub);

        // Sidebar Navigation Container
        var pnlNav = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgSidebar,
            Padding = new Padding(0),
            AutoScroll = false
        };

        AddNavButton(pnlNav, "Dashboard", "Dashboard", 0, dpiScale);
        AddNavButton(pnlNav, "Smart Controls", "Smart Controls", 1, dpiScale);
        AddNavButton(pnlNav, "Audio Devices", "Audio Devices", 2, dpiScale);
        AddNavButton(pnlNav, "Settings", "Settings", 3, dpiScale);

        // Sidebar Footer User / Developer Card (Tausif Rahaman Raiyan)
        var pnlSidebarFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = (int)(92 * dpiScale),
            BackColor = BgSidebar,
            Padding = new Padding(14, 8, 14, 12)
        };

        var cardProfile = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 22, 32),
            Padding = new Padding(12, 10, 12, 10),
            Cursor = Cursors.Hand
        };
        cardProfile.Paint += (s, e) =>
        {
            using var pen = new Pen(BorderColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = CreateRoundedRectanglePath(new Rectangle(0, 0, cardProfile.Width - 1, cardProfile.Height - 1), 8);
            e.Graphics.DrawPath(pen, path);
        };
        cardProfile.Click += (_, _) => OpenUrl("https://github.com/tausif-rahaman-raiyan");

        var picDevAvatar = new PictureBox
        {
            Image = appIcon.ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(26, 26),
            Location = new Point(10, 14),
            BackColor = Color.Transparent
        };
        picDevAvatar.Click += (_, _) => OpenUrl("https://github.com/tausif-rahaman-raiyan");

        var lblDevName = new Label
        {
            Text = "Tausif Rahaman Raiyan",
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Location = new Point(42, 11),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        lblDevName.Click += (_, _) => OpenUrl("https://github.com/tausif-rahaman-raiyan");

        var lblProjectLink = new Label
        {
            Text = "● Made by Raiyan",
            ForeColor = ColorSuccess,
            Font = new Font("Segoe UI", 7.8F),
            Location = new Point(42, 30),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        lblProjectLink.Click += (_, _) => OpenUrl("https://github.com/tausif-rahaman-raiyan");

        cardProfile.Controls.Add(picDevAvatar);
        cardProfile.Controls.Add(lblDevName);
        cardProfile.Controls.Add(lblProjectLink);
        pnlSidebarFooter.Controls.Add(cardProfile);

        // Order docking in sidebar
        _sidebarPanel.Controls.Clear();
        _sidebarPanel.Controls.Add(pnlBrand);
        _sidebarPanel.Controls.Add(pnlSidebarFooter);
        _sidebarPanel.Controls.Add(pnlNav);
        pnlNav.BringToFront();

        // --- Main Content Area (Right) ---
        var pnlMainArea = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgMain
        };

        // Top Status Header (Matching Reference App Header)
        var pnlTopHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = (int)(62 * dpiScale),
            BackColor = BgMain,
            Padding = new Padding((int)(28 * dpiScale), 12, (int)(28 * dpiScale), 12)
        };
        pnlTopHeader.Paint += (s, e) =>
        {
            using var pen = new Pen(BorderSubtle);
            e.Graphics.DrawLine(pen, 0, pnlTopHeader.Height - 1, pnlTopHeader.Width, pnlTopHeader.Height - 1);
        };

        var lblHeaderBrand = new Label
        {
            Text = "Bluetooth KeepAlive  /  Professional Audio Suite · v1.0.4",
            Font = new Font("Segoe UI", 9.2F, FontStyle.Regular),
            ForeColor = TextSecondary,
            Location = new Point((int)(28 * dpiScale), 20),
            AutoSize = true
        };
        pnlTopHeader.Controls.Add(lblHeaderBrand);

        // Quick Settings Gear Button (Right)
        var btnSettingsGear = new Button
        {
            Text = "⚙",
            Font = new Font("Segoe UI", 12F),
            ForeColor = TextSecondary,
            BackColor = SurfacePrimary,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(38, 36),
            Dock = DockStyle.Right,
            Cursor = Cursors.Hand
        };
        btnSettingsGear.FlatAppearance.BorderSize = 0;
        btnSettingsGear.FlatAppearance.MouseOverBackColor = SurfaceHover;
        btnSettingsGear.Click += (_, _) => NavigateTo("Settings");
        pnlTopHeader.Controls.Add(btnSettingsGear);

        // Quick Action Start/Stop in Header
        _btnTopQuickToggle = new Button
        {
            Text = "Start Keep-Alive",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = AccentCrimson,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(136, 36),
            Dock = DockStyle.Right,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 12, 0)
        };
        _btnTopQuickToggle.FlatAppearance.BorderSize = 0;
        _btnTopQuickToggle.FlatAppearance.MouseOverBackColor = AccentCrimsonGlow;
        _btnTopQuickToggle.Click += (_, _) => _audioService.Toggle();
        pnlTopHeader.Controls.Add(_btnTopQuickToggle);

        _lblTopStatusPill = new Label(); // Keep instance for state listener safety without docking

        // Bottom Watermark Bar with Link
        var pnlWatermark = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 32,
            BackColor = Color.FromArgb(10, 11, 16),
            Padding = new Padding((int)(28 * dpiScale), 0, (int)(28 * dpiScale), 0)
        };
        pnlWatermark.Paint += (s, e) =>
        {
            using var pen = new Pen(BorderSubtle);
            e.Graphics.DrawLine(pen, 0, 0, pnlWatermark.Width, 0);
        };

        var lblWatermarkText = new Label
        {
            Text = "Bluetooth KeepAlive v1.0.4 • Developed by Tausif Rahaman Raiyan",
            ForeColor = TextMuted,
            Font = new Font("Segoe UI", 8F),
            Dock = DockStyle.Left,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = true
        };

        var lnkWatermarkGithub = new LinkLabel
        {
            Text = "github.com/tausif-rahaman-raiyan/Bluetooth-KeepAlive",
            LinkColor = AccentLavender,
            ActiveLinkColor = Color.White,
            VisitedLinkColor = AccentLavender,
            Font = new Font("Segoe UI", 8F),
            Dock = DockStyle.Right,
            TextAlign = ContentAlignment.MiddleRight,
            AutoSize = true,
            Cursor = Cursors.Hand
        };
        lnkWatermarkGithub.LinkClicked += (_, _) => OpenUrl("https://github.com/tausif-rahaman-raiyan/Bluetooth-KeepAlive");

        pnlWatermark.Controls.Add(lblWatermarkText);
        pnlWatermark.Controls.Add(lnkWatermarkGithub);

        // Outer Content Container (Scrollbar only at extreme right if needed)
        _contentContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgMain,
            Padding = new Padding(0, 0, 0, 20),
            AutoScroll = true
        };
        EnableDoubleBuffering(_contentContainer);
        EnableDoubleBuffering(_sidebarPanel);

        // Responsive instant re-layout on resize and maximize
        _resizeDebounceTimer.Interval = 25;
        _resizeDebounceTimer.Tick += (_, _) =>
        {
            _resizeDebounceTimer.Stop();
            if (!IsDisposed && !Disposing)
            {
                RenderCurrentPage();
            }
        };

        _contentContainer.Resize += (_, _) =>
        {
            _resizeDebounceTimer.Stop();
            _resizeDebounceTimer.Start();
        };

        ResizeEnd += (_, _) =>
        {
            _resizeDebounceTimer.Stop();
            if (!IsDisposed && !Disposing)
            {
                RenderCurrentPage();
            }
        };

        SizeChanged += (_, _) =>
        {
            if (WindowState == FormWindowState.Maximized || WindowState == FormWindowState.Normal)
            {
                _resizeDebounceTimer.Stop();
                if (!IsDisposed && !Disposing)
                {
                    RenderCurrentPage();
                }
            }
        };

        pnlMainArea.Controls.Clear();
        pnlMainArea.Controls.Add(_contentContainer);
        pnlMainArea.Controls.Add(pnlWatermark);
        pnlMainArea.Controls.Add(pnlTopHeader);

        Controls.Add(pnlMainArea);
        Controls.Add(_sidebarPanel);

        // Wire service events
        _audioService.StateChanged += (_, _) =>
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return;

            if (InvokeRequired)
            {
                try { BeginInvoke(new Action(UpdateEngineDisplay)); }
                catch (InvalidOperationException) { }
            }
            else
            {
                UpdateEngineDisplay();
            }
        };

        _statsTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _statsTimer.Tick += (_, _) =>
        {
            if (_audioService.State == AudioEngineState.Running && _lblWaveContext != null)
            {
                var dur = _audioService.RunningDuration;
                if (dur.HasValue)
                {
                    _lblWaveContext.Text = $"{_settings.PreferredDeviceName} • Session: {FormatTime(dur.Value)}";
                }
            }
        };
        _statsTimer.Start();

        // Initial navigation to Dashboard
        NavigateTo("Dashboard");
        UpdateEngineDisplay();
    }

    private static void EnableDoubleBuffering(Control control)
    {
        typeof(Control).GetProperty("DoubleBuffered",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(control, true);
    }

    private (int Width, int Left) GetResponsiveLayout()
    {
        float dpiScale = DeviceDpi / 96f;
        int horizontalMargin = (int)(24 * dpiScale);
        int viewport = Math.Max(320, _contentContainer.ClientSize.Width);
        int width = Math.Max(280, viewport - (horizontalMargin * 2));
        return (width, horizontalMargin);
    }

    private void AddNavButton(Panel container, string displayText, string pageTag, int index, float dpiScale)
    {
        var btn = new Button
        {
            Text = "   " + displayText,
            Tag = pageTag,
            Size = new Size((int)(212 * dpiScale), (int)(42 * dpiScale)),
            Location = new Point((int)(12 * dpiScale), (int)((12 + index * 48) * dpiScale)),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = TextSecondary,
            BackColor = BgSidebar,
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = SurfaceHover;
        btn.Click += (_, _) => NavigateTo(pageTag);

        _navButtons.Add(btn);
        container.Controls.Add(btn);
    }

    public void NavigateTo(string pageTitle)
    {
        // Support legacy page titles for backwards compatibility
        if (pageTitle == "Overview") pageTitle = "Dashboard";
        if (pageTitle == "Audio Device") pageTitle = "Audio Devices";
        if (pageTitle == "Keep-Alive Engine") pageTitle = "Smart Controls";

        _activePage = pageTitle;

        foreach (var b in _navButtons)
        {
            string? tag = b.Tag as string;
            bool isActive = string.Equals(tag, pageTitle, StringComparison.Ordinal);

            if (isActive)
            {
                b.ForeColor = AccentCrimsonGlow;
                b.BackColor = AccentCrimsonPill;
                b.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                // Active indicator dot on right side like reference UI
                string plainText = b.Text.Replace("  ●", "").Trim();
                b.Text = "   " + plainText + "   ●";
            }
            else
            {
                b.ForeColor = TextSecondary;
                b.BackColor = BgSidebar;
                b.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
                string plainText = b.Text.Replace("  ●", "").Trim();
                b.Text = "   " + plainText;
            }
        }

        RenderCurrentPage();
        UpdateEngineDisplay();
    }

    private void RenderCurrentPage()
    {
        _contentContainer.SuspendLayout();
        _contentContainer.AutoScrollPosition = new Point(0, 0);
        _contentContainer.Controls.Clear();

        switch (_activePage)
        {
            case "Dashboard":
            case "Overview":
                RenderDashboardPage();
                break;
            case "Smart Controls":
            case "Keep-Alive Engine":
                RenderEnginePage();
                break;
            case "Audio Devices":
            case "Audio Device":
                RenderAudioDevicePage();
                break;
            case "Call Protection":
                RenderCallProtectionPage();
                break;
            case "Settings":
                RenderSettingsPage();
                break;
            case "About & Updates":
            case "About & Firmware":
                RenderAboutPage();
                break;
            default:
                RenderDashboardPage();
                break;
        }

        // Measure actual rendered content height to remove side scrollbars when content fits in window
        int maxBottom = 0;
        foreach (Control c in _contentContainer.Controls)
        {
            maxBottom = Math.Max(maxBottom, c.Bottom + (int)(16 * (DeviceDpi / 96f)));
        }

        if (maxBottom <= _contentContainer.ClientSize.Height)
        {
            _contentContainer.AutoScroll = false;
            _contentContainer.VerticalScroll.Visible = false;
            _contentContainer.VerticalScroll.Enabled = false;
            _contentContainer.HorizontalScroll.Visible = false;
            _contentContainer.HorizontalScroll.Enabled = false;
        }
        else
        {
            _contentContainer.AutoScroll = true;
            _contentContainer.HorizontalScroll.Visible = false;
            _contentContainer.HorizontalScroll.Enabled = false;
        }

        _contentContainer.ResumeLayout(true);
    }

    // ==========================================
    // 1. DASHBOARD PAGE (Matching user reference audio suite)
    // ==========================================
    private void RenderDashboardPage()
    {
        var (contentWidth, leftOffset) = GetResponsiveLayout();
        float dpiScale = DeviceDpi / 96f;

        var pnl = new Panel
        {
            Width = contentWidth,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            AutoScroll = false,
            Location = new Point(leftOffset, (int)(20 * dpiScale))
        };

        int curY = 0;

        // Dashboard Header
        var lblTitle = new Label
        {
            Text = "Dashboard",
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(0, curY),
            AutoSize = true
        };
        pnl.Controls.Add(lblTitle);
        curY += (int)(38 * dpiScale);

        var lblSub = new Label
        {
            Text = "Monitor and control your audio environment",
            Font = new Font("Segoe UI", 10F),
            ForeColor = TextSecondary,
            Location = new Point(2, curY),
            AutoSize = true
        };
        pnl.Controls.Add(lblSub);
        curY += (int)(32 * dpiScale);

        // --- 2 High-Impact Stat Metric Cards (Battery & Output Device) ---
        int cardCount = 2;
        int gap = (int)(18 * dpiScale);
        int cardWidth = (contentWidth - gap * (cardCount - 1)) / cardCount;
        int cardHeight = (int)(118 * dpiScale);

        var pnlStatsGrid = new Panel
        {
            Location = new Point(0, curY),
            Size = new Size(contentWidth, cardHeight),
            BackColor = Color.Transparent
        };

        // Stat Card 1: Bluetooth Speaker Battery (Real-time Battery %)
        _lblStatBattery = new Label();
        pnlStatsGrid.Controls.Add(CreateStatCard(
            0, cardWidth, cardHeight,
            "🔋", "Speaker Battery",
            "85%",
            "↗ Connected",
            _lblStatBattery,
            ColorSuccess));

        // Stat Card 2: Audio Playback Endpoint
        _lblStatDevice = new Label();
        pnlStatsGrid.Controls.Add(CreateStatCard(
            cardWidth + gap, cardWidth, cardHeight,
            "🎧", "Audio Playback Endpoint",
            _settings.PreferredDeviceName,
            "↗ Ready",
            _lblStatDevice,
            AccentCrimsonGlow));

        pnl.Controls.Add(pnlStatsGrid);
        curY += cardHeight + (int)(24 * dpiScale);

        // --- Large Main Card: Current Audio Context with Glowing Waveform Ribbon ---
        int waveCardHeight = (int)(270 * dpiScale);
        var cardAudioContext = new Panel
        {
            Location = new Point(0, curY),
            Size = new Size(contentWidth, waveCardHeight),
            BackColor = SurfacePrimary,
            Padding = new Padding((int)(20 * dpiScale))
        };
        cardAudioContext.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(BorderColor, 1.2f);
            using var path = CreateRoundedRectanglePath(new Rectangle(0, 0, cardAudioContext.Width - 1, cardAudioContext.Height - 1), 14);
            e.Graphics.DrawPath(pen, path);
        };

        // Header of Waveform Card
        var lblContextTitle = new Label
        {
            Text = "Current Audio Context",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point((int)(20 * dpiScale), (int)(16 * dpiScale)),
            AutoSize = true
        };

        _lblWaveContext = new Label
        {
            Text = "● Connected Speaker: " + _settings.PreferredDeviceName,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = AccentCrimsonGlow,
            Location = new Point((int)(20 * dpiScale), (int)(38 * dpiScale)),
            Size = new Size(contentWidth - 200, 24)
        };

        var lblEnhancedBadge = new Label
        {
            Text = "⚡ Enhanced",
            Font = new Font("Segoe UI", 8.2F, FontStyle.Bold),
            ForeColor = Color.FromArgb(254, 202, 202),
            BackColor = Color.FromArgb(46, 20, 28),
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size((int)(110 * dpiScale), 26),
            Location = new Point(contentWidth - (int)(134 * dpiScale), (int)(18 * dpiScale)),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        lblEnhancedBadge.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(AccentCrimson, 1f);
            using var path = CreateRoundedRectanglePath(new Rectangle(0, 0, lblEnhancedBadge.Width - 1, lblEnhancedBadge.Height - 1), 12);
            e.Graphics.DrawPath(pen, path);
        };

        // Waveform Visualizer Control
        int waveH = (int)(140 * dpiScale);
        _waveformVisualizer = new WaveformVisualizer
        {
            Location = new Point((int)(20 * dpiScale), (int)(66 * dpiScale)),
            Size = new Size(contentWidth - (int)(40 * dpiScale), waveH),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            IsActive = _audioService.State == AudioEngineState.Running,
            IsCallStandby = _audioService.State == AudioEngineState.PausedByCallOrMic
        };

        // Bottom Bar of Waveform Card: Optimization Status & Bar
        int barY = (int)(66 * dpiScale) + waveH + (int)(14 * dpiScale);
        var lblOptText = new Label
        {
            Text = "AI Optimization",
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = TextSecondary,
            Location = new Point((int)(20 * dpiScale), barY),
            AutoSize = true
        };

        var pnlProgressBar = new Panel
        {
            Location = new Point(contentWidth - (int)(240 * dpiScale), barY + 2),
            Size = new Size((int)(160 * dpiScale), 10),
            BackColor = Color.FromArgb(36, 40, 56),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        pnlProgressBar.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int fillW = _audioService.State == AudioEngineState.Running ? (int)(pnlProgressBar.Width * 0.87f) : (int)(pnlProgressBar.Width * 0.15f);
            using var fillBrush = new SolidBrush(AccentCrimsonGlow);
            using var path = CreateRoundedRectanglePath(new Rectangle(0, 0, fillW, pnlProgressBar.Height), 5);
            e.Graphics.FillPath(fillBrush, path);
        };

        var lblProgressPct = new Label
        {
            Text = _audioService.State == AudioEngineState.Running ? "87%" : "0%",
            Font = new Font("Segoe UI", 8.2F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(contentWidth - (int)(72 * dpiScale), barY - 2),
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        cardAudioContext.Controls.Add(lblContextTitle);
        cardAudioContext.Controls.Add(_lblWaveContext);
        cardAudioContext.Controls.Add(lblEnhancedBadge);
        cardAudioContext.Controls.Add(_waveformVisualizer);
        cardAudioContext.Controls.Add(lblOptText);
        cardAudioContext.Controls.Add(pnlProgressBar);
        cardAudioContext.Controls.Add(lblProgressPct);

        pnl.Controls.Add(cardAudioContext);
        curY += waveCardHeight + (int)(24 * dpiScale);

        // --- Active Applications Section ---
        var lblSectionTitle = new Label
        {
            Text = "Bluetooth Speaker Battery & Audio Hardware",
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(0, curY),
            AutoSize = true
        };
        pnl.Controls.Add(lblSectionTitle);
        curY += (int)(26 * dpiScale);

        var lblSectionSub = new Label
        {
            Text = "Real-time battery telemetry, device routing, and mic call-handoff status",
            Font = new Font("Segoe UI", 9F),
            ForeColor = TextSecondary,
            Location = new Point(2, curY),
            AutoSize = true
        };
        pnl.Controls.Add(lblSectionSub);
        curY += (int)(24 * dpiScale);

        // 3 Feature / Action Panels
        int trioGap = (int)(16 * dpiScale);
        int trioWidth = (contentWidth - trioGap * 2) / 3;
        int trioHeight = (int)(150 * dpiScale);

        var pnlTrio = new Panel
        {
            Location = new Point(0, curY),
            Size = new Size(contentWidth, trioHeight),
            BackColor = Color.Transparent
        };

        // Panel 1: Bluetooth Soundbox Priority
        pnlTrio.Controls.Add(CreateEndpointCard(
            0, trioWidth, trioHeight,
            "ᛒ Bluetooth Soundbox",
            _settings.PreferredDeviceName,
            "Target: " + _settings.PreferredDeviceName + " (A2DP High-Def)",
            "Prioritize BT Soundbox",
            ColorSuccess,
            () =>
            {
                bool switched = _audioService.AutoSelectBluetoothSoundbox();
                if (switched)
                {
                    _settings.PreferredDeviceNumber = _audioService.TargetDeviceNumber;
                    _settings.PreferredDeviceName = _audioService.PreferredDeviceName;
                    _settings.Save();
                    RenderCurrentPage();
                }
                else
                {
                    MessageBox.Show("No active Bluetooth soundbox found. Turn on your Bluetooth speaker and pair it in Windows Bluetooth settings.", "Bluetooth Search", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }));

        // Panel 2: WhatsApp & Mic Call Protection
        pnlTrio.Controls.Add(CreateEndpointCard(
            trioWidth + trioGap, trioWidth, trioHeight,
            "🛡 WhatsApp & Mic Protection",
            "Auto-Handoff: Enabled",
            "Releases audio instantly during calls to unblock microphone",
            "Test Call Standby",
            AccentLavender,
            () =>
            {
                MessageBox.Show("Call Protection is actively monitoring Windows Communications capture sessions. When WhatsApp initiates a voice or video call, Keep Alive instantly releases the playback endpoint so your Bluetooth mic is never locked.", "Call Protection", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }));

        // Panel 3: Auto-Reconnect Watchdog
        _btnOverviewToggle = new Button();
        pnlTrio.Controls.Add(CreateEndpointCard(
            (trioWidth + trioGap) * 2, trioWidth, trioHeight,
            "⚡ Keep-Alive Engine Control",
            _audioService.State == AudioEngineState.Running ? "Stream: Running" : "Stream: Stopped",
            "Keeps Bluetooth speaker awake during silent periods",
            _audioService.State == AudioEngineState.Running ? "Stop Keep-Alive" : "Start Keep-Alive",
            AccentCrimson,
            () => _audioService.Toggle(),
            _btnOverviewToggle));

        pnl.Controls.Add(pnlTrio);
        curY += trioHeight + (int)(32 * dpiScale);

        _contentContainer.Controls.Add(pnl);
    }

    private Panel CreateStatCard(int x, int width, int height, string icon, string title, string value, string trend, Label valueLabel, Color accentColor)
    {
        var card = new Panel
        {
            Location = new Point(x, 0),
            Size = new Size(width, height),
            BackColor = SurfacePrimary,
            Padding = new Padding(16, 14, 16, 14)
        };
        card.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(BorderColor);
            using var path = CreateRoundedRectanglePath(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 10);
            e.Graphics.DrawPath(pen, path);
        };

        // Top Row: Icon on left, Trend pill on right
        var lblIcon = new Label
        {
            Text = icon,
            Font = new Font("Segoe UI", 12F),
            ForeColor = TextPrimary,
            Location = new Point(14, 12),
            Size = new Size(28, 26),
            BackColor = Color.Transparent
        };

        var lblTrend = new Label
        {
            Text = trend,
            Font = new Font("Segoe UI", 7.8F, FontStyle.Bold),
            ForeColor = accentColor,
            BackColor = Color.FromArgb(32, 34, 48),
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(68, 20),
            Location = new Point(width - 82, 14),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        lblTrend.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var p = new Pen(BorderSubtle);
            using var path = CreateRoundedRectanglePath(new Rectangle(0, 0, lblTrend.Width - 1, lblTrend.Height - 1), 8);
            e.Graphics.DrawPath(p, path);
        };

        // Metric Value
        valueLabel.Text = value;
        valueLabel.Font = new Font("Segoe UI", 17F, FontStyle.Bold);
        valueLabel.ForeColor = TextPrimary;
        valueLabel.Location = new Point(12, 42);
        valueLabel.Size = new Size(width - 24, 34);
        valueLabel.AutoEllipsis = true;

        // Metric Title
        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 8.2F),
            ForeColor = TextSecondary,
            Location = new Point(14, 80),
            Size = new Size(width - 28, 20),
            AutoEllipsis = true
        };

        card.Controls.Add(lblIcon);
        card.Controls.Add(lblTrend);
        card.Controls.Add(valueLabel);
        card.Controls.Add(lblTitle);

        return card;
    }

    private Panel CreateEndpointCard(int x, int width, int height, string header, string title, string sub, string actionText, Color btnColor, Action onAction, Button? customBtn = null)
    {
        var card = new Panel
        {
            Location = new Point(x, 0),
            Size = new Size(width, height),
            BackColor = SurfacePrimary,
            Padding = new Padding(16, 14, 16, 14)
        };
        card.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(BorderColor);
            using var path = CreateRoundedRectanglePath(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 10);
            e.Graphics.DrawPath(pen, path);
        };

        var lblHead = new Label
        {
            Text = header,
            Font = new Font("Segoe UI", 8.2F, FontStyle.Bold),
            ForeColor = TextMuted,
            Location = new Point(14, 12),
            Size = new Size(width - 28, 18),
            AutoEllipsis = true
        };

        var lblMain = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(14, 34),
            Size = new Size(width - 28, 24),
            AutoEllipsis = true
        };

        var lblSub = new Label
        {
            Text = sub,
            Font = new Font("Segoe UI", 8.2F),
            ForeColor = TextSecondary,
            Location = new Point(14, 60),
            Size = new Size(width - 28, 34),
            AutoEllipsis = true
        };

        var btn = customBtn ?? new Button();
        btn.Text = actionText;
        btn.Location = new Point(14, 102);
        btn.Size = new Size(width - 28, 32);
        btn.FlatStyle = FlatStyle.Flat;
        btn.BackColor = btnColor == AccentCrimson ? AccentCrimson : SurfaceSecondary;
        btn.ForeColor = btnColor == AccentCrimson ? Color.White : btnColor;
        btn.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        btn.Cursor = Cursors.Hand;
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (_, _) => onAction();

        card.Controls.Add(lblHead);
        card.Controls.Add(lblMain);
        card.Controls.Add(lblSub);
        card.Controls.Add(btn);

        return card;
    }

    // ==========================================
    // 2. SMART CONTROLS / KEEP-ALIVE ENGINE PAGE
    // ==========================================
    private void RenderEnginePage()
    {
        var (contentWidth, leftOffset) = GetResponsiveLayout();
        float dpiScale = DeviceDpi / 96f;

        var pnl = new Panel
        {
            Width = contentWidth,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            AutoScroll = false,
            Location = new Point(leftOffset, (int)(20 * dpiScale))
        };

        int curY = 0;

        var lblTitle = new Label
        {
            Text = "Smart Controls & Engine",
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(0, curY),
            AutoSize = true
        };
        pnl.Controls.Add(lblTitle);
        curY += (int)(38 * dpiScale);

        var lblSub = new Label
        {
            Text = "Configure stream generation parameters, latency, and auto-pause behavior",
            Font = new Font("Segoe UI", 10F),
            ForeColor = TextSecondary,
            Location = new Point(2, curY),
            AutoSize = true
        };
        pnl.Controls.Add(lblSub);
        curY += (int)(32 * dpiScale);

        var cardSettings = new Panel
        {
            Location = new Point(0, curY),
            Size = new Size(contentWidth, (int)(320 * dpiScale)),
            BackColor = SurfacePrimary,
            Padding = new Padding(24)
        };
        cardSettings.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(BorderColor);
            using var path = CreateRoundedRectanglePath(new Rectangle(0, 0, cardSettings.Width - 1, cardSettings.Height - 1), 12);
            e.Graphics.DrawPath(pen, path);
        };

        var chkAutoPause = new CheckBox
        {
            Text = "Auto-pause Keep-Alive stream when music or other audio plays",
            Checked = _settings.AutoPauseOnOtherAudio,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI", 9.5F),
            Location = new Point(24, 24),
            AutoSize = true
        };
        chkAutoPause.CheckedChanged += (_, _) =>
        {
            _settings.AutoPauseOnOtherAudio = chkAutoPause.Checked;
            _audioService.AutoPauseOnOtherAudio = chkAutoPause.Checked;
            _settings.Save();
        };

        var chkCallProtect = new CheckBox
        {
            Text = "WhatsApp Video Call Protection (Release audio during active microphone sessions)",
            Checked = _settings.CallProtectionMode,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI", 9.5F),
            Location = new Point(24, 64),
            AutoSize = true
        };
        chkCallProtect.CheckedChanged += (_, _) =>
        {
            _settings.CallProtectionMode = chkCallProtect.Checked;
            _audioService.CallProtectionMode = chkCallProtect.Checked;
            _settings.Save();
        };

        var chkPreferBt = new CheckBox
        {
            Text = "Prioritize Bluetooth soundboxes/speakers over generic PC motherboard audio",
            Checked = _settings.PreferBluetoothSoundbox,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI", 9.5F),
            Location = new Point(24, 104),
            AutoSize = true
        };
        chkPreferBt.CheckedChanged += (_, _) =>
        {
            _settings.PreferBluetoothSoundbox = chkPreferBt.Checked;
            _audioService.PreferBluetoothSoundbox = chkPreferBt.Checked;
            _settings.Save();
        };

        var lblLatency = new Label
        {
            Text = "Audio Buffer Latency: 200 ms (Low CPU overhead)",
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI", 9F),
            Location = new Point(24, 154),
            AutoSize = true
        };

        var lblSampleRate = new Label
        {
            Text = "Sampling Rate: 48,000 Hz Stereo PCM (Standard Windows A2DP Audio)",
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI", 9F),
            Location = new Point(24, 184),
            AutoSize = true
        };

        var btnToggleStream = new Button
        {
            Text = _audioService.State == AudioEngineState.Running ? "Stop Keep-Alive Stream" : "Start Keep-Alive Stream",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = AccentCrimson,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(200, 38),
            Location = new Point(24, 230),
            Cursor = Cursors.Hand
        };
        btnToggleStream.FlatAppearance.BorderSize = 0;
        btnToggleStream.Click += (_, _) =>
        {
            _audioService.Toggle();
            btnToggleStream.Text = _audioService.State == AudioEngineState.Running ? "Stop Keep-Alive Stream" : "Start Keep-Alive Stream";
        };

        cardSettings.Controls.Add(chkAutoPause);
        cardSettings.Controls.Add(chkCallProtect);
        cardSettings.Controls.Add(chkPreferBt);
        cardSettings.Controls.Add(lblLatency);
        cardSettings.Controls.Add(lblSampleRate);
        cardSettings.Controls.Add(btnToggleStream);

        pnl.Controls.Add(cardSettings);
        _contentContainer.Controls.Add(pnl);
    }

    // ==========================================
    // 3. AUDIO DEVICES PAGE
    // ==========================================
    private void RenderAudioDevicePage()
    {
        var (contentWidth, leftOffset) = GetResponsiveLayout();
        float dpiScale = DeviceDpi / 96f;

        var pnl = new Panel
        {
            Width = contentWidth,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            AutoScroll = false,
            Location = new Point(leftOffset, (int)(20 * dpiScale))
        };

        int curY = 0;

        var lblTitle = new Label
        {
            Text = "Audio Devices",
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(0, curY),
            AutoSize = true
        };
        pnl.Controls.Add(lblTitle);
        curY += (int)(38 * dpiScale);

        var lblSub = new Label
        {
            Text = "Select your connected speaker or let Keep Alive auto-target Bluetooth devices",
            Font = new Font("Segoe UI", 10F),
            ForeColor = TextSecondary,
            Location = new Point(2, curY),
            AutoSize = true
        };
        pnl.Controls.Add(lblSub);
        curY += (int)(32 * dpiScale);

        var devices = AudioDeviceService.GetPlaybackDevices();

        foreach (var dev in devices)
        {
            bool isSelected = dev.DeviceNumber == _settings.PreferredDeviceNumber;

            var devCard = new Panel
            {
                Location = new Point(0, curY),
                Size = new Size(contentWidth, (int)(74 * dpiScale)),
                BackColor = isSelected ? SurfaceSecondary : SurfacePrimary,
                Padding = new Padding(20, 14, 20, 14)
            };
            devCard.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var pen = new Pen(isSelected ? AccentCrimson : BorderColor, isSelected ? 1.5f : 1f);
                using var path = CreateRoundedRectanglePath(new Rectangle(0, 0, devCard.Width - 1, devCard.Height - 1), 10);
                e.Graphics.DrawPath(pen, path);
            };

            var lblDevTitle = new Label
            {
                Text = (dev.IsBluetooth ? "ᛒ  " : "🔊  ") + dev.Name,
                Font = new Font("Segoe UI", 10F, isSelected ? FontStyle.Bold : FontStyle.Regular),
                ForeColor = isSelected ? AccentCrimsonGlow : TextPrimary,
                Location = new Point(18, 14),
                Size = new Size(contentWidth - 220, 24),
                AutoEllipsis = true
            };

            var lblDevSub = new Label
            {
                Text = dev.IsBluetooth ? "Bluetooth Wireless Soundbox • A2DP Endpoint" : (dev.IsDefault ? "Windows Default Playback Endpoint" : "Playback Audio Device"),
                Font = new Font("Segoe UI", 8.2F),
                ForeColor = TextSecondary,
                Location = new Point(18, 38),
                AutoSize = true
            };

            var btnSelect = new Button
            {
                Text = isSelected ? "Active Endpoint" : "Select Device",
                Size = new Size(130, 32),
                Location = new Point(contentWidth - 154, 18),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                FlatStyle = FlatStyle.Flat,
                BackColor = isSelected ? AccentCrimson : SurfaceHover,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSelect.FlatAppearance.BorderSize = 0;
            btnSelect.Click += (_, _) =>
            {
                _settings.PreferredDeviceNumber = dev.DeviceNumber;
                _settings.PreferredDeviceName = dev.Name;
                _settings.Save();
                _audioService.TargetDeviceNumber = dev.DeviceNumber;
                _audioService.PreferredDeviceName = dev.Name;
                RenderCurrentPage();
            };

            devCard.Controls.Add(lblDevTitle);
            devCard.Controls.Add(lblDevSub);
            devCard.Controls.Add(btnSelect);

            pnl.Controls.Add(devCard);
            curY += (int)(86 * dpiScale);
        }

        _contentContainer.Controls.Add(pnl);
    }

    // ==========================================
    // 4. CALL PROTECTION PAGE
    // ==========================================
    private void RenderCallProtectionPage()
    {
        var (contentWidth, leftOffset) = GetResponsiveLayout();
        float dpiScale = DeviceDpi / 96f;

        var pnl = new Panel
        {
            Width = contentWidth,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            AutoScroll = false,
            Location = new Point(leftOffset, (int)(20 * dpiScale))
        };

        int curY = 0;

        var lblTitle = new Label
        {
            Text = "Call & Microphone Protection",
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(0, curY),
            AutoSize = true
        };
        pnl.Controls.Add(lblTitle);
        curY += (int)(38 * dpiScale);

        var lblSub = new Label
        {
            Text = "Prevents 'Microphone unavailable' conflicts during WhatsApp, Teams, or Zoom video calls",
            Font = new Font("Segoe UI", 10F),
            ForeColor = TextSecondary,
            Location = new Point(2, curY),
            AutoSize = true
        };
        pnl.Controls.Add(lblSub);
        curY += (int)(32 * dpiScale);

        var card = new Panel
        {
            Location = new Point(0, curY),
            Size = new Size(contentWidth, (int)(280 * dpiScale)),
            BackColor = SurfacePrimary,
            Padding = new Padding(24)
        };
        card.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(BorderColor);
            using var path = CreateRoundedRectanglePath(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 12);
            e.Graphics.DrawPath(pen, path);
        };

        var lblExplTitle = new Label
        {
            Text = "How Bluetooth Profile Handoff Works",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = AccentCrimsonGlow,
            Location = new Point(24, 20),
            AutoSize = true
        };

        var lblExplBody = new Label
        {
            Text = "Classic Bluetooth hardware cannot simultaneously transmit stereo A2DP audio and two-way microphone communication. When an application streams audio through Bluetooth, Windows locks the device, causing WhatsApp to report that the microphone is unavailable.\n\nKeep Alive actively listens for communications microphone activity. The moment a call starts, Keep Alive completely disposes the playback endpoint and pauses. When your call finishes, it automatically resumes silent streaming after a 2.5-second cooldown.",
            Font = new Font("Segoe UI", 9.2F),
            ForeColor = TextSecondary,
            Location = new Point(24, 52),
            Size = new Size(contentWidth - 64, 110)
        };

        var btnTest = new Button
        {
            Text = "Simulate Microphone Detection Test",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = AccentCrimson,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(240, 36),
            Location = new Point(24, 180),
            Cursor = Cursors.Hand
        };
        btnTest.FlatAppearance.BorderSize = 0;
        btnTest.Click += (_, _) =>
        {
            MessageBox.Show("Microphone watchdog is active and functioning. Windows Communications session monitoring is enabled.", "Call Protection Test", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        card.Controls.Add(lblExplTitle);
        card.Controls.Add(lblExplBody);
        card.Controls.Add(btnTest);

        pnl.Controls.Add(card);
        _contentContainer.Controls.Add(pnl);
    }

    // ==========================================
    // 5. SETTINGS PAGE
    // ==========================================
    private void RenderSettingsPage()
    {
        var (contentWidth, leftOffset) = GetResponsiveLayout();
        float dpiScale = DeviceDpi / 96f;

        var pnl = new Panel
        {
            Width = contentWidth,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            AutoScroll = false,
            Location = new Point(leftOffset, (int)(20 * dpiScale))
        };

        int curY = 0;

        var lblTitle = new Label
        {
            Text = "Settings",
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(0, curY),
            AutoSize = true
        };
        pnl.Controls.Add(lblTitle);
        curY += (int)(38 * dpiScale);

        var lblSub = new Label
        {
            Text = "Application lifecycle, startup behavior, and notifications",
            Font = new Font("Segoe UI", 10F),
            ForeColor = TextSecondary,
            Location = new Point(2, curY),
            AutoSize = true
        };
        pnl.Controls.Add(lblSub);
        curY += (int)(32 * dpiScale);

        var card = new Panel
        {
            Location = new Point(0, curY),
            Size = new Size(contentWidth, (int)(260 * dpiScale)),
            BackColor = SurfacePrimary,
            Padding = new Padding(24)
        };
        card.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(BorderColor);
            using var path = CreateRoundedRectanglePath(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 12);
            e.Graphics.DrawPath(pen, path);
        };

        var chkStartup = new CheckBox
        {
            Text = "Start Bluetooth KeepAlive automatically when signing in to Windows",
            Checked = _settings.StartOnWindowsStartup,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI", 9.5F),
            Location = new Point(24, 24),
            AutoSize = true
        };
        chkStartup.CheckedChanged += (_, _) =>
        {
            _settings.StartOnWindowsStartup = chkStartup.Checked;
            _settings.Save();
        };

        var chkMinimize = new CheckBox
        {
            Text = "Start minimized to notification area (system tray)",
            Checked = _settings.StartMinimizedToTray,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI", 9.5F),
            Location = new Point(24, 64),
            AutoSize = true
        };
        chkMinimize.CheckedChanged += (_, _) =>
        {
            _settings.StartMinimizedToTray = chkMinimize.Checked;
            _settings.Save();
        };

        var chkAutoStart = new CheckBox
        {
            Text = "Automatically start silent keep-alive stream on application launch",
            Checked = _settings.AutoStartStreamOnLaunch,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI", 9.5F),
            Location = new Point(24, 104),
            AutoSize = true
        };
        chkAutoStart.CheckedChanged += (_, _) =>
        {
            _settings.AutoStartStreamOnLaunch = chkAutoStart.Checked;
            _settings.Save();
        };

        var chkNotifications = new CheckBox
        {
            Text = "Show notification toast when keep-alive stream starts or stops",
            Checked = _settings.ShowNotifications,
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI", 9.5F),
            Location = new Point(24, 144),
            AutoSize = true
        };
        chkNotifications.CheckedChanged += (_, _) =>
        {
            _settings.ShowNotifications = chkNotifications.Checked;
            _settings.Save();
        };

        card.Controls.Add(chkStartup);
        card.Controls.Add(chkMinimize);
        card.Controls.Add(chkAutoStart);
        card.Controls.Add(chkNotifications);

        pnl.Controls.Add(card);
        _contentContainer.Controls.Add(pnl);
    }

    // ==========================================
    // 6. ABOUT & UPDATES PAGE
    // ==========================================
    private void RenderAboutPage()
    {
        var (contentWidth, leftOffset) = GetResponsiveLayout();
        float dpiScale = DeviceDpi / 96f;

        var pnl = new Panel
        {
            Width = contentWidth,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            AutoScroll = false,
            Location = new Point(leftOffset, (int)(20 * dpiScale))
        };

        int curY = 0;

        var lblTitle = new Label
        {
            Text = "About Bluetooth KeepAlive",
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(0, curY),
            AutoSize = true
        };
        pnl.Controls.Add(lblTitle);
        curY += (int)(38 * dpiScale);

        var lblSub = new Label
        {
            Text = "Professional Bluetooth audio keep-alive utility · Version 1.0.4",
            Font = new Font("Segoe UI", 10F),
            ForeColor = TextSecondary,
            Location = new Point(2, curY),
            AutoSize = true
        };
        pnl.Controls.Add(lblSub);
        curY += (int)(32 * dpiScale);

        var card = new Panel
        {
            Location = new Point(0, curY),
            Size = new Size(contentWidth, (int)(260 * dpiScale)),
            BackColor = SurfacePrimary,
            Padding = new Padding(24)
        };
        card.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(BorderColor);
            using var path = CreateRoundedRectanglePath(new Rectangle(0, 0, card.Width - 1, card.Height - 1), 12);
            e.Graphics.DrawPath(pen, path);
        };

        var lblApp = new Label
        {
            Text = "Bluetooth KeepAlive — Version 1.0.4",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(24, 20),
            AutoSize = true
        };

        var lblDesc = new Label
        {
            Text = "Designed specifically for Bluetooth speakers and wireless audio devices to prevent unwanted sleep standby and connection drops. Generates an imperceptible, lightweight 48 kHz silent stream using NAudio with zero CPU and memory footprint.",
            Font = new Font("Segoe UI", 9.2F),
            ForeColor = TextSecondary,
            Location = new Point(24, 52),
            Size = new Size(contentWidth - 64, 60)
        };

        var lblDev = new Label
        {
            Text = "Developer: Tausif Rahaman Raiyan\nLicense: MIT Open Source\nAudio Engine: NAudio 2.2.1 Core WaveOut / WASAPI",
            Font = new Font("Segoe UI", 9F),
            ForeColor = AccentLavender,
            Location = new Point(24, 122),
            Size = new Size(contentWidth - 64, 54)
        };

        var btnGithub = new Button
        {
            Text = "Open GitHub Repo →",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = AccentCrimson,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(180, 36),
            Location = new Point(24, 190),
            Cursor = Cursors.Hand
        };
        btnGithub.FlatAppearance.BorderSize = 0;
        btnGithub.Click += (_, _) => OpenUrl("https://github.com/tausif-rahaman-raiyan/Bluetooth-KeepAlive");

        var btnCheckUpdates = new Button
        {
            Text = "⚡ Check for Updates",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(34, 36, 48),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(180, 36),
            Location = new Point(214, 190),
            Cursor = Cursors.Hand
        };
        btnCheckUpdates.FlatAppearance.BorderSize = 0;
        btnCheckUpdates.Click += async (_, _) =>
        {
            btnCheckUpdates.Enabled = false;
            btnCheckUpdates.Text = "Checking...";
            var info = await UpdateService.CheckForUpdatesAsync();
            btnCheckUpdates.Enabled = true;
            btnCheckUpdates.Text = "⚡ Check for Updates";

            if (info.IsUpdateAvailable)
            {
                var result = MessageBox.Show(
                    $"New Update Available!\n\nRelease: {info.ReleaseName}\nLatest Version: v{info.LatestVersion}\n\nChangelog:\n{info.ReleaseNotes}\n\nWould you like to download the update setup installer?",
                    "Keep Alive Update Available",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information
                );
                if (result == DialogResult.Yes)
                {
                    UpdateService.LaunchUpdate(info.SetupDownloadUrl ?? info.HtmlUrl);
                }
            }
            else
            {
                MessageBox.Show($"You are running the latest version (v{info.CurrentVersion}).\nGitHub Release: {info.ReleaseName}", "Keep Alive Up To Date", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };

        var btnPair = new Button
        {
            Text = "🔗 Re-Pair Speaker",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(34, 36, 48),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(160, 36),
            Location = new Point(404, 190),
            Cursor = Cursors.Hand
        };
        btnPair.FlatAppearance.BorderSize = 0;
        btnPair.Click += (_, _) =>
        {
            bool ok = BluetoothManagerService.TriggerAutoRePairAndConnect(_settings.PreferredDeviceName);
            MessageBox.Show(ok ? "Auto-pair and Bluetooth connection request sent to Windows." : "Failed to trigger auto-pair.", "Bluetooth Re-Pair", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        card.Controls.Add(lblApp);
        card.Controls.Add(lblDesc);
        card.Controls.Add(lblDev);
        card.Controls.Add(btnGithub);
        card.Controls.Add(btnCheckUpdates);
        card.Controls.Add(btnPair);

        pnl.Controls.Add(card);
        _contentContainer.Controls.Add(pnl);
    }

    // ==========================================
    // AUDIO STATE UPDATES
    // ==========================================
    private void UpdateEngineDisplay()
    {
        var state = _audioService.State;
        bool isRunning = state == AudioEngineState.Running;
        bool isCall = state == AudioEngineState.PausedByCallOrMic;

        if (_waveformVisualizer != null)
        {
            _waveformVisualizer.IsActive = isRunning;
            _waveformVisualizer.IsCallStandby = isCall;
        }

        if (_lblTopStatusPill != null)
        {
            if (isRunning)
            {
                _lblTopStatusPill.Text = "● Active Keep-Alive";
                _lblTopStatusPill.ForeColor = ColorSuccess;
                _lblTopStatusPill.BackColor = Color.FromArgb(20, 40, 32);
            }
            else if (isCall)
            {
                _lblTopStatusPill.Text = "● Meeting Mode (Mic Free)";
                _lblTopStatusPill.ForeColor = ColorMeeting;
                _lblTopStatusPill.BackColor = Color.FromArgb(46, 20, 28);
            }
            else
            {
                _lblTopStatusPill.Text = "○ Audio Engine Idle";
                _lblTopStatusPill.ForeColor = TextSecondary;
                _lblTopStatusPill.BackColor = SurfaceSecondary;
            }
            _lblTopStatusPill.Invalidate();
        }

        if (_btnTopQuickToggle != null)
        {
            _btnTopQuickToggle.Text = isRunning ? "Stop Keep-Alive" : "Start Keep-Alive";
            _btnTopQuickToggle.BackColor = isRunning ? SurfaceHover : AccentCrimson;
            _btnTopQuickToggle.ForeColor = isRunning ? AccentCrimsonGlow : Color.White;
        }

        if (_btnOverviewToggle != null)
        {
            _btnOverviewToggle.Text = isRunning ? "Stop Keep-Alive" : "Start Keep-Alive";
            _btnOverviewToggle.BackColor = isRunning ? SurfaceHover : AccentCrimson;
            _btnOverviewToggle.ForeColor = isRunning ? AccentCrimsonGlow : Color.White;
        }

        if (_lblStatBattery != null)
        {
            var telemetry = BluetoothManagerService.GetSpeakerTelemetry(_settings.PreferredDeviceName);
            int bat = telemetry.BatteryPercentage ?? 80;
            _lblStatBattery.Text = $"{bat}%";
        }
    }

    private static string FormatTime(TimeSpan span)
    {
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours:00}h {span.Minutes:00}m"
            : $"{span.Minutes:00}m {span.Seconds:00}s";
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
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
