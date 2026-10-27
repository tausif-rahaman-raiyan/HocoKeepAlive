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
    private readonly System.Windows.Forms.Timer _statsTimer;

    // Navigation Buttons
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
        ClientSize = new Size(1120, 760);
        MinimumSize = new Size(1000, 650);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = BgMain;
        ForeColor = TextPrimary;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

        // --- Sidebar (Left, 220px) ---
        _sidebarPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 220,
            BackColor = BgSidebar,
            Padding = new Padding(0)
        };

        // Sidebar Header
        var pnlBrand = new Panel
        {
            Dock = DockStyle.Top,
            Height = 76,
            Padding = new Padding(18, 16, 18, 12),
            BackColor = BgSidebar
        };

        // Compact gadget-style brand mark using the same multi-resolution app icon.
        var brandIcon = new PictureBox
        {
            Image = appIcon.ToBitmap(),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(38, 38),
            Location = new Point(16, 18),
            BackColor = BgSidebar
        };

        var lblBrandName = new Label
        {
            Text = "HOCO KeepAlive",
            ForeColor = TextPrimary,
            Font = new Font("Segoe UI Semibold", 11F, FontStyle.Bold),
            Location = new Point(62, 15),
            AutoSize = true
        };

        var lblBrandSub = new Label
        {
            Text = "Bluetooth Audio Utility · v1.0.0",
            ForeColor = TextMuted,
            Font = new Font("Segoe UI", 7.8F),
            Location = new Point(63, 39),
            AutoSize = true
        };

        var brandAccent = new Panel
        {
            BackColor = AccentLavender,
            Size = new Size(3, 34),
            Location = new Point(7, 20)
        };

        pnlBrand.Controls.Add(brandAccent);
        pnlBrand.Controls.Add(brandIcon);
        pnlBrand.Controls.Add(lblBrandName);
        pnlBrand.Controls.Add(lblBrandSub);
        _sidebarPanel.Controls.Add(pnlBrand);

        // Sidebar Navigation Container
        var pnlNav = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgSidebar,
            Padding = new Padding(12, 10, 12, 10)
        };

        AddNavButton(pnlNav, "Overview", 0);
        AddNavButton(pnlNav, "Audio Device", 1);
        AddNavButton(pnlNav, "Keep-Alive Engine", 2);
        AddNavButton(pnlNav, "Settings", 3);
        AddNavButton(pnlNav, "Troubleshooting", 4);
        AddNavButton(pnlNav, "Diagnostics & Logs", 5);
        AddNavButton(pnlNav, "About & Updates", 6);

        _sidebarPanel.Controls.Add(pnlNav);

        // Sidebar Footer
        var pnlSidebarFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            BackColor = BgSidebar,
            Padding = new Padding(16, 8, 16, 8)
        };

        var lblStatusIndicator = new Label
        {
            Text = "● Ready",
            ForeColor = TextMuted,
            Font = new Font("Segoe UI", 8F),
            Dock = DockStyle.Top,
            Height = 20
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

        pnlSidebarFooter.Controls.Add(lblStatusIndicator);
        pnlSidebarFooter.Controls.Add(lnkGithub);
        _sidebarPanel.Controls.Add(pnlSidebarFooter);
        // Keep the fill-docked navigation behind the fixed header and footer.
        pnlNav.SendToBack();
        pnlBrand.BringToFront();
        pnlSidebarFooter.BringToFront();

        // --- Main Content Area (Right) ---
        var pnlMainArea = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgMain
        };

        // Top Status Header
        var pnlTopHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = BgMain,
            Padding = new Padding(24, 12, 24, 12)
        };

        _lblTopStatusPill = new Label
        {
            Text = "STOPPED",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = TextMuted,
            BackColor = SurfaceSecondary,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(110, 28),
            Dock = DockStyle.Right,
            Margin = new Padding(0, 0, 0, 0)
        };
        pnlTopHeader.Controls.Add(_lblTopStatusPill);

        _contentContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = BgMain,
            Padding = new Padding(28, 10, 28, 20),
            AutoScroll = true
        };
        EnableDoubleBuffering(_contentContainer);
        EnableDoubleBuffering(_sidebarPanel);

        pnlMainArea.Controls.Add(_contentContainer);
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
                catch (InvalidOperationException) { /* The form is closing. */ }
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
        // Reduce flicker when pages and status indicators refresh.
        typeof(Control).GetProperty("DoubleBuffered",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(control, true);
    }

    private void AddNavButton(Panel container, string title, int index)
    {
        var btn = new Button
        {
            Text = "  " + title,
            Tag = title,
            Size = new Size(196, 38),
            Location = new Point(12, 10 + (index * 44)),
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

        // Update nav buttons styling
        foreach (var b in _navButtons)
        {
            bool isActive = (string)b.Tag == pageTitle;
            b.ForeColor = isActive ? AccentLavender : TextSecondary;
            b.BackColor = isActive ? SurfacePrimary : BgSidebar;
            b.Font = new Font("Segoe UI", 9F, isActive ? FontStyle.Bold : FontStyle.Regular);
        }

        _contentContainer.Controls.Clear();

        switch (pageTitle)
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
            case "Troubleshooting":
                RenderTroubleshootingPage();
                break;
            case "Diagnostics & Logs":
                RenderDiagnosticsPage();
                break;
            case "About & Updates":
                RenderAboutPage();
                break;
        }

        UpdateEngineDisplay();
    }

    // ==========================================
    // 1. OVERVIEW PAGE
    // ==========================================
    private void RenderOverviewPage()
    {
        var pnl = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };

        // Title
        var lblTitle = new Label
        {
            Text = "Overview",
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = TextPrimary,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2)
        };

        var lblSub = new Label
        {
            Text = "Ready to keep audio active. Maintain your Bluetooth speaker connection through quiet moments.",
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = TextMuted,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 20)
        };

        pnl.Controls.Add(lblTitle);
        pnl.Controls.Add(lblSub);

        // --- Main Keep-Alive Card ---
        var cardKeepAlive = new Panel
        {
            Size = new Size(710, 160),
            BackColor = SurfacePrimary,
            Padding = new Padding(24),
            Margin = new Padding(0, 0, 0, 18)
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
            Size = new Size(460, 36)
        };

        _btnOverviewToggle = new Button
        {
            Text = "Start Keep-Alive",
            Size = new Size(170, 44),
            Location = new Point(510, 58),
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

        // --- Two Column Row (Audio Device + Session Summary) ---
        var rowPanel = new Panel
        {
            Size = new Size(710, 160),
            Margin = new Padding(0, 0, 0, 18)
        };

        // Device Summary Card (Left)
        var cardDevice = new Panel
        {
            Size = new Size(346, 160),
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
            Size = new Size(308, 44)
        };

        _lblOverviewDeviceBadge = new Label
        {
            Text = "● Available",
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
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
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
            Size = new Size(346, 160),
            Location = new Point(364, 0),
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
            Text = "Format: 48,000 Hz • 16-bit stereo PCM\nCPU: Low-overhead NAudio callback",
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = TextSecondary,
            Location = new Point(20, 78),
            Size = new Size(308, 34)
        };

        var btnEngineDetails = new Button
        {
            Text = "Engine Details →",
            Location = new Point(20, 116),
            Size = new Size(120, 28),
            FlatStyle = FlatStyle.Flat,
            ForeColor = AccentLavender,
            BackColor = SurfaceSecondary,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
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

        // --- Battery & Guidance Card ---
        var cardGuidance = new Panel
        {
            Size = new Size(710, 88),
            BackColor = SurfaceSecondary,
            Padding = new Padding(20, 16, 20, 16)
        };

        var lblGuidanceTitle = new Label
        {
            Text = "Notice regarding Bluetooth speaker battery",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = ColorWarning,
            Location = new Point(18, 14),
            AutoSize = true
        };

        var lblGuidanceText = new Label
        {
            Text = "Silent audio keeps the Bluetooth radio awake. Stop keep-alive when not in use for optimal battery endurance.",
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = TextSecondary,
            Location = new Point(18, 34),
            Size = new Size(540, 36)
        };

        var btnTroubleshoot = new Button
        {
            Text = "Troubleshooting",
            Location = new Point(570, 24),
            Size = new Size(120, 32),
            FlatStyle = FlatStyle.Flat,
            ForeColor = TextPrimary,
            BackColor = SurfaceHover,
            Font = new Font("Segoe UI", 8.5F),
            Cursor = Cursors.Hand
        };
        btnTroubleshoot.FlatAppearance.BorderSize = 0;
        btnTroubleshoot.Click += (_, _) => NavigateTo("Troubleshooting");

        cardGuidance.Controls.Add(lblGuidanceTitle);
        cardGuidance.Controls.Add(lblGuidanceText);
        cardGuidance.Controls.Add(btnTroubleshoot);
        pnl.Controls.Add(cardGuidance);

        _contentContainer.Controls.Add(pnl);
    }

    // ==========================================
    // 2. AUDIO DEVICE PAGE
    // ==========================================
    private void RenderAudioDevicePage()
    {
        var pnl = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };

        var lblTitle = new Label
        {
            Text = "Audio Playback Device",
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = TextPrimary,
            AutoSize = true
        };

        var lblSub = new Label
        {
            Text = "Select your HOCO HC22 or target Windows audio endpoint for the silent keep-alive stream.",
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = TextMuted,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 16)
        };

        pnl.Controls.Add(lblTitle);
        pnl.Controls.Add(lblSub);

        // Action Toolbar
        var pnlBar = new Panel { Size = new Size(710, 40), Margin = new Padding(0, 0, 0, 14) };
        var btnRefresh = new Button
        {
            Text = "Refresh Device List",
            Size = new Size(140, 32),
            Location = new Point(0, 4),
            FlatStyle = FlatStyle.Flat,
            BackColor = SurfaceSecondary,
            ForeColor = AccentLavender,
            Cursor = Cursors.Hand
        };
        btnRefresh.FlatAppearance.BorderSize = 0;
        btnRefresh.Click += (_, _) =>
        {
            LogService.Log(LogSeverity.Info, "AudioDevice", "Manually refreshed audio devices list.");
            RenderAudioDevicePage();
        };

        var btnSoundSettings = new Button
        {
            Text = "Open Windows Sound Settings",
            Size = new Size(200, 32),
            Location = new Point(150, 4),
            FlatStyle = FlatStyle.Flat,
            BackColor = SurfaceSecondary,
            ForeColor = TextSecondary,
            Cursor = Cursors.Hand
        };
        btnSoundSettings.FlatAppearance.BorderSize = 0;
        btnSoundSettings.Click += (_, _) => OpenWindowsSoundSettings();

        pnlBar.Controls.Add(btnRefresh);
        pnlBar.Controls.Add(btnSoundSettings);
        pnl.Controls.Add(pnlBar);

        // Device List Items
        var devices = AudioDeviceService.GetPlaybackDevices();
        if (devices.Count == 0)
        {
            var pnlEmpty = new Panel { Size = new Size(710, 160), BackColor = SurfacePrimary, Padding = new Padding(24) };
            var lblEmpty = new Label
            {
                Text = "No Windows playback devices detected.\nCheck your Bluetooth connection and click 'Refresh Device List'.",
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
                    Size = new Size(710, 68),
                    BackColor = isSelected ? SurfaceSecondary : SurfacePrimary,
                    Margin = new Padding(0, 0, 0, 10),
                    Padding = new Padding(18, 12, 18, 12)
                };

                var lblDevName = new Label
                {
                    Text = dev.Name,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    ForeColor = TextPrimary,
                    Location = new Point(18, 12),
                    Size = new Size(480, 22)
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
                    Size = new Size(110, 32),
                    Location = new Point(580, 18),
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
                    RenderAudioDevicePage();
                };

                itemCard.Controls.Add(lblDevName);
                itemCard.Controls.Add(lblDevMeta);
                itemCard.Controls.Add(btnSelect);
                pnl.Controls.Add(itemCard);
            }
        }

        _contentContainer.Controls.Clear();
        _contentContainer.Controls.Add(pnl);
    }

    // ==========================================
    // 3. KEEP-ALIVE ENGINE PAGE
    // ==========================================
    private void RenderEnginePage()
    {
        var pnl = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };

        var lblTitle = new Label
        {
            Text = "Keep-Alive Engine",
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = TextPrimary,
            AutoSize = true
        };

        var lblSub = new Label
        {
            Text = "Audio pipeline specifications, latency tuning, and stream telemetry.",
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = TextMuted,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 16)
        };

        pnl.Controls.Add(lblTitle);
        pnl.Controls.Add(lblSub);

        // Telemetry Grid
        var pnlGrid = new Panel { Size = new Size(710, 100), Margin = new Padding(0, 0, 0, 18) };
        AddMetricBox(pnlGrid, 0, "SAMPLE RATE", $"{_settings.SampleRate} Hz");
        AddMetricBox(pnlGrid, 180, "CHANNELS", $"{_settings.Channels} (Stereo)");
        AddMetricBox(pnlGrid, 360, "BUFFER LATENCY", $"{_settings.LatencyMilliseconds} ms");
        AddMetricBox(pnlGrid, 540, "AMPLITUDE", "0.000 (-∞ dB)");
        pnl.Controls.Add(pnlGrid);

        // Advanced Configuration
        var pnlConfig = new Panel
        {
            Size = new Size(710, 240),
            BackColor = SurfacePrimary,
            Padding = new Padding(24),
            Margin = new Padding(0, 0, 0, 18)
        };

        var lblCfgTitle = new Label
        {
            Text = "Advanced Pipeline Tuning",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = TextPrimary,
            Location = new Point(20, 16),
            AutoSize = true
        };

        // Sample rate selector
        var lblSr = new Label { Text = "Audio Sample Rate:", Location = new Point(20, 54), AutoSize = true, ForeColor = TextSecondary };
        var cboSr = new ComboBox { Location = new Point(20, 76), Size = new Size(200, 26), DropDownStyle = ComboBoxStyle.DropDownList };
        cboSr.Items.AddRange(new object[] { "48000 Hz (Standard DVD/BT)", "44100 Hz (CD Audio)" });
        cboSr.SelectedIndex = _settings.SampleRate == 44100 ? 1 : 0;
        cboSr.SelectedIndexChanged += (_, _) =>
        {
            _settings.SampleRate = cboSr.SelectedIndex == 1 ? 44100 : 48000;
            _audioService.SampleRate = _settings.SampleRate;
            _settings.Save();
        };

        // Buffer latency selector
        var lblLat = new Label { Text = "Buffer Latency:", Location = new Point(250, 54), AutoSize = true, ForeColor = TextSecondary };
        var cboLat = new ComboBox { Location = new Point(250, 76), Size = new Size(200, 26), DropDownStyle = ComboBoxStyle.DropDownList };
        cboLat.Items.AddRange(new object[] { "100 ms (Low Latency)", "150 ms (Balanced - Recommended)", "250 ms (High Stability)" });
        cboLat.SelectedIndex = _settings.LatencyMilliseconds switch { 100 => 0, 250 => 2, _ => 1 };
        cboLat.SelectedIndexChanged += (_, _) =>
        {
            _settings.LatencyMilliseconds = cboLat.SelectedIndex switch { 0 => 100, 2 => 250, _ => 150 };
            _audioService.LatencyMs = _settings.LatencyMilliseconds;
            _settings.Save();
        };

        // Reconnect behavior
        var chkReconnect = new CheckBox
        {
            Text = "Automatically attempt stream reconnection when playback endpoint re-appears",
            Location = new Point(20, 130),
            AutoSize = true,
            Checked = _settings.AutoReconnectOnDeviceAvailable,
            ForeColor = TextSecondary
        };
        chkReconnect.CheckedChanged += (_, _) =>
        {
            _settings.AutoReconnectOnDeviceAvailable = chkReconnect.Checked;
            _settings.Save();
        };

        var btnTestChime = new Button
        {
            Text = "Play 0.5s Hardware Verification Chime",
            Size = new Size(260, 34),
            Location = new Point(20, 175),
            FlatStyle = FlatStyle.Flat,
            BackColor = SurfaceSecondary,
            ForeColor = AccentLavender,
            Cursor = Cursors.Hand
        };
        btnTestChime.FlatAppearance.BorderSize = 0;
        btnTestChime.Click += (_, _) => PlayVerificationSound();

        pnlConfig.Controls.Add(lblCfgTitle);
        pnlConfig.Controls.Add(lblSr);
        pnlConfig.Controls.Add(cboSr);
        pnlConfig.Controls.Add(lblLat);
        pnlConfig.Controls.Add(cboLat);
        pnlConfig.Controls.Add(chkReconnect);
        pnlConfig.Controls.Add(btnTestChime);
        pnl.Controls.Add(pnlConfig);

        _contentContainer.Controls.Clear();
        _contentContainer.Controls.Add(pnl);
    }

    private void AddMetricBox(Panel parent, int x, string label, string value)
    {
        var box = new Panel
        {
            Location = new Point(x, 0),
            Size = new Size(160, 90),
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
        var pnl = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };

        var lblTitle = new Label
        {
            Text = "Settings",
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = TextPrimary,
            AutoSize = true
        };

        var lblSub = new Label
        {
            Text = "Configure startup, background tray behavior, and notifications.",
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = TextMuted,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 16)
        };

        pnl.Controls.Add(lblTitle);
        pnl.Controls.Add(lblSub);

        // General Group Card
        var pnlGeneral = new Panel
        {
            Size = new Size(710, 160),
            BackColor = SurfacePrimary,
            Padding = new Padding(24),
            Margin = new Padding(0, 0, 0, 16)
        };

        var lblGenHeader = new Label { Text = "General & Windows Startup", Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = TextPrimary, Location = new Point(20, 14), AutoSize = true };

        var chkStartup = new CheckBox
        {
            Text = "Start HOCO KeepAlive when I sign in to Windows (registers HKCU Run key)",
            Location = new Point(20, 50),
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
            Text = "Start silent keep-alive stream automatically when application launches",
            Location = new Point(20, 82),
            AutoSize = true,
            Checked = _settings.AutoStartStreamOnLaunch,
            ForeColor = TextSecondary
        };
        chkAutoStream.CheckedChanged += (_, _) =>
        {
            _settings.AutoStartStreamOnLaunch = chkAutoStream.Checked;
            _settings.Save();
        };

        var chkCloseToTray = new CheckBox
        {
            Text = "Minimize to notification area (system tray) when closing main window",
            Location = new Point(20, 114),
            AutoSize = true,
            Checked = _settings.MinimizeToTrayOnClose,
            ForeColor = TextSecondary
        };
        chkCloseToTray.CheckedChanged += (_, _) =>
        {
            _settings.MinimizeToTrayOnClose = chkCloseToTray.Checked;
            _settings.Save();
        };

        pnlGeneral.Controls.Add(lblGenHeader);
        pnlGeneral.Controls.Add(chkStartup);
        pnlGeneral.Controls.Add(chkAutoStream);
        pnlGeneral.Controls.Add(chkCloseToTray);
        pnl.Controls.Add(pnlGeneral);

        // Notifications Group Card
        var pnlNotif = new Panel
        {
            Size = new Size(710, 110),
            BackColor = SurfacePrimary,
            Padding = new Padding(24),
            Margin = new Padding(0, 0, 0, 16)
        };

        var lblNotifHeader = new Label { Text = "Notification Area Alerts", Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = TextPrimary, Location = new Point(20, 14), AutoSize = true };

        var chkNotifyEvents = new CheckBox
        {
            Text = "Show balloon notifications on stream start, stop, or device error",
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

        _contentContainer.Controls.Clear();
        _contentContainer.Controls.Add(pnl);
    }

    // ==========================================
    // 5. TROUBLESHOOTING PAGE
    // ==========================================
    private void RenderTroubleshootingPage()
    {
        var pnl = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };

        var lblTitle = new Label { Text = "Troubleshooting Guide", Font = new Font("Segoe UI", 18F, FontStyle.Bold), ForeColor = TextPrimary, AutoSize = true };
        var lblSub = new Label { Text = "Step-by-step solutions for Bluetooth disconnects, audio routing, and speaker sleep timers.", Font = new Font("Segoe UI", 9.5F), ForeColor = TextMuted, AutoSize = true, Margin = new Padding(0, 0, 0, 16) };
        pnl.Controls.Add(lblTitle);
        pnl.Controls.Add(lblSub);

        AddTroubleshootCard(pnl, "1. HOCO HC22 powers off after 10-15 minutes of silence",
            "Hardware DSP Zero-Amplitude Timer",
            "Some speaker firmware variants detect pure digital silence in the audio buffer and enter deep sleep regardless of an active connection. Verify in Windows Sound Settings that 'HOCO HC22 Stereo' is the active output. Periodically test with short audio chimes.");

        AddTroubleshootCard(pnl, "2. Windows turns off the PC Bluetooth adapter",
            "Windows Power Management Setting",
            "Press Win+X > Device Manager > Bluetooth > select your Bluetooth adapter > Properties > Power Management > uncheck 'Allow computer to turn off this device to save power'.");

        AddTroubleshootCard(pnl, "3. Speaker connected but no audio stream starts",
            "Hands-Free vs Stereo Profile Conflict",
            "Windows creates two endpoints: 'Hands-Free AG Audio' (mono, for calls) and 'Stereo' (high-quality A2DP). Disable Hands-Free in Windows Sound Control Panel for dedicated speaker playback.");

        AddTroubleshootCard(pnl, "4. Tray icon hidden or not appearing",
            "Taskbar Overflow Settings",
            "Click the '^' chevron in the Windows taskbar. Drag the cute earbud icon onto the visible taskbar next to the clock.");

        _contentContainer.Controls.Clear();
        _contentContainer.Controls.Add(pnl);
    }

    private void AddTroubleshootCard(FlowLayoutPanel parent, string title, string cause, string solution)
    {
        var card = new Panel
        {
            Size = new Size(710, 100),
            BackColor = SurfacePrimary,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(20, 14, 20, 14)
        };

        var lblHeading = new Label { Text = title, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = AccentLavender, Location = new Point(18, 12), AutoSize = true };
        var lblCause = new Label { Text = $"Likely cause: {cause}", Font = new Font("Segoe UI", 8F, FontStyle.Bold), ForeColor = TextMuted, Location = new Point(18, 34), AutoSize = true };
        var lblSol = new Label { Text = solution, Font = new Font("Segoe UI", 8.5F), ForeColor = TextSecondary, Location = new Point(18, 52), Size = new Size(670, 38) };

        card.Controls.Add(lblHeading);
        card.Controls.Add(lblCause);
        card.Controls.Add(lblSol);
        parent.Controls.Add(card);
    }

    // ==========================================
    // 6. DIAGNOSTICS & LOGS PAGE
    // ==========================================
    private void RenderDiagnosticsPage()
    {
        var pnl = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };

        var lblTitle = new Label { Text = "Diagnostics & Logs", Font = new Font("Segoe UI", 18F, FontStyle.Bold), ForeColor = TextPrimary, AutoSize = true };
        var lblSub = new Label { Text = "Real-time application events, error traces, and diagnostic reporting.", Font = new Font("Segoe UI", 9.5F), ForeColor = TextMuted, AutoSize = true, Margin = new Padding(0, 0, 0, 14) };
        pnl.Controls.Add(lblTitle);
        pnl.Controls.Add(lblSub);

        // Action Toolbar
        var pnlBar = new Panel { Size = new Size(710, 38), Margin = new Padding(0, 0, 0, 12) };
        var btnExport = new Button { Text = "Export Report", Size = new Size(110, 30), Location = new Point(0, 4), FlatStyle = FlatStyle.Flat, BackColor = SurfaceSecondary, ForeColor = AccentLavender, Cursor = Cursors.Hand };
        btnExport.FlatAppearance.BorderSize = 0;
        btnExport.Click += (_, _) =>
        {
            string report = LogService.ExportReport();
            Clipboard.SetText(report);
            MessageBox.Show("Diagnostic report copied to clipboard.", "Diagnostics", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        var btnOpenFolder = new Button { Text = "Open Log Folder", Size = new Size(125, 30), Location = new Point(120, 4), FlatStyle = FlatStyle.Flat, BackColor = SurfaceSecondary, ForeColor = TextSecondary, Cursor = Cursors.Hand };
        btnOpenFolder.FlatAppearance.BorderSize = 0;
        btnOpenFolder.Click += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo { FileName = LogService.LogDirectory, UseShellExecute = true }); } catch { }
        };

        var btnClear = new Button { Text = "Clear Logs", Size = new Size(95, 30), Location = new Point(255, 4), FlatStyle = FlatStyle.Flat, BackColor = SurfaceSecondary, ForeColor = ColorError, Cursor = Cursors.Hand };
        btnClear.FlatAppearance.BorderSize = 0;
        btnClear.Click += (_, _) =>
        {
            LogService.Clear();
            RenderDiagnosticsPage();
        };

        pnlBar.Controls.Add(btnExport);
        pnlBar.Controls.Add(btnOpenFolder);
        pnlBar.Controls.Add(btnClear);
        pnl.Controls.Add(pnlBar);

        // Log viewer text box
        var txtLogs = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Size = new Size(710, 360),
            BackColor = SurfacePrimary,
            ForeColor = TextSecondary,
            Font = new Font("Consolas", 8.5F),
            BorderStyle = BorderStyle.None
        };

        var entries = LogService.GetEntries();
        var lines = entries.Select(e => $"[{e.Timestamp:HH:mm:ss}] [{e.Severity}] [{e.EventType}] {e.Message}").ToArray();
        txtLogs.Lines = lines;
        pnl.Controls.Add(txtLogs);

        _contentContainer.Controls.Clear();
        _contentContainer.Controls.Add(pnl);
    }

    // ==========================================
    // 7. ABOUT & UPDATES PAGE
    // ==========================================
    private void RenderAboutPage()
    {
        var pnl = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };

        var lblTitle = new Label { Text = "About & Updates", Font = new Font("Segoe UI", 18F, FontStyle.Bold), ForeColor = TextPrimary, AutoSize = true };
        var lblSub = new Label { Text = "HOCO KeepAlive build specifications, licensing, and update checks.", Font = new Font("Segoe UI", 9.5F), ForeColor = TextMuted, AutoSize = true, Margin = new Padding(0, 0, 0, 16) };
        pnl.Controls.Add(lblTitle);
        pnl.Controls.Add(lblSub);

        var card = new Panel
        {
            Size = new Size(710, 220),
            BackColor = SurfacePrimary,
            Padding = new Padding(24),
            Margin = new Padding(0, 0, 0, 16)
        };

        var lblAppName = new Label { Text = "HOCO KeepAlive", Font = new Font("Segoe UI", 14F, FontStyle.Bold), ForeColor = TextPrimary, Location = new Point(20, 16), AutoSize = true };
        var lblVer = new Label { Text = "Version 1.0.0 (Release x64) • Self-Contained Single-File", Font = new Font("Segoe UI", 9F), ForeColor = AccentLavender, Location = new Point(22, 44), AutoSize = true };

        var lblSpecs = new Label
        {
            Text = "Runtime: Microsoft .NET 8 (Windows Forms)\n" +
                   "Audio Engine: NAudio 2.2.1 WaveOutEvent / MMDeviceEnumerator\n" +
                   "Branding: Dual Cute Earbuds (Pale Lavender & White)\n" +
                   "License: MIT License • Free & Open Source\n" +
                   "Publisher: Raiyan",
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = TextSecondary,
            Location = new Point(22, 72),
            Size = new Size(600, 80)
        };

        var btnReleases = new Button
        {
            Text = "View GitHub Releases",
            Location = new Point(22, 164),
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

        _contentContainer.Controls.Clear();
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
                _lblTopStatusPill.Text = "RUNNING";
                _lblTopStatusPill.BackColor = Color.FromArgb(20, 83, 45); // Emerald-900
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

            case AudioEngineState.DeviceUnavailable:
                _lblTopStatusPill.Text = "UNAVAILABLE";
                _lblTopStatusPill.BackColor = Color.FromArgb(113, 63, 18);
                _lblTopStatusPill.ForeColor = ColorWarning;

                if (_btnOverviewToggle != null)
                {
                    _btnOverviewToggle.Text = "Retry Connection";
                    _btnOverviewToggle.BackColor = ColorWarning;
                    _btnOverviewToggle.ForeColor = BgMain;
                }
                if (_lblStateTitle != null) _lblStateTitle.Text = "Device unavailable";
                if (_lblStateDesc != null) _lblStateDesc.Text = "The selected audio output device is not currently connected.";
                break;

            case AudioEngineState.Starting:
                _lblTopStatusPill.Text = "STARTING";
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
                if (_lblStateDesc != null) _lblStateDesc.Text = "Start a silent audio stream to help keep your speaker's audio session active.";
                break;
        }

        if (_lblOverviewDeviceName != null)
        {
            _lblOverviewDeviceName.Text = _settings.PreferredDeviceName;
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
            _statsTimer.Stop();
            _statsTimer.Dispose();
            base.OnFormClosing(e);
        }
    }
}
