using System.Diagnostics;
using HocoKeepAlive.Audio;
using HocoKeepAlive.Configuration;
using HocoKeepAlive.Diagnostics;

namespace HocoKeepAlive.UI;

public sealed class MainForm : Form
{
    // Design System Tokens
    public static readonly Color BgMain = Color.FromArgb(14, 18, 32);        // #0E1220
    public static readonly Color BgSidebar = Color.FromArgb(17, 23, 39);     // #111727
    public static readonly Color SurfacePrimary = Color.FromArgb(23, 31, 50); // #171F32
    public static readonly Color SurfaceSecondary = Color.FromArgb(29, 40, 64); // #1D2840
    public static readonly Color SurfaceHover = Color.FromArgb(37, 49, 75);  // #25314B
    public static readonly Color TextPrimary = Color.FromArgb(245, 247, 255); // #F5F7FF
    public static readonly Color TextSecondary = Color.FromArgb(167, 178, 200); // #A7B2C8
    public static readonly Color TextMuted = Color.FromArgb(119, 131, 157);   // #77839D
    public static readonly Color AccentLavender = Color.FromArgb(201, 213, 255); // #C9D5FF
    public static readonly Color BorderColor = Color.FromArgb(43, 54, 80);     // #2B3650
    public static readonly Color ColorSuccess = Color.FromArgb(74, 222, 128);   // #4ADE80
    public static readonly Color ColorWarning = Color.FromArgb(251, 191, 36);   // #FBBF24
    public static readonly Color ColorError = Color.FromArgb(251, 113, 133);    // #FB7185
    public static readonly Color ColorInfo = Color.FromArgb(125, 211, 252);     // #7DD3FC

    private readonly AudioKeepAliveService _audioService;
    private readonly AppSettings _settings;
    private readonly Action _onExit;

    private readonly Panel _sidebarPanel;
    private readonly Panel _contentContainer;
    private readonly Label _lblTopStatusPill;
    private readonly Label _lblTopDeviceLabel;
    private readonly System.Windows.Forms.Timer _statsTimer;
    private readonly System.Windows.Forms.Timer _resizeDebounceTimer = new() { Interval = 120 };

    // Navigation Buttons (5 clean pages)
    private readonly List<Button> _navButtons = new();
    private string _activePage = "Overview";

    // Controls on Overview Page
    private Label? _lblStateTitle;
    private Label? _lblStateDesc;
    private Button? _btnOverviewToggle;
    private Label? _lblOverviewDuration;
    private Label? _lblOverviewDeviceName;
    private Label? _lblOverviewDeviceBadge;

    public MainForm(AudioKeepAliveService audioService, AppSettings settings, Icon appIcon, Action onExit)
    {
        _audioService = audioService;
        _settings = settings;
        _onExit = onExit;

        Icon = appIcon;
        Text = "HOCO KeepAlive";
        ClientSize = new Size(1100, 720);
        MinimumSize = new Size(880, 580);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BgMain;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

        // --- Sidebar (Left, 224px) ---
        _sidebarPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 224,
            BackColor = BgSidebar,
            Padding = new Padding(0)
        };

        // Sidebar Header
        var pnlBrand = new Panel
        {
            Dock = DockStyle.Top,
            Height = 78,
            Padding = new Padding(18, 16, 18, 12),
            BackColor = BgSidebar
        };

        var brandIcon = new PictureBox
        {
            Image = appIcon.ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(38, 38),
            Location = new Point(16, 20),
            BackColor = BgSidebar
        };

        var lblBrandName = new Label
        {
            Text = "HOCO KeepAlive",
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
            Location = new Point(62, 17),
            AutoSize = true
        };

        var lblBrandSub = new Label
        {
            Text = "Bluetooth Audio Utility · v1.0.2",
            ForeColor = TextMuted,
            Font = new Font("Segoe UI", 7.8F),
            Location = new Point(63, 41),
            AutoSize = true
        };

        var brandAccent = new Panel
        {
            BackColor = AccentLavender,
            Size = new Size(3, 34),
            Location = new Point(7, 22)
        };

        pnlBrand.Controls.Add(brandAccent);
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

        AddNavButton(pnlNav, "Overview", 0);
        AddNavButton(pnlNav, "Audio Device", 1);
        AddNavButton(pnlNav, "Keep-Alive Engine", 2);
        AddNavButton(pnlNav, "Settings", 3);
        AddNavButton(pnlNav, "About & Updates", 4);

        // Sidebar Footer
        var pnlSidebarFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 64,
            BackColor = BgSidebar,
            Padding = new Padding(16, 8, 16, 8)
        };

        var lblAutoConnectStatus = new Label
        {
            Text = "● Auto-Reconnect: Active",
            ForeColor = ColorSuccess,
            Font = new Font("Segoe UI", 7.8F, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 18
        };

        var lnkGithub = new LinkLabel
        {
            Text = "GitHub Repository",
            LinkColor = AccentLavender,
            VisitedLinkColor = AccentLavender,
            Font = new Font("Segoe UI", 8F),
            Dock = DockStyle.Bottom,
            Height = 18
        };
        lnkGithub.LinkClicked += (_, _) => OpenUrl("https://github.com/tausif-rahaman-raiyan/HocoKeepAlive");

        pnlSidebarFooter.Controls.Add(lblAutoConnectStatus);
        pnlSidebarFooter.Controls.Add(lnkGithub);

        // In WinForms docking:
        // Adding in order (Fill, Bottom, Top) puts Top at index 0, Bottom at index 1, Fill at index 2.
        // Index 0 docks Top (0 to 78), Index 1 docks Bottom (Height-64 to Height), Index 2 fills the rest.
        _sidebarPanel.Controls.Clear();
        _sidebarPanel.Controls.Add(pnlNav);
        _sidebarPanel.Controls.Add(pnlSidebarFooter);
        _sidebarPanel.Controls.Add(pnlBrand);

        // --- Main Content Area (Right) ---
        var pnlMainArea = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgMain
        };

        // Top Status Header (Matching Web UI)
        var pnlTopHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = BgMain,
            Padding = new Padding(28, 14, 28, 14)
        };

        _lblTopDeviceLabel = new Label
        {
            Text = "Playback Output: " + _settings.PreferredDeviceName,
            Font = new Font("Segoe UI", 9F),
            ForeColor = TextSecondary,
            Location = new Point(28, 18),
            AutoSize = true
        };
        pnlTopHeader.Controls.Add(_lblTopDeviceLabel);

        _lblTopStatusPill = new Label
        {
            Text = "STOPPED",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = TextMuted,
            BackColor = SurfaceSecondary,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(130, 28),
            Dock = DockStyle.Right
        };
        pnlTopHeader.Controls.Add(_lblTopStatusPill);

        // Bottom Watermark Bar (Requested by user: "niche ekta watermark add kre dao with github icon and link")
        var pnlWatermark = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 32,
            BackColor = Color.FromArgb(11, 15, 25),
            Padding = new Padding(28, 0, 28, 0)
        };

        pnlWatermark.Paint += (s, e) =>
        {
            using var pen = new Pen(BorderColor);
            e.Graphics.DrawLine(pen, 0, 0, pnlWatermark.Width, 0);
        };

        var lblWatermarkText = new Label
        {
            Text = "HOCO KeepAlive v1.0.2 • Developed by Tausif Rahaman Raiyan",
            ForeColor = TextMuted,
            Font = new Font("Segoe UI", 8F),
            Dock = DockStyle.Left,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = true
        };

        var lnkWatermarkGithub = new LinkLabel
        {
            Text = "github.com/tausif-rahaman-raiyan/HocoKeepAlive",
            LinkColor = AccentLavender,
            ActiveLinkColor = Color.White,
            VisitedLinkColor = AccentLavender,
            Font = new Font("Segoe UI", 8F),
            Dock = DockStyle.Right,
            TextAlign = ContentAlignment.MiddleRight,
            AutoSize = true,
            Cursor = Cursors.Hand
        };
        lnkWatermarkGithub.LinkClicked += (_, _) => OpenUrl("https://github.com/tausif-rahaman-raiyan/HocoKeepAlive");

        pnlWatermark.Controls.Add(lblWatermarkText);
        pnlWatermark.Controls.Add(lnkWatermarkGithub);

        // Outer Content Container: ONLY scrollable container, scrollbar always at the right edge
        _contentContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgMain,
            Padding = new Padding(0, 0, 0, 16),
            AutoScroll = true
        };
        EnableDoubleBuffering(_contentContainer);
        EnableDoubleBuffering(_sidebarPanel);

        // Adjust card widths on window resize / maximize with clean debouncing
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

        // Add to pnlMainArea in WinForms docking order: Fill first, then Bottom, then Top
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
            if (_audioService.State == AudioEngineState.Running && _lblOverviewDuration != null)
            {
                var dur = _audioService.RunningDuration;
                if (dur.HasValue)
                {
                    _lblOverviewDuration.Text = FormatTime(dur.Value);
                }
            }
        };
        _statsTimer.Start();

        // Load default page
        NavigateTo("Overview");
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
        // Smooth responsive container centering:
        // Capped at 1040px max width (matching web max-w-5xl), centered horizontally in full-screen/maximized mode
        int available = _contentContainer.ClientSize.Width - 64;
        if (available < 600) available = 600;
        int width = Math.Min(1040, available);
        int left = Math.Max(28, (_contentContainer.ClientSize.Width - width) / 2);
        return (width, left);
    }

    private void AddNavButton(Panel container, string title, int index)
    {
        var btn = new Button
        {
            Text = "   " + title,
            Tag = title,
            Size = new Size(204, 42),
            Location = new Point(10, 10 + (index * 48)),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = TextSecondary,
            BackColor = BgSidebar,
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = SurfaceHover;
        btn.Click += (_, _) => NavigateTo(title);

        _navButtons.Add(btn);
        container.Controls.Add(btn);
    }

    public void NavigateTo(string pageTitle)
    {
        _activePage = pageTitle;

        foreach (var b in _navButtons)
        {
            bool isActive = string.Equals(b.Tag as string, pageTitle, StringComparison.Ordinal);
            b.ForeColor = isActive ? AccentLavender : TextSecondary;
            b.BackColor = isActive ? SurfacePrimary : BgSidebar;
            b.Font = new Font("Segoe UI", 9F, isActive ? FontStyle.Bold : FontStyle.Regular);
        }

        RenderCurrentPage();
        UpdateEngineDisplay();
    }

    private void RenderCurrentPage()
    {
        _contentContainer.SuspendLayout();
        _contentContainer.Controls.Clear();

        switch (_activePage)
        {
            case "Overview":
                RenderOverviewPage();
                break;
            case "Audio Device":
                RenderAudioDevicePage();
                break;
            case "Keep-Alive Engine":
                RenderEnginePage();
                break;
            case "Settings":
                RenderSettingsPage();
                break;
            case "About & Updates":
                RenderAboutPage();
                break;
            default:
                RenderOverviewPage();
                break;
        }

        _contentContainer.ResumeLayout(true);
    }

    // ==========================================
    // 1. OVERVIEW PAGE
    // ==========================================
    private void RenderOverviewPage()
    {
        var (contentWidth, leftOffset) = GetResponsiveLayout();

        // Single panel: AutoScroll is FALSE here, so no middle scrollbars ever appear!
        var pnl = new Panel
        {
            Width = contentWidth,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            AutoScroll = false,
            Location = new Point(leftOffset, 16)
        };

        int curY = 0;

        // Title
        var lblTitle = new Label
        {
            Text = "Overview",
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(0, curY),
            AutoSize = true
        };
        pnl.Controls.Add(lblTitle);
        curY += 34;

        var lblSub = new Label
        {
            Text = "Ready to keep audio active. Maintains your Bluetooth speaker connection through quiet moments.",
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = TextMuted,
            Location = new Point(0, curY),
            AutoSize = true
        };
        pnl.Controls.Add(lblSub);
        curY += 30;

        // --- Main Keep-Alive Card ---
        var cardKeepAlive = new Panel
        {
            Location = new Point(0, curY),
            Size = new Size(contentWidth, 160),
            BackColor = SurfacePrimary,
            Padding = new Padding(24)
        };

        var lblStatusTag = new Label
        {
            Text = "KEEP-ALIVE STATUS",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = AccentLavender,
            Location = new Point(24, 18),
            AutoSize = true
        };

        _lblStateTitle = new Label
        {
            Text = "Ready to keep audio active",
            Font = new Font("Segoe UI Semibold", 16F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(22, 38),
            AutoSize = true
        };

        _lblStateDesc = new Label
        {
            Text = "Start a lightweight silent stream to help prevent your Bluetooth speaker from disconnecting during pauses.",
            Font = new Font("Segoe UI", 9F),
            ForeColor = TextSecondary,
            Location = new Point(24, 72),
            Size = new Size(Math.Max(300, contentWidth - 260), 38)
        };

        _btnOverviewToggle = new Button
        {
            Text = "Start Keep-Alive",
            Size = new Size(180, 46),
            Location = new Point(contentWidth - 210, 56),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = BgMain,
            BackColor = AccentLavender,
            Cursor = Cursors.Hand
        };
        _btnOverviewToggle.FlatAppearance.BorderSize = 0;
        _btnOverviewToggle.Click += (_, _) => _audioService.Toggle();

        cardKeepAlive.Controls.Add(lblStatusTag);
        cardKeepAlive.Controls.Add(_lblStateTitle);
        cardKeepAlive.Controls.Add(_lblStateDesc);
        cardKeepAlive.Controls.Add(_btnOverviewToggle);
        pnl.Controls.Add(cardKeepAlive);
        curY += 176;

        // --- Auto-Connect & Auto-Pair Banner ---
        var pnlAutoConnect = new Panel
        {
            Location = new Point(0, curY),
            Size = new Size(contentWidth, 68),
            BackColor = SurfaceSecondary,
            Padding = new Padding(20, 14, 20, 14)
        };

        var lblAutoTitle = new Label
        {
            Text = "Auto-Reconnect & Auto-Pair: Active",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = ColorSuccess,
            Location = new Point(18, 14),
            AutoSize = true
        };

        var lblAutoDesc = new Label
        {
            Text = "Automatically reconnects silent stream when HOCO HC22 reconnects or wakes up from standby.",
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = TextSecondary,
            Location = new Point(18, 36),
            AutoSize = true
        };

        var btnPairAssistant = new Button
        {
            Text = "Pair / Connect Speaker",
            Location = new Point(contentWidth - 190, 18),
            Size = new Size(170, 32),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            FlatStyle = FlatStyle.Flat,
            BackColor = SurfaceHover,
            ForeColor = AccentLavender,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnPairAssistant.FlatAppearance.BorderSize = 0;
        btnPairAssistant.Click += (_, _) => _audioService.LaunchAutoPairAssistant();

        pnlAutoConnect.Controls.Add(lblAutoTitle);
        pnlAutoConnect.Controls.Add(lblAutoDesc);
        pnlAutoConnect.Controls.Add(btnPairAssistant);
        pnl.Controls.Add(pnlAutoConnect);
        curY += 84;

        // --- Two Column Row (Audio Device + Session Summary) ---
        int halfWidth = (contentWidth - 16) / 2;

        var rowPanel = new Panel
        {
            Location = new Point(0, curY),
            Size = new Size(contentWidth, 160)
        };

        // Device Summary Card (Left)
        var cardDevice = new Panel
        {
            Size = new Size(halfWidth, 160),
            Location = new Point(0, 0),
            BackColor = SurfacePrimary,
            Padding = new Padding(20)
        };

        var lblDevHeader = new Label
        {
            Text = "AUDIO PLAYBACK DEVICE",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = TextMuted,
            Location = new Point(20, 16),
            AutoSize = true
        };

        _lblOverviewDeviceName = new Label
        {
            Text = _settings.PreferredDeviceName,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(18, 40),
            Size = new Size(halfWidth - 36, 44)
        };

        _lblOverviewDeviceBadge = new Label
        {
            Text = "● Ready & Monitored",
            Font = new Font("Segoe UI", 8F),
            ForeColor = ColorSuccess,
            Location = new Point(20, 88),
            AutoSize = true
        };

        var btnManageDev = new Button
        {
            Text = "Manage Devices →",
            Location = new Point(20, 116),
            Size = new Size(130, 28),
            FlatStyle = FlatStyle.Flat,
            ForeColor = AccentLavender,
            BackColor = SurfaceSecondary,
            Font = new Font("Segoe UI", 8.5F),
            Cursor = Cursors.Hand
        };
        btnManageDev.FlatAppearance.BorderSize = 0;
        btnManageDev.Click += (_, _) => NavigateTo("Audio Device");

        cardDevice.Controls.Add(lblDevHeader);
        cardDevice.Controls.Add(_lblOverviewDeviceName);
        cardDevice.Controls.Add(_lblOverviewDeviceBadge);
        cardDevice.Controls.Add(btnManageDev);
        rowPanel.Controls.Add(cardDevice);

        // Session Summary Card (Right)
        var cardSession = new Panel
        {
            Size = new Size(halfWidth, 160),
            Location = new Point(halfWidth + 16, 0),
            BackColor = SurfacePrimary,
            Padding = new Padding(20)
        };

        var lblSessHeader = new Label
        {
            Text = "ACTIVE SESSION TELEMETRY",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = TextMuted,
            Location = new Point(20, 16),
            AutoSize = true
        };

        _lblOverviewDuration = new Label
        {
            Text = "00m 00s",
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(18, 38),
            AutoSize = true
        };

        var lblEngineSpecs = new Label
        {
            Text = "Format: 48,000 Hz • 16-bit stereo PCM\nEngine: Low-overhead NAudio callback",
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = TextSecondary,
            Location = new Point(20, 78),
            Size = new Size(halfWidth - 36, 34)
        };

        var btnEngineDetails = new Button
        {
            Text = "Engine Details →",
            Location = new Point(20, 116),
            Size = new Size(120, 28),
            FlatStyle = FlatStyle.Flat,
            ForeColor = AccentLavender,
            BackColor = SurfaceSecondary,
            Font = new Font("Segoe UI", 8.5F),
            Cursor = Cursors.Hand
        };
        btnEngineDetails.FlatAppearance.BorderSize = 0;
        btnEngineDetails.Click += (_, _) => NavigateTo("Keep-Alive Engine");

        cardSession.Controls.Add(lblSessHeader);
        cardSession.Controls.Add(_lblOverviewDuration);
        cardSession.Controls.Add(lblEngineSpecs);
        cardSession.Controls.Add(btnEngineDetails);
        rowPanel.Controls.Add(cardSession);

        pnl.Controls.Add(rowPanel);

        _contentContainer.Controls.Add(pnl);
    }

    // ==========================================
    // 2. AUDIO DEVICE PAGE
    // ==========================================
    private void RenderAudioDevicePage()
    {
        var (contentWidth, leftOffset) = GetResponsiveLayout();

        var pnl = new Panel
        {
            Width = contentWidth,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            AutoScroll = false,
            Location = new Point(leftOffset, 16)
        };

        int curY = 0;

        var lblTitle = new Label
        {
            Text = "Audio Playback Device",
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(0, curY),
            AutoSize = true
        };
        pnl.Controls.Add(lblTitle);
        curY += 34;

        var lblSub = new Label
        {
            Text = "Select your HOCO HC22 or target Windows audio endpoint for the silent keep-alive stream.",
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = TextMuted,
            Location = new Point(0, curY),
            AutoSize = true
        };
        pnl.Controls.Add(lblSub);
        curY += 28;

        // Toolbar
        var pnlBar = new Panel { Location = new Point(0, curY), Size = new Size(contentWidth, 40) };
        var btnRefresh = new Button
        {
            Text = "Refresh Device List",
            Size = new Size(150, 32),
            Location = new Point(0, 4),
            FlatStyle = FlatStyle.Flat,
            BackColor = SurfaceSecondary,
            ForeColor = AccentLavender,
            Cursor = Cursors.Hand
        };
        btnRefresh.FlatAppearance.BorderSize = 0;
        btnRefresh.Click += (_, _) =>
        {
            LogService.Log(LogSeverity.Info, "AudioDevice", "Refreshed audio devices list.");
            RenderCurrentPage();
        };

        var btnPair = new Button
        {
            Text = "Open Bluetooth Pairing",
            Size = new Size(170, 32),
            Location = new Point(160, 4),
            FlatStyle = FlatStyle.Flat,
            BackColor = SurfaceSecondary,
            ForeColor = ColorInfo,
            Cursor = Cursors.Hand
        };
        btnPair.FlatAppearance.BorderSize = 0;
        btnPair.Click += (_, _) => _audioService.LaunchAutoPairAssistant();

        var btnSoundSettings = new Button
        {
            Text = "Windows Sound Settings",
            Size = new Size(170, 32),
            Location = new Point(340, 4),
            FlatStyle = FlatStyle.Flat,
            BackColor = SurfaceSecondary,
            ForeColor = TextSecondary,
            Cursor = Cursors.Hand
        };
        btnSoundSettings.FlatAppearance.BorderSize = 0;
        btnSoundSettings.Click += (_, _) => OpenWindowsSoundSettings();

        pnlBar.Controls.Add(btnRefresh);
        pnlBar.Controls.Add(btnPair);
        pnlBar.Controls.Add(btnSoundSettings);
        pnl.Controls.Add(pnlBar);
        curY += 48;

        // Device List Items
        var devices = AudioDeviceService.GetPlaybackDevices();
        if (devices.Count == 0)
        {
            var pnlEmpty = new Panel { Location = new Point(0, curY), Size = new Size(contentWidth, 140), BackColor = SurfacePrimary, Padding = new Padding(24) };
            var lblEmpty = new Label
            {
                Text = "No Windows playback devices detected.\nTurn on HOCO HC22 Bluetooth and click 'Refresh Device List'.",
                ForeColor = TextSecondary,
                Font = new Font("Segoe UI", 10F),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            pnlEmpty.Controls.Add(lblEmpty);
            pnl.Controls.Add(pnlEmpty);
        }
        else
        {
            foreach (var dev in devices)
            {
                bool isSelected = dev.DeviceNumber == _audioService.TargetDeviceNumber;
                var itemCard = new Panel
                {
                    Location = new Point(0, curY),
                    Size = new Size(contentWidth, 68),
                    BackColor = isSelected ? SurfaceSecondary : SurfacePrimary,
                    Padding = new Padding(18, 12, 18, 12)
                };

                var lblDevName = new Label
                {
                    Text = dev.Name,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    ForeColor = TextPrimary,
                    Location = new Point(18, 12),
                    Size = new Size(Math.Max(300, contentWidth - 180), 22)
                };

                var lblDevMeta = new Label
                {
                    Text = dev.DeviceNumber == -1 ? "System Default Route • DirectSound/WaveOut" : $"WaveOut Device #{dev.DeviceNumber}",
                    Font = new Font("Segoe UI", 8F),
                    ForeColor = TextMuted,
                    Location = new Point(18, 36),
                    AutoSize = true
                };

                var btnSelect = new Button
                {
                    Text = isSelected ? "Active Target" : "Select Device",
                    Size = new Size(120, 32),
                    Location = new Point(contentWidth - 140, 18),
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = isSelected ? AccentLavender : SurfaceHover,
                    ForeColor = isSelected ? BgMain : TextSecondary,
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    Enabled = !isSelected,
                    Cursor = isSelected ? Cursors.Default : Cursors.Hand
                };
                btnSelect.FlatAppearance.BorderSize = 0;
                btnSelect.Click += (_, _) =>
                {
                    _audioService.TargetDeviceNumber = dev.DeviceNumber;
                    _settings.PreferredDeviceNumber = dev.DeviceNumber;
                    _settings.PreferredDeviceName = dev.Name;
                    _settings.Save();
                    RenderCurrentPage();
                };

                itemCard.Controls.Add(lblDevName);
                itemCard.Controls.Add(lblDevMeta);
                itemCard.Controls.Add(btnSelect);
                pnl.Controls.Add(itemCard);
                curY += 76;
            }
        }

        _contentContainer.Controls.Add(pnl);
    }

    // ==========================================
    // 3. KEEP-ALIVE ENGINE PAGE
    // ==========================================
    private void RenderEnginePage()
    {
        var (contentWidth, leftOffset) = GetResponsiveLayout();

        var pnl = new Panel
        {
            Width = contentWidth,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            AutoScroll = false,
            Location = new Point(leftOffset, 16)
        };

        int curY = 0;

        var lblTitle = new Label
        {
            Text = "Keep-Alive Engine",
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(0, curY),
            AutoSize = true
        };
        pnl.Controls.Add(lblTitle);
        curY += 34;

        var lblSub = new Label
        {
            Text = "Audio pipeline specifications, latency tuning, and stream telemetry.",
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = TextMuted,
            Location = new Point(0, curY),
            AutoSize = true
        };
        pnl.Controls.Add(lblSub);
        curY += 28;

        // Telemetry Grid
        var pnlGrid = new Panel { Location = new Point(0, curY), Size = new Size(contentWidth, 95) };
        int boxWidth = (contentWidth - 36) / 4;
        AddMetricBox(pnlGrid, 0, boxWidth, "SAMPLE RATE", $"{_settings.SampleRate} Hz");
        AddMetricBox(pnlGrid, boxWidth + 12, boxWidth, "CHANNELS", $"{_settings.Channels} (Stereo)");
        AddMetricBox(pnlGrid, (boxWidth + 12) * 2, boxWidth, "BUFFER LATENCY", $"{_settings.LatencyMilliseconds} ms");
        AddMetricBox(pnlGrid, (boxWidth + 12) * 3, boxWidth, "AMPLITUDE", "0.000 (-∞ dB)");
        pnl.Controls.Add(pnlGrid);
        curY += 110;

        // Advanced Configuration Card
        var pnlConfig = new Panel
        {
            Location = new Point(0, curY),
            Size = new Size(contentWidth, 230),
            BackColor = SurfacePrimary,
            Padding = new Padding(24)
        };

        var lblCfgTitle = new Label
        {
            Text = "Pipeline Tuning & Auto-Reconnection",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(20, 16),
            AutoSize = true
        };

        var lblSr = new Label { Text = "Audio Sample Rate:", Location = new Point(20, 52), AutoSize = true, ForeColor = TextSecondary };
        var cboSr = new ComboBox { Location = new Point(20, 74), Size = new Size(220, 26), DropDownStyle = ComboBoxStyle.DropDownList };
        cboSr.Items.AddRange(new object[] { "48000 Hz (Standard DVD/BT)", "44100 Hz (CD Audio)" });
        cboSr.SelectedIndex = _settings.SampleRate == 44100 ? 1 : 0;
        cboSr.SelectedIndexChanged += (_, _) =>
        {
            _settings.SampleRate = cboSr.SelectedIndex == 1 ? 44100 : 48000;
            _audioService.SampleRate = _settings.SampleRate;
            _settings.Save();
        };

        var lblLat = new Label { Text = "Buffer Latency:", Location = new Point(260, 52), AutoSize = true, ForeColor = TextSecondary };
        var cboLat = new ComboBox { Location = new Point(260, 74), Size = new Size(220, 26), DropDownStyle = ComboBoxStyle.DropDownList };
        cboLat.Items.AddRange(new object[] { "100 ms (Low Latency)", "150 ms (Balanced - Recommended)", "250 ms (High Stability)" });
        cboLat.SelectedIndex = _settings.LatencyMilliseconds switch { 100 => 0, 250 => 2, _ => 1 };
        cboLat.SelectedIndexChanged += (_, _) =>
        {
            _settings.LatencyMilliseconds = cboLat.SelectedIndex switch { 0 => 100, 2 => 250, _ => 150 };
            _audioService.LatencyMs = _settings.LatencyMilliseconds;
            _settings.Save();
        };

        var chkReconnect = new CheckBox
        {
            Text = "Auto-reconnect automatically when speaker disconnects or reconnects",
            Location = new Point(20, 120),
            AutoSize = true,
            Checked = _settings.AutoReconnectOnDeviceAvailable,
            ForeColor = ColorSuccess,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        chkReconnect.CheckedChanged += (_, _) =>
        {
            _settings.AutoReconnectOnDeviceAvailable = chkReconnect.Checked;
            _audioService.AutoReconnect = chkReconnect.Checked;
            _settings.Save();
        };

        var chkAutoPause = new CheckBox
        {
            Text = "Smart Auto-Pause: Turn off silent stream when other audio plays, auto-resume when paused",
            Location = new Point(20, 150),
            AutoSize = true,
            Checked = _settings.AutoPauseOnOtherAudio,
            ForeColor = AccentLavender,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        chkAutoPause.CheckedChanged += (_, _) =>
        {
            _settings.AutoPauseOnOtherAudio = chkAutoPause.Checked;
            _audioService.AutoPauseOnOtherAudio = chkAutoPause.Checked;
            _settings.Save();
        };

        var btnTestChime = new Button
        {
            Text = "Play 0.5s Hardware Verification Chime",
            Size = new Size(270, 34),
            Location = new Point(20, 194),
            FlatStyle = FlatStyle.Flat,
            BackColor = SurfaceSecondary,
            ForeColor = AccentLavender,
            Cursor = Cursors.Hand
        };
        btnTestChime.FlatAppearance.BorderSize = 0;
        btnTestChime.Click += (_, _) => PlayVerificationSound();

        pnlConfig.Size = new Size(contentWidth, 250);
        pnlConfig.Controls.Add(lblCfgTitle);
        pnlConfig.Controls.Add(lblSr);
        pnlConfig.Controls.Add(cboSr);
        pnlConfig.Controls.Add(lblLat);
        pnlConfig.Controls.Add(cboLat);
        pnlConfig.Controls.Add(chkReconnect);
        pnlConfig.Controls.Add(chkAutoPause);
        pnlConfig.Controls.Add(btnTestChime);
        pnl.Controls.Add(pnlConfig);

        _contentContainer.Controls.Add(pnl);
    }

    private void AddMetricBox(Panel parent, int x, int width, string label, string value)
    {
        var box = new Panel
        {
            Location = new Point(x, 0),
            Size = new Size(width, 90),
            BackColor = SurfacePrimary,
            Padding = new Padding(16)
        };

        var lblTag = new Label
        {
            Text = label,
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = TextMuted,
            Location = new Point(14, 14),
            AutoSize = true
        };

        var lblVal = new Label
        {
            Text = value,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(12, 38),
            AutoSize = true
        };

        box.Controls.Add(lblTag);
        box.Controls.Add(lblVal);
        parent.Controls.Add(box);
    }

    // ==========================================
    // 4. SETTINGS PAGE
    // ==========================================
    private void RenderSettingsPage()
    {
        var (contentWidth, leftOffset) = GetResponsiveLayout();

        var pnl = new Panel
        {
            Width = contentWidth,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            AutoScroll = false,
            Location = new Point(leftOffset, 16)
        };

        int curY = 0;

        var lblTitle = new Label
        {
            Text = "Settings",
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(0, curY),
            AutoSize = true
        };
        pnl.Controls.Add(lblTitle);
        curY += 34;

        var lblSub = new Label
        {
            Text = "Configure startup, background tray behavior, and auto-pairing preferences.",
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = TextMuted,
            Location = new Point(0, curY),
            AutoSize = true
        };
        pnl.Controls.Add(lblSub);
        curY += 28;

        // General Group Card
        var pnlGeneral = new Panel
        {
            Location = new Point(0, curY),
            Size = new Size(contentWidth, 195),
            BackColor = SurfacePrimary,
            Padding = new Padding(24)
        };

        var lblGenHeader = new Label { Text = "General & Windows Startup", Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = TextPrimary, Location = new Point(20, 14), AutoSize = true };

        var chkStartup = new CheckBox
        {
            Text = "Start HOCO KeepAlive when I sign in to Windows (registers HKCU Run key)",
            Location = new Point(20, 48),
            AutoSize = true,
            Checked = _settings.StartOnWindowsStartup,
            ForeColor = TextSecondary
        };
        chkStartup.CheckedChanged += (_, _) =>
        {
            _settings.StartOnWindowsStartup = chkStartup.Checked;
            _settings.Save();
        };

        var chkAutoStream = new CheckBox
        {
            Text = "Start silent keep-alive stream automatically on app launch",
            Location = new Point(20, 80),
            AutoSize = true,
            Checked = _settings.AutoStartStreamOnLaunch,
            ForeColor = TextSecondary
        };
        chkAutoStream.CheckedChanged += (_, _) =>
        {
            _settings.AutoStartStreamOnLaunch = chkAutoStream.Checked;
            _settings.Save();
        };

        var chkAutoReconnect = new CheckBox
        {
            Text = "Automatically reconnect stream if speaker drops connection or turns off",
            Location = new Point(20, 112),
            AutoSize = true,
            Checked = _settings.AutoReconnectOnDeviceAvailable,
            ForeColor = ColorSuccess,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        chkAutoReconnect.CheckedChanged += (_, _) =>
        {
            _settings.AutoReconnectOnDeviceAvailable = chkAutoReconnect.Checked;
            _audioService.AutoReconnect = chkAutoReconnect.Checked;
            _settings.Save();
        };

        var chkAutoPause = new CheckBox
        {
            Text = "Smart Auto-Pause: Turn off silent stream when other audio plays, auto-resume when paused",
            Location = new Point(20, 144),
            AutoSize = true,
            Checked = _settings.AutoPauseOnOtherAudio,
            ForeColor = AccentLavender,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        chkAutoPause.CheckedChanged += (_, _) =>
        {
            _settings.AutoPauseOnOtherAudio = chkAutoPause.Checked;
            _audioService.AutoPauseOnOtherAudio = chkAutoPause.Checked;
            _settings.Save();
        };

        var chkCloseToTray = new CheckBox
        {
            Text = "Minimize to notification area (tray) when closing window",
            Location = new Point(20, 176),
            AutoSize = true,
            Checked = _settings.MinimizeToTrayOnClose,
            ForeColor = TextSecondary
        };
        chkCloseToTray.CheckedChanged += (_, _) =>
        {
            _settings.MinimizeToTrayOnClose = chkCloseToTray.Checked;
            _settings.Save();
        };

        pnlGeneral.Size = new Size(contentWidth, 235);
        pnlGeneral.Controls.Add(lblGenHeader);
        pnlGeneral.Controls.Add(chkStartup);
        pnlGeneral.Controls.Add(chkAutoStream);
        pnlGeneral.Controls.Add(chkAutoReconnect);
        pnlGeneral.Controls.Add(chkAutoPause);
        pnlGeneral.Controls.Add(chkCloseToTray);
        pnl.Controls.Add(pnlGeneral);
        curY += 245;

        // Notifications Group Card
        var pnlNotif = new Panel
        {
            Location = new Point(0, curY),
            Size = new Size(contentWidth, 110),
            BackColor = SurfacePrimary,
            Padding = new Padding(24)
        };

        var lblNotifHeader = new Label { Text = "Notification Area Alerts", Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = TextPrimary, Location = new Point(20, 14), AutoSize = true };

        var chkNotifyEvents = new CheckBox
        {
            Text = "Show balloon notifications on stream start, stop, or device reconnect",
            Location = new Point(20, 50),
            AutoSize = true,
            Checked = _settings.ShowNotifications,
            ForeColor = TextSecondary
        };
        chkNotifyEvents.CheckedChanged += (_, _) =>
        {
            _settings.ShowNotifications = chkNotifyEvents.Checked;
            _settings.Save();
        };

        pnlNotif.Controls.Add(lblNotifHeader);
        pnlNotif.Controls.Add(chkNotifyEvents);
        pnl.Controls.Add(pnlNotif);

        _contentContainer.Controls.Add(pnl);
    }

    // ==========================================
    // 5. ABOUT & UPDATES PAGE
    // ==========================================
    private void RenderAboutPage()
    {
        var (contentWidth, leftOffset) = GetResponsiveLayout();

        var pnl = new Panel
        {
            Width = contentWidth,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            AutoScroll = false,
            Location = new Point(leftOffset, 16)
        };

        int curY = 0;

        var lblTitle = new Label { Text = "About & Updates", Font = new Font("Segoe UI", 18F, FontStyle.Bold), ForeColor = TextPrimary, Location = new Point(0, curY), AutoSize = true };
        pnl.Controls.Add(lblTitle);
        curY += 34;

        var lblSub = new Label { Text = "HOCO KeepAlive build specifications, licensing, and update checks.", Font = new Font("Segoe UI", 9.5F), ForeColor = TextMuted, Location = new Point(0, curY), AutoSize = true };
        pnl.Controls.Add(lblSub);
        curY += 28;

        var card = new Panel
        {
            Location = new Point(0, curY),
            Size = new Size(contentWidth, 230),
            BackColor = SurfacePrimary,
            Padding = new Padding(24)
        };

        var lblAppName = new Label { Text = "HOCO KeepAlive", Font = new Font("Segoe UI", 14F, FontStyle.Bold), ForeColor = TextPrimary, Location = new Point(20, 16), AutoSize = true };
        var lblVer = new Label { Text = "Version 1.0.2 (Release x64) • Self-Contained Single-File", Font = new Font("Segoe UI", 9F), ForeColor = AccentLavender, Location = new Point(22, 44), AutoSize = true };

        var lblSpecs = new Label
        {
            Text = "Runtime: Microsoft .NET 8 (Windows Forms)\n" +
                   "Audio Engine: NAudio 2.2.1 WaveOutEvent / Auto-Reconnect Watchdog\n" +
                   "Branding: Dual Cute Earbuds (Pale Lavender & White)\n" +
                   "License: MIT License • Free & Open Source\n" +
                   "Publisher: Raiyan",
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = TextSecondary,
            Location = new Point(22, 72),
            Size = new Size(Math.Max(300, contentWidth - 44), 80)
        };

        var btnReleases = new Button
        {
            Text = "View GitHub Releases",
            Location = new Point(22, 168),
            Size = new Size(160, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = SurfaceSecondary,
            ForeColor = AccentLavender,
            Cursor = Cursors.Hand
        };
        btnReleases.FlatAppearance.BorderSize = 0;
        btnReleases.Click += (_, _) => OpenUrl("https://github.com/tausif-rahaman-raiyan/HocoKeepAlive/releases");

        card.Controls.Add(lblAppName);
        card.Controls.Add(lblVer);
        card.Controls.Add(lblSpecs);
        card.Controls.Add(btnReleases);
        pnl.Controls.Add(card);

        _contentContainer.Controls.Add(pnl);
    }

    // ==========================================
    // UI Helpers & Engine State Updates
    // ==========================================
    private void UpdateEngineDisplay()
    {
        var state = _audioService.State;
        switch (state)
        {
            case AudioEngineState.Running:
                _lblTopStatusPill.Text = "RUNNING SILENTLY";
                _lblTopStatusPill.BackColor = Color.FromArgb(20, 83, 45);
                _lblTopStatusPill.ForeColor = ColorSuccess;

                if (_btnOverviewToggle != null)
                {
                    _btnOverviewToggle.Text = "Stop Keep-Alive";
                    _btnOverviewToggle.BackColor = ColorError;
                    _btnOverviewToggle.ForeColor = Color.White;
                }
                if (_lblStateTitle != null) _lblStateTitle.Text = "Keep-Alive is running";
                if (_lblStateDesc != null) _lblStateDesc.Text = "Digital silence is actively maintaining the Bluetooth audio link.";
                break;

            case AudioEngineState.PausedByOtherAudio:
                _lblTopStatusPill.Text = "STANDBY (MEDIA PLAYING)";
                _lblTopStatusPill.BackColor = Color.FromArgb(49, 46, 129);
                _lblTopStatusPill.ForeColor = AccentLavender;

                if (_btnOverviewToggle != null)
                {
                    _btnOverviewToggle.Text = "Stop Keep-Alive";
                    _btnOverviewToggle.BackColor = Color.FromArgb(71, 85, 105);
                    _btnOverviewToggle.ForeColor = TextPrimary;
                }
                if (_lblStateTitle != null) _lblStateTitle.Text = "Standing by (Audio active elsewhere)";
                if (_lblStateDesc != null) _lblStateDesc.Text = "Background silent stream paused while other media plays. Stream will automatically resume when audio pauses.";
                break;

            case AudioEngineState.Recovering:
                _lblTopStatusPill.Text = "AUTO-RECONNECTING...";
                _lblTopStatusPill.BackColor = Color.FromArgb(113, 63, 18);
                _lblTopStatusPill.ForeColor = ColorWarning;

                if (_btnOverviewToggle != null)
                {
                    _btnOverviewToggle.Text = "Auto-Reconnecting...";
                    _btnOverviewToggle.BackColor = ColorWarning;
                    _btnOverviewToggle.ForeColor = BgMain;
                }
                if (_lblStateTitle != null) _lblStateTitle.Text = "Reconnecting to Speaker...";
                if (_lblStateDesc != null) _lblStateDesc.Text = "Waiting for HOCO HC22 audio link to reconnect. Stream will resume automatically.";
                break;

            case AudioEngineState.DeviceUnavailable:
                _lblTopStatusPill.Text = "DEVICE UNAVAILABLE";
                _lblTopStatusPill.BackColor = Color.FromArgb(113, 63, 18);
                _lblTopStatusPill.ForeColor = ColorWarning;

                if (_btnOverviewToggle != null)
                {
                    _btnOverviewToggle.Text = "Retry / Pair Device";
                    _btnOverviewToggle.BackColor = ColorWarning;
                    _btnOverviewToggle.ForeColor = BgMain;
                }
                if (_lblStateTitle != null) _lblStateTitle.Text = "Device unavailable";
                if (_lblStateDesc != null) _lblStateDesc.Text = "Target audio device is disconnected. Auto-reconnect watchdog is monitoring.";
                break;

            case AudioEngineState.Starting:
                _lblTopStatusPill.Text = "STARTING...";
                _lblTopStatusPill.BackColor = Color.FromArgb(30, 58, 138);
                _lblTopStatusPill.ForeColor = ColorInfo;

                if (_btnOverviewToggle != null)
                {
                    _btnOverviewToggle.Text = "Starting...";
                    _btnOverviewToggle.BackColor = ColorInfo;
                    _btnOverviewToggle.ForeColor = BgMain;
                }
                if (_lblStateTitle != null) _lblStateTitle.Text = "Starting audio engine...";
                if (_lblStateDesc != null) _lblStateDesc.Text = "Initializing the selected playback device.";
                break;

            case AudioEngineState.Error:
                _lblTopStatusPill.Text = "ERROR";
                _lblTopStatusPill.BackColor = Color.FromArgb(136, 19, 55);
                _lblTopStatusPill.ForeColor = ColorError;

                if (_btnOverviewToggle != null)
                {
                    _btnOverviewToggle.Text = "Retry Keep-Alive";
                    _btnOverviewToggle.BackColor = ColorError;
                    _btnOverviewToggle.ForeColor = Color.White;
                }
                if (_lblStateTitle != null) _lblStateTitle.Text = "Unable to start Keep-Alive";
                if (_lblStateDesc != null) _lblStateDesc.Text = _audioService.LastError ?? "Audio initialization failed.";
                break;

            default:
                _lblTopStatusPill.Text = "STOPPED";
                _lblTopStatusPill.BackColor = SurfaceSecondary;
                _lblTopStatusPill.ForeColor = TextMuted;

                if (_btnOverviewToggle != null)
                {
                    _btnOverviewToggle.Text = "Start Keep-Alive";
                    _btnOverviewToggle.BackColor = AccentLavender;
                    _btnOverviewToggle.ForeColor = BgMain;
                }
                if (_lblStateTitle != null) _lblStateTitle.Text = "Ready to keep audio active";
                if (_lblStateDesc != null) _lblStateDesc.Text = "Start a lightweight silent stream to help prevent your Bluetooth speaker from disconnecting during pauses.";
                break;
        }

        if (_lblOverviewDeviceName != null)
        {
            _lblOverviewDeviceName.Text = _settings.PreferredDeviceName;
        }

        if (_lblTopDeviceLabel != null)
        {
            _lblTopDeviceLabel.Text = "Playback Output: " + _settings.PreferredDeviceName;
        }
    }

    private static string FormatTime(TimeSpan span)
    {
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours:D2}h {span.Minutes:D2}m {span.Seconds:D2}s"
            : $"{span.Minutes:D2}m {span.Seconds:D2}s";
    }

    private void PlayVerificationSound()
    {
        try
        {
            System.Media.SystemSounds.Asterisk.Play();
            LogService.Log(LogSeverity.Info, "Audio", "Played hardware verification chime.");
        }
        catch (Exception ex)
        {
            LogService.Log(LogSeverity.Warning, "Audio", "Could not play verification sound: " + ex.Message);
        }
    }

    private static void OpenWindowsSoundSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = "ms-settings:sound", UseShellExecute = true });
        }
        catch
        {
            try { Process.Start(new ProcessStartInfo { FileName = "mmsys.cpl", UseShellExecute = true }); } catch { }
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch { }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_settings.MinimizeToTrayOnClose && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            LogService.Log(LogSeverity.Info, "UI", "Main window minimized to notification area.");
        }
        else
        {
            _resizeDebounceTimer.Stop();
            _resizeDebounceTimer.Dispose();
            _statsTimer.Stop();
            _statsTimer.Dispose();
            base.OnFormClosing(e);
        }
    }
}
