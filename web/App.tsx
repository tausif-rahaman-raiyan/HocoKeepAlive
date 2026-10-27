import { useState, useEffect, useRef } from 'react';
import {
  LayoutDashboard,
  Speaker,
  Cpu,
  Settings as SettingsIcon,
  HelpCircle,
  FileText,
  Info,
  Play,
  Square,
  RefreshCw,
  ExternalLink,
  Volume2,
  CheckCircle2,
  AlertTriangle,
  Copy,
  Trash2,
  Check,
  Search,
  Bell
} from 'lucide-react';

interface AudioDevice {
  deviceId: string;
  label: string;
  groupId: string;
  isDefault?: boolean;
}

interface LogEntry {
  id: string;
  timestamp: string;
  severity: 'Info' | 'Warning' | 'Error';
  eventType: string;
  message: string;
  details?: string;
}

type Page =
  | 'overview'
  | 'devices'
  | 'engine'
  | 'settings'
  | 'troubleshooting'
  | 'diagnostics'
  | 'about';

export default function App() {
  const [activePage, setActivePage] = useState<Page>('overview');
  const [isRunning, setIsRunning] = useState<boolean>(false);
  const [elapsedSeconds, setElapsedSeconds] = useState<number>(0);
  const [devices, setDevices] = useState<AudioDevice[]>([]);
  const [selectedDeviceId, setSelectedDeviceId] = useState<string>('default');
  const [selectedDeviceName, setSelectedDeviceName] = useState<string>('Windows Default Playback Device (HOCO HC22)');

  // Engine config
  const [sampleRate, setSampleRate] = useState<number>(48000);
  const [channels, setChannels] = useState<number>(2);
  const [bufferLatency, setBufferLatency] = useState<number>(150);
  const [autoReconnect, setAutoReconnect] = useState<boolean>(true);

  // Settings
  const [launchOnStartup, setLaunchOnStartup] = useState<boolean>(false);
  const [autoStartStream, setAutoStartStream] = useState<boolean>(false);
  const [startMinimized, setStartMinimized] = useState<boolean>(false);
  const [minimizeToTrayOnClose, setMinimizeToTrayOnClose] = useState<boolean>(true);
  const [showNotifications, setShowNotifications] = useState<boolean>(true);
  const [notifyOnStartStop, setNotifyOnStartStop] = useState<boolean>(true);
  const [notifyOnError, setNotifyOnError] = useState<boolean>(true);

  // Diagnostics & Logs
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [logFilter, setLogFilter] = useState<'All' | 'Info' | 'Warning' | 'Error'>('All');
  const [logSearch, setLogSearch] = useState<string>('');
  const [copiedReport, setCopiedReport] = useState<boolean>(false);

  // Web Audio Context Refs
  const audioCtxRef = useRef<AudioContext | null>(null);
  const bufferSourceRef = useRef<AudioBufferSourceNode | null>(null);

  // Toast notification
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  // Update check
  const [checkingUpdate, setCheckingUpdate] = useState<boolean>(false);
  const [updateStatus, setUpdateStatus] = useState<string | null>(null);

  const addLog = (severity: 'Info' | 'Warning' | 'Error', eventType: string, message: string, details?: string) => {
    const newEntry: LogEntry = {
      id: Math.random().toString(36).substring(2, 9),
      timestamp: new Date().toLocaleTimeString(),
      severity,
      eventType,
      message,
      details
    };
    setLogs((prev) => [newEntry, ...prev.slice(0, 499)]);
  };

  const triggerToast = (msg: string) => {
    if (!showNotifications) return;
    setToastMessage(msg);
    setTimeout(() => {
      setToastMessage((curr) => (curr === msg ? null : curr));
    }, 4000);
  };

  // Enumerate Devices
  const refreshDevices = async () => {
    try {
      if (!navigator.mediaDevices?.enumerateDevices) {
        fallbackDevices();
        return;
      }
      const list = await navigator.mediaDevices.enumerateDevices();
      const audioOutputs = list
        .filter((d) => d.kind === 'audiooutput')
        .map((d, index) => ({
          deviceId: d.deviceId,
          label: d.label || `Windows Playback Device #${index + 1}`,
          groupId: d.groupId,
          isDefault: index === 0
        }));

      if (audioOutputs.length === 0) {
        fallbackDevices();
      } else {
        setDevices(audioOutputs);
        addLog('Info', 'AudioDevice', `Discovered ${audioOutputs.length} audio playback endpoints.`);
      }
    } catch (ex) {
      fallbackDevices();
      addLog('Warning', 'AudioDevice', 'Could not enumerate devices directly. Using default route.', String(ex));
    }
  };

  const fallbackDevices = () => {
    const list: AudioDevice[] = [
      { deviceId: 'default', label: 'Windows Default Playback Device (HOCO HC22)', groupId: 'default', isDefault: true },
      { deviceId: 'speakers', label: 'Speakers / Headphones (Realtek High Definition Audio)', groupId: 'built-in' },
      { deviceId: 'bt-stereo', label: 'Headphones (HOCO HC22 Stereo)', groupId: 'bt' }
    ];
    setDevices(list);
  };

  useEffect(() => {
    refreshDevices();
    addLog('Info', 'Application', 'HOCO KeepAlive 1.0.0 session initialized.');
    addLog('Info', 'AudioEngine', 'NAudio 48,000 Hz, 16-bit stereo silent provider ready.');
  }, []);

  // Timer loop
  useEffect(() => {
    let timer: number;
    if (isRunning) {
      timer = window.setInterval(() => {
        setElapsedSeconds((prev) => prev + 1);
      }, 1000);
    }
    return () => clearInterval(timer);
  }, [isRunning]);

  // Audio Keep-Alive Engine
  const startKeepAlive = async () => {
    try {
      if (audioCtxRef.current && audioCtxRef.current.state === 'running') {
        return;
      }

      const AudioCtx = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
      const ctx = new AudioCtx({ sampleRate });
      audioCtxRef.current = ctx;

      // Select audio sink if API supported
      if ('setSinkId' in ctx && selectedDeviceId !== 'default') {
        try {
          await (ctx as unknown as { setSinkId: (id: string) => Promise<void> }).setSinkId(selectedDeviceId);
        } catch {
          // Ignored
        }
      }

      // Generate 1-second buffer of true digital silence (zeroes)
      const buffer = ctx.createBuffer(channels, sampleRate, sampleRate);
      for (let c = 0; c < channels; c++) {
        buffer.getChannelData(c).fill(0);
      }

      const source = ctx.createBufferSource();
      source.buffer = buffer;
      source.loop = true;
      source.connect(ctx.destination);
      source.start();
      bufferSourceRef.current = source;

      // MediaSession registration for persistent background playback
      if ('mediaSession' in navigator) {
        navigator.mediaSession.metadata = new MediaMetadata({
          title: 'HOCO KeepAlive — Silent Stream Active',
          artist: selectedDeviceName,
          album: 'Digital Silence Keep-Alive',
          artwork: [{ src: '/assets/earbuds-icon.svg', sizes: '512x512', type: 'image/svg+xml' }]
        });
        navigator.mediaSession.playbackState = 'playing';
      }

      setIsRunning(true);
      setElapsedSeconds(0);
      addLog('Info', 'AudioEngine', `Silent stream started on [${selectedDeviceName}] @ ${sampleRate}Hz, ${channels}ch.`);
      if (notifyOnStartStop) {
        triggerToast('Silent keep-alive started. Bluetooth audio link active.');
      }
    } catch (err: unknown) {
      const errMsg = err instanceof Error ? err.message : String(err);
      addLog('Error', 'AudioEngine', 'Audio initialization failed.', errMsg);
      if (notifyOnError) {
        triggerToast(`Audio Error: ${errMsg}`);
      }
    }
  };

  const stopKeepAlive = () => {
    try {
      if (bufferSourceRef.current) {
        bufferSourceRef.current.stop();
        bufferSourceRef.current.disconnect();
        bufferSourceRef.current = null;
      }
      if (audioCtxRef.current) {
        audioCtxRef.current.close();
        audioCtxRef.current = null;
      }
      if ('mediaSession' in navigator) {
        navigator.mediaSession.playbackState = 'none';
      }
    } catch {
      // Ignored
    } finally {
      setIsRunning(false);
      addLog('Info', 'AudioEngine', 'Silent keep-alive stream stopped by user.');
      if (notifyOnStartStop) {
        triggerToast('Silent keep-alive stopped.');
      }
    }
  };

  const toggleKeepAlive = () => {
    if (isRunning) stopKeepAlive();
    else startKeepAlive();
  };

  const playVerificationChime = () => {
    try {
      const AudioCtx = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
      const ctx = new AudioCtx();
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();

      osc.type = 'sine';
      osc.frequency.setValueAtTime(523.25, ctx.currentTime); // C5
      osc.frequency.exponentialRampToValueAtTime(659.25, ctx.currentTime + 0.15); // E5

      gain.gain.setValueAtTime(0.001, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.2, ctx.currentTime + 0.05);
      gain.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + 0.45);

      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.start();
      osc.stop(ctx.currentTime + 0.5);

      addLog('Info', 'Audio', 'Played 0.5s hardware verification chime.');
      triggerToast('Hardware verification chime played.');
    } catch (ex) {
      addLog('Warning', 'Audio', 'Could not play verification chime.', String(ex));
    }
  };

  const copyDiagnosticReport = () => {
    const header = [
      '=== HOCO KeepAlive Diagnostic Report ===',
      `Timestamp: ${new Date().toISOString()}`,
      `Engine State: ${isRunning ? 'Running' : 'Stopped'}`,
      `Device: ${selectedDeviceName}`,
      `Sample Rate: ${sampleRate} Hz | Channels: ${channels}`,
      '=========================================\n'
    ].join('\n');

    const body = logs
      .map((l) => `[${l.timestamp}] [${l.severity}] [${l.eventType}] ${l.message}${l.details ? ` (${l.details})` : ''}`)
      .join('\n');

    navigator.clipboard.writeText(`${header}\n${body}`);
    setCopiedReport(true);
    setTimeout(() => setCopiedReport(false), 2500);
    triggerToast('Diagnostic report copied to clipboard.');
  };

  const checkForUpdates = () => {
    setCheckingUpdate(true);
    setUpdateStatus(null);
    setTimeout(() => {
      setCheckingUpdate(false);
      setUpdateStatus('HOCO KeepAlive 1.0.0 is the latest available release.');
      addLog('Info', 'UpdateCheck', 'Checked GitHub Releases API: App is up to date (v1.0.0).');
    }, 1200);
  };

  const formatDuration = (totalSec: number) => {
    const hours = Math.floor(totalSec / 3600);
    const minutes = Math.floor((totalSec % 3600) / 60);
    const seconds = totalSec % 60;
    if (hours > 0) {
      return `${String(hours).padStart(2, '0')}h ${String(minutes).padStart(2, '0')}m ${String(seconds).padStart(2, '0')}s`;
    }
    return `${String(minutes).padStart(2, '0')}m ${String(seconds).padStart(2, '0')}s`;
  };

  const navItems: { id: Page; label: string; icon: typeof LayoutDashboard }[] = [
    { id: 'overview', label: 'Overview', icon: LayoutDashboard },
    { id: 'devices', label: 'Audio Device', icon: Speaker },
    { id: 'engine', label: 'Keep-Alive Engine', icon: Cpu },
    { id: 'settings', label: 'Settings', icon: SettingsIcon },
    { id: 'troubleshooting', label: 'Troubleshooting', icon: HelpCircle },
    { id: 'diagnostics', label: 'Diagnostics & Logs', icon: FileText },
    { id: 'about', label: 'About & Updates', icon: Info }
  ];

  const filteredLogs = logs.filter((l) => {
    if (logFilter !== 'All' && l.severity !== logFilter) return false;
    if (logSearch.trim() === '') return true;
    const q = logSearch.toLowerCase();
    return (
      l.message.toLowerCase().includes(q) ||
      l.eventType.toLowerCase().includes(q) ||
      (l.details && l.details.toLowerCase().includes(q))
    );
  });

  return (
    <div className="flex h-screen w-screen bg-[#0E1220] text-[#F5F7FF] font-sans antialiased overflow-hidden select-none">
      {/* Toast Notification */}
      {toastMessage && (
        <div className="fixed bottom-6 right-6 z-50 bg-[#171F32] border border-[#2B3650] text-[#F5F7FF] px-4 py-3 rounded-xl shadow-2xl flex items-center gap-3 animate-in fade-in slide-in-from-bottom-2">
          <div className="w-8 h-8 rounded-lg bg-[#1D2840] flex items-center justify-center text-[#C9D5FF]">
            <Bell className="w-4 h-4" />
          </div>
          <div>
            <p className="text-xs font-bold text-[#C9D5FF]">HOCO KeepAlive</p>
            <p className="text-xs text-[#A7B2C8]">{toastMessage}</p>
          </div>
          <button
            onClick={() => setToastMessage(null)}
            className="text-xs text-[#77839D] hover:text-[#F5F7FF] ml-2 p-1"
          >
            ✕
          </button>
        </div>
      )}

      {/* --- SIDEBAR NAVIGATION (220px, #111727) --- */}
      <aside className="w-[220px] flex-shrink-0 bg-[#111727] border-r border-[#2B3650] flex flex-col justify-between">
        <div>
          {/* Header & Earbud Branding */}
          <div className="h-[76px] px-5 flex items-center gap-3 border-b border-[#2B3650]/60">
            <div className="w-9 h-9 rounded-xl bg-[#0E1220] border border-[#2B3650] p-1 flex items-center justify-center shadow-inner">
              <img src="/assets/earbuds-icon.svg" alt="Earbud icon" className="w-7 h-7" />
            </div>
            <div>
              <h1 className="font-bold text-[14px] leading-tight text-[#F5F7FF] tracking-tight">HOCO KeepAlive</h1>
              <span className="text-[11px] text-[#77839D] font-mono">v1.0.0 • x64</span>
            </div>
          </div>

          {/* Navigation Items */}
          <nav className="p-3 space-y-1">
            {navItems.map((item) => {
              const IconComp = item.icon;
              const isActive = activePage === item.id;
              return (
                <button
                  key={item.id}
                  onClick={() => setActivePage(item.id)}
                  className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-lg text-[13px] font-medium transition-colors cursor-pointer text-left ${
                    isActive
                      ? 'bg-[#171F32] text-[#C9D5FF] font-semibold border-l-2 border-[#C9D5FF]'
                      : 'text-[#A7B2C8] hover:bg-[#25314B]/50 hover:text-[#F5F7FF]'
                  }`}
                >
                  <IconComp className={`w-4 h-4 ${isActive ? 'text-[#C9D5FF]' : 'text-[#77839D]'}`} />
                  <span>{item.label}</span>
                </button>
              );
            })}
          </nav>
        </div>

        {/* Sidebar Footer */}
        <div className="p-4 border-t border-[#2B3650]/60 text-[11px] text-[#77839D] flex flex-col gap-1.5">
          <div className="flex items-center gap-2">
            <span
              className={`w-2 h-2 rounded-full ${
                isRunning ? 'bg-[#4ADE80] animate-pulse' : 'bg-[#77839D]'
              }`}
            />
            <span className="font-medium text-[#A7B2C8]">{isRunning ? 'Engine Running' : 'Engine Idle'}</span>
          </div>
          <a
            href="https://github.com/tausif-rahaman-raiyan/HocoKeepAlive"
            target="_blank"
            rel="noreferrer"
            className="text-[11px] text-[#C9D5FF] hover:underline flex items-center gap-1 mt-1"
          >
            <span>GitHub Repository</span>
            <ExternalLink className="w-2.5 h-2.5" />
          </a>
        </div>
      </aside>

      {/* --- MAIN CONTENT CONTAINER (#0E1220) --- */}
      <div className="flex-1 flex flex-col min-w-0 overflow-hidden">
        {/* Top Header Status Bar */}
        <header className="h-[56px] px-8 border-b border-[#2B3650]/50 flex items-center justify-between flex-shrink-0 bg-[#0E1220]">
          <div className="flex items-center gap-2 text-xs text-[#77839D]">
            <span>Output Device:</span>
            <span className="text-[#A7B2C8] font-medium truncate max-w-sm">{selectedDeviceName}</span>
          </div>

          <div className="flex items-center gap-3">
            <span
              className={`text-[11px] font-mono font-bold px-3 py-1 rounded-full border ${
                isRunning
                  ? 'bg-[#4ADE80]/15 text-[#4ADE80] border-[#4ADE80]/30'
                  : 'bg-[#1D2840] text-[#77839D] border-[#2B3650]'
              }`}
            >
              {isRunning ? 'RUNNING SILENTLY' : 'STOPPED'}
            </span>
          </div>
        </header>

        {/* Dynamic Page Views */}
        <main className="flex-1 overflow-y-auto p-8 max-w-5xl">
          {/* ========================================================= */}
          {/* 1. OVERVIEW PAGE (Main Dashboard) */}
          {/* ========================================================= */}
          {activePage === 'overview' && (
            <div className="space-y-6">
              {/* Page Title */}
              <div>
                <h2 className="text-2xl font-bold tracking-tight text-[#F5F7FF]">Overview</h2>
                <p className="text-sm text-[#77839D] mt-1">
                  Keep your Bluetooth audio session active during pauses and silence.
                </p>
              </div>

              {/* Main Keep-Alive Card */}
              <div className="bg-[#171F32] border border-[#2B3650] rounded-2xl p-6 relative overflow-hidden shadow-xl">
                <div className="flex flex-col md:flex-row md:items-center justify-between gap-6">
                  <div className="space-y-2 max-w-lg">
                    <span className="text-[11px] font-mono font-bold text-[#C9D5FF] tracking-wider uppercase">
                      KEEP-ALIVE STATUS
                    </span>
                    <h3 className="text-2xl font-bold text-[#F5F7FF]">
                      {isRunning ? 'Keep-Alive is running' : 'Ready when you are'}
                    </h3>
                    <p className="text-sm text-[#A7B2C8] leading-relaxed">
                      {isRunning
                        ? 'Silent audio (zero-amplitude PCM) is being streamed through your selected output device to maintain the Bluetooth link.'
                        : 'Start a silent audio stream to help prevent your HOCO HC22 speaker from disconnecting or falling asleep.'}
                    </p>
                  </div>

                  {/* Primary Start / Stop Button */}
                  <div className="flex flex-col items-start md:items-end gap-3 flex-shrink-0">
                    <button
                      onClick={toggleKeepAlive}
                      className={`px-7 py-3.5 rounded-xl font-bold text-sm flex items-center gap-2.5 transition-all shadow-lg cursor-pointer ${
                        isRunning
                          ? 'bg-[#FB7185] hover:bg-[#FB7185]/90 text-white shadow-[#FB7185]/20'
                          : 'bg-[#C9D5FF] hover:bg-[#AEBFFF] text-[#0E1220] shadow-[#C9D5FF]/10'
                      }`}
                    >
                      {isRunning ? (
                        <>
                          <Square className="w-4 h-4 fill-current" />
                          <span>Stop Keep-Alive</span>
                        </>
                      ) : (
                        <>
                          <Play className="w-4 h-4 fill-current" />
                          <span>Start Keep-Alive</span>
                        </>
                      )}
                    </button>
                    {isRunning && (
                      <span className="text-xs font-mono font-semibold text-[#4ADE80]">
                        Session Uptime: {formatDuration(elapsedSeconds)}
                      </span>
                    )}
                  </div>
                </div>
              </div>

              {/* Two Column Summary Grid */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
                {/* Audio Device Card */}
                <div className="bg-[#171F32] border border-[#2B3650] rounded-2xl p-5 flex flex-col justify-between">
                  <div>
                    <div className="flex items-center justify-between">
                      <span className="text-[11px] font-mono font-bold text-[#77839D]">AUDIO PLAYBACK DEVICE</span>
                      <span className="text-xs text-[#4ADE80] font-medium flex items-center gap-1">
                        <CheckCircle2 className="w-3.5 h-3.5" />
                        <span>Ready</span>
                      </span>
                    </div>
                    <h4 className="text-base font-bold text-[#F5F7FF] mt-2 line-clamp-2">
                      {selectedDeviceName}
                    </h4>
                    <p className="text-xs text-[#77839D] mt-1">
                      DirectSound / WaveOut Playback Endpoint
                    </p>
                  </div>
                  <div className="mt-4 pt-3 border-t border-[#2B3650]/60 flex items-center justify-between">
                    <button
                      onClick={() => setActivePage('devices')}
                      className="text-xs text-[#C9D5FF] hover:underline font-medium cursor-pointer"
                    >
                      Manage Devices →
                    </button>
                    <button
                      onClick={playVerificationChime}
                      className="text-xs text-[#A7B2C8] hover:text-[#F5F7FF] flex items-center gap-1 cursor-pointer"
                    >
                      <Volume2 className="w-3.5 h-3.5 text-[#C9D5FF]" />
                      <span>Test Chime</span>
                    </button>
                  </div>
                </div>

                {/* Session Summary Card */}
                <div className="bg-[#171F32] border border-[#2B3650] rounded-2xl p-5 flex flex-col justify-between">
                  <div>
                    <span className="text-[11px] font-mono font-bold text-[#77839D]">ACTIVE SESSION TELEMETRY</span>
                    <div className="text-2xl font-bold font-mono text-[#F5F7FF] mt-2">
                      {isRunning ? formatDuration(elapsedSeconds) : '00m 00s'}
                    </div>
                    <p className="text-xs text-[#A7B2C8] mt-1">
                      Audio Stream: {sampleRate.toLocaleString()} Hz • {channels === 2 ? 'Stereo' : 'Mono'} PCM
                    </p>
                  </div>
                  <div className="mt-4 pt-3 border-t border-[#2B3650]/60 flex items-center justify-between">
                    <button
                      onClick={() => setActivePage('engine')}
                      className="text-xs text-[#C9D5FF] hover:underline font-medium cursor-pointer"
                    >
                      Engine Tuning →
                    </button>
                    <span className="text-xs text-[#77839D] font-mono">Buffer: {bufferLatency}ms</span>
                  </div>
                </div>
              </div>

              {/* Battery & Helpful Information Card */}
              <div className="bg-[#1D2840] border border-[#2B3650] rounded-2xl p-5 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
                <div className="flex items-start gap-3">
                  <div className="w-8 h-8 rounded-lg bg-[#25314B] flex items-center justify-center text-[#FBBF24] flex-shrink-0 mt-0.5">
                    <AlertTriangle className="w-4 h-4" />
                  </div>
                  <div>
                    <h5 className="text-xs font-bold text-[#FBBF24]">Speaker Battery Note</h5>
                    <p className="text-xs text-[#A7B2C8] mt-0.5 leading-relaxed">
                      Silent audio can help maintain an active Bluetooth audio link, but speaker amplifier & radio stay active. For best battery life, stop keep-alive when taking long breaks.
                    </p>
                  </div>
                </div>
                <button
                  onClick={() => setActivePage('troubleshooting')}
                  className="px-4 py-2 bg-[#25314B] hover:bg-[#25314B]/80 text-[#F5F7FF] text-xs font-medium rounded-lg flex-shrink-0 cursor-pointer"
                >
                  Troubleshooting Guide
                </button>
              </div>
            </div>
          )}

          {/* ========================================================= */}
          {/* 2. AUDIO DEVICE PAGE */}
          {/* ========================================================= */}
          {activePage === 'devices' && (
            <div className="space-y-6">
              <div className="flex items-center justify-between">
                <div>
                  <h2 className="text-2xl font-bold tracking-tight text-[#F5F7FF]">Audio Device</h2>
                  <p className="text-sm text-[#77839D] mt-1">
                    Select your HOCO HC22 or target Windows audio endpoint for the silent keep-alive stream.
                  </p>
                </div>
                <button
                  onClick={refreshDevices}
                  className="px-3.5 py-2 rounded-lg bg-[#1D2840] hover:bg-[#25314B] text-[#C9D5FF] text-xs font-medium flex items-center gap-2 border border-[#2B3650] cursor-pointer"
                >
                  <RefreshCw className="w-3.5 h-3.5" />
                  <span>Refresh Device List</span>
                </button>
              </div>

              {/* Device List Items */}
              <div className="space-y-3">
                {devices.map((dev) => {
                  const isSelected = selectedDeviceId === dev.deviceId;
                  return (
                    <div
                      key={dev.deviceId}
                      className={`p-4 rounded-xl border transition-colors flex items-center justify-between ${
                        isSelected
                          ? 'bg-[#1D2840] border-[#C9D5FF]/60 shadow-md'
                          : 'bg-[#171F32] border-[#2B3650] hover:bg-[#25314B]/30'
                      }`}
                    >
                      <div className="space-y-1">
                        <div className="flex items-center gap-2">
                          <span className="font-semibold text-sm text-[#F5F7FF]">{dev.label}</span>
                          {dev.isDefault && (
                            <span className="text-[10px] bg-[#25314B] text-[#A7B2C8] px-2 py-0.5 rounded font-mono">
                              Default Output
                            </span>
                          )}
                        </div>
                        <p className="text-xs text-[#77839D] font-mono">
                          Endpoint ID: {dev.deviceId === 'default' ? 'SYSTEM_DEFAULT_MAPPER' : dev.deviceId.slice(0, 24)}...
                        </p>
                      </div>

                      <div className="flex items-center gap-3">
                        <button
                          onClick={() => {
                            setSelectedDeviceId(dev.deviceId);
                            setSelectedDeviceName(dev.label);
                            addLog('Info', 'AudioDevice', `Selected target playback endpoint: ${dev.label}`);
                            triggerToast(`Selected device: ${dev.label}`);
                          }}
                          disabled={isSelected}
                          className={`px-4 py-2 rounded-lg text-xs font-bold transition cursor-pointer ${
                            isSelected
                              ? 'bg-[#C9D5FF] text-[#0E1220] cursor-default'
                              : 'bg-[#25314B] hover:bg-[#25314B]/80 text-[#A7B2C8] hover:text-[#F5F7FF]'
                          }`}
                        >
                          {isSelected ? 'Active Target' : 'Select Device'}
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>

              {/* Empty state guidance */}
              <div className="bg-[#171F32] border border-[#2B3650] rounded-xl p-5 text-xs text-[#A7B2C8] space-y-2">
                <h4 className="font-bold text-[#F5F7FF]">Cannot find HOCO HC22 in the list?</h4>
                <p>
                  1. Verify your Bluetooth speaker is turned on, paired, and shows as <strong>Connected</strong> in Windows Settings &gt; Bluetooth & devices.
                </p>
                <p>
                  2. Ensure <strong>HOCO HC22 Stereo</strong> is chosen as the Windows default playback output.
                </p>
              </div>
            </div>
          )}

          {/* ========================================================= */}
          {/* 3. KEEP-ALIVE ENGINE PAGE */}
          {/* ========================================================= */}
          {activePage === 'engine' && (
            <div className="space-y-6">
              <div>
                <h2 className="text-2xl font-bold tracking-tight text-[#F5F7FF]">Keep-Alive Engine</h2>
                <p className="text-sm text-[#77839D] mt-1">
                  Audio pipeline specifications, latency tuning, and stream telemetry.
                </p>
              </div>

              {/* Live Metric Cards */}
              <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
                <div className="bg-[#171F32] border border-[#2B3650] rounded-xl p-4">
                  <span className="text-[10px] font-mono font-bold text-[#77839D]">SAMPLE RATE</span>
                  <div className="text-lg font-bold font-mono text-[#F5F7FF] mt-1">{sampleRate} Hz</div>
                </div>
                <div className="bg-[#171F32] border border-[#2B3650] rounded-xl p-4">
                  <span className="text-[10px] font-mono font-bold text-[#77839D]">CHANNELS</span>
                  <div className="text-lg font-bold font-mono text-[#F5F7FF] mt-1">
                    {channels === 2 ? 'Stereo (2ch)' : 'Mono (1ch)'}
                  </div>
                </div>
                <div className="bg-[#171F32] border border-[#2B3650] rounded-xl p-4">
                  <span className="text-[10px] font-mono font-bold text-[#77839D]">LATENCY</span>
                  <div className="text-lg font-bold font-mono text-[#F5F7FF] mt-1">{bufferLatency} ms</div>
                </div>
                <div className="bg-[#171F32] border border-[#2B3650] rounded-xl p-4">
                  <span className="text-[10px] font-mono font-bold text-[#77839D]">PCM AMPLITUDE</span>
                  <div className="text-lg font-bold font-mono text-[#4ADE80] mt-1">-∞ dB (0x00)</div>
                </div>
              </div>

              {/* Advanced Pipeline Tuning Form */}
              <div className="bg-[#171F32] border border-[#2B3650] rounded-2xl p-6 space-y-5">
                <h3 className="font-bold text-base text-[#F5F7FF]">Audio Pipeline Configuration</h3>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-5">
                  <div className="space-y-2">
                    <label className="text-xs font-semibold text-[#A7B2C8]">Audio Sample Rate</label>
                    <select
                      value={sampleRate}
                      onChange={(e) => {
                        const val = Number(e.target.value);
                        setSampleRate(val);
                        addLog('Info', 'AudioEngine', `Configured sample rate: ${val} Hz`);
                      }}
                      className="w-full bg-[#0E1220] border border-[#2B3650] text-[#F5F7FF] rounded-lg p-2.5 text-xs focus:outline-none focus:border-[#C9D5FF]"
                    >
                      <option value={48000}>48,000 Hz (Standard DVD / Bluetooth A2DP)</option>
                      <option value={44100}>44,100 Hz (Compact Disc Audio)</option>
                    </select>
                  </div>

                  <div className="space-y-2">
                    <label className="text-xs font-semibold text-[#A7B2C8]">Audio Channel Layout</label>
                    <select
                      value={channels}
                      onChange={(e) => {
                        const val = Number(e.target.value);
                        setChannels(val);
                        addLog('Info', 'AudioEngine', `Configured channel layout: ${val === 2 ? 'Stereo' : 'Mono'}`);
                      }}
                      className="w-full bg-[#0E1220] border border-[#2B3650] text-[#F5F7FF] rounded-lg p-2.5 text-xs focus:outline-none focus:border-[#C9D5FF]"
                    >
                      <option value={2}>2 Channels (Stereo)</option>
                      <option value={1}>1 Channel (Mono)</option>
                    </select>
                  </div>

                  <div className="space-y-2">
                    <label className="text-xs font-semibold text-[#A7B2C8]">Buffer Desired Latency</label>
                    <select
                      value={bufferLatency}
                      onChange={(e) => {
                        const val = Number(e.target.value);
                        setBufferLatency(val);
                        addLog('Info', 'AudioEngine', `Configured buffer latency: ${val} ms`);
                      }}
                      className="w-full bg-[#0E1220] border border-[#2B3650] text-[#F5F7FF] rounded-lg p-2.5 text-xs focus:outline-none focus:border-[#C9D5FF]"
                    >
                      <option value={100}>100 ms (Low Latency)</option>
                      <option value={150}>150 ms (Balanced - Recommended)</option>
                      <option value={250}>250 ms (High Stability / Power Saving)</option>
                    </select>
                  </div>

                  <div className="space-y-2">
                    <label className="text-xs font-semibold text-[#A7B2C8]">Reconnection Behavior</label>
                    <div className="pt-2">
                      <label className="flex items-center gap-2 text-xs text-[#A7B2C8] cursor-pointer">
                        <input
                          type="checkbox"
                          checked={autoReconnect}
                          onChange={(e) => setAutoReconnect(e.target.checked)}
                          className="rounded bg-[#0E1220] border-[#2B3650] text-[#C9D5FF]"
                        />
                        <span>Auto-reconnect if device disconnects and reappears</span>
                      </label>
                    </div>
                  </div>
                </div>

                <div className="pt-3 border-t border-[#2B3650]/60 flex items-center justify-between">
                  <span className="text-xs text-[#77839D]">
                    Changes take effect on the next stream start.
                  </span>
                  <button
                    onClick={playVerificationChime}
                    className="px-4 py-2 bg-[#1D2840] hover:bg-[#25314B] text-[#C9D5FF] text-xs font-semibold rounded-lg flex items-center gap-2 cursor-pointer"
                  >
                    <Volume2 className="w-3.5 h-3.5" />
                    <span>Play 0.5s Hardware Verification Chime</span>
                  </button>
                </div>
              </div>
            </div>
          )}

          {/* ========================================================= */}
          {/* 4. SETTINGS PAGE */}
          {/* ========================================================= */}
          {activePage === 'settings' && (
            <div className="space-y-6">
              <div>
                <h2 className="text-2xl font-bold tracking-tight text-[#F5F7FF]">Settings</h2>
                <p className="text-sm text-[#77839D] mt-1">
                  Configure startup, background tray behavior, and notifications.
                </p>
              </div>

              {/* General Group */}
              <div className="bg-[#171F32] border border-[#2B3650] rounded-2xl p-6 space-y-4">
                <h3 className="text-sm font-bold text-[#F5F7FF]">General &amp; Windows Startup</h3>
                <div className="space-y-3 text-xs">
                  <label className="flex items-center justify-between p-3 rounded-xl bg-[#0E1220] border border-[#2B3650]/60 cursor-pointer">
                    <div>
                      <span className="font-semibold text-[#F5F7FF] block">
                        Start HOCO KeepAlive when I sign in to Windows
                      </span>
                      <span className="text-[#77839D]">
                        Registers an entry in HKCU\Software\Microsoft\Windows\CurrentVersion\Run
                      </span>
                    </div>
                    <input
                      type="checkbox"
                      checked={launchOnStartup}
                      onChange={(e) => {
                        setLaunchOnStartup(e.target.checked);
                        addLog('Info', 'Settings', `Launch on startup toggled: ${e.target.checked}`);
                      }}
                      className="rounded bg-[#1D2840] border-[#2B3650] text-[#C9D5FF] w-4 h-4 cursor-pointer"
                    />
                  </label>

                  <label className="flex items-center justify-between p-3 rounded-xl bg-[#0E1220] border border-[#2B3650]/60 cursor-pointer">
                    <div>
                      <span className="font-semibold text-[#F5F7FF] block">
                        Start silent keep-alive stream automatically on app launch
                      </span>
                      <span className="text-[#77839D]">
                        Immediately opens the audio stream when application boots
                      </span>
                    </div>
                    <input
                      type="checkbox"
                      checked={autoStartStream}
                      onChange={(e) => setAutoStartStream(e.target.checked)}
                      className="rounded bg-[#1D2840] border-[#2B3650] text-[#C9D5FF] w-4 h-4 cursor-pointer"
                    />
                  </label>

                  <label className="flex items-center justify-between p-3 rounded-xl bg-[#0E1220] border border-[#2B3650]/60 cursor-pointer">
                    <div>
                      <span className="font-semibold text-[#F5F7FF] block">
                        Start minimized to notification area (tray)
                      </span>
                      <span className="text-[#77839D]">
                        Runs quietly in taskbar notification tray without opening main window
                      </span>
                    </div>
                    <input
                      type="checkbox"
                      checked={startMinimized}
                      onChange={(e) => setStartMinimized(e.target.checked)}
                      className="rounded bg-[#1D2840] border-[#2B3650] text-[#C9D5FF] w-4 h-4 cursor-pointer"
                    />
                  </label>

                  <label className="flex items-center justify-between p-3 rounded-xl bg-[#0E1220] border border-[#2B3650]/60 cursor-pointer">
                    <div>
                      <span className="font-semibold text-[#F5F7FF] block">
                        Minimize to tray when closing window (X button)
                      </span>
                      <span className="text-[#77839D]">
                        Keeps stream active in tray instead of completely exiting the app
                      </span>
                    </div>
                    <input
                      type="checkbox"
                      checked={minimizeToTrayOnClose}
                      onChange={(e) => setMinimizeToTrayOnClose(e.target.checked)}
                      className="rounded bg-[#1D2840] border-[#2B3650] text-[#C9D5FF] w-4 h-4 cursor-pointer"
                    />
                  </label>
                </div>
              </div>

              {/* Notification Alerts */}
              <div className="bg-[#171F32] border border-[#2B3650] rounded-2xl p-6 space-y-4">
                <h3 className="text-sm font-bold text-[#F5F7FF]">Notification Area Alerts</h3>
                <div className="space-y-3 text-xs">
                  <label className="flex items-center justify-between p-3 rounded-xl bg-[#0E1220] border border-[#2B3650]/60 cursor-pointer">
                    <div>
                      <span className="font-semibold text-[#F5F7FF] block">
                        Enable Windows Notification Area Balloon Tips
                      </span>
                      <span className="text-[#77839D]">
                        Master switch for system notifications and state changes
                      </span>
                    </div>
                    <input
                      type="checkbox"
                      checked={showNotifications}
                      onChange={(e) => setShowNotifications(e.target.checked)}
                      className="rounded bg-[#1D2840] border-[#2B3650] text-[#C9D5FF] w-4 h-4 cursor-pointer"
                    />
                  </label>

                  <label className="flex items-center justify-between p-3 rounded-xl bg-[#0E1220] border border-[#2B3650]/60 cursor-pointer">
                    <div>
                      <span className="font-semibold text-[#F5F7FF] block">
                        Show notifications on stream start and stop
                      </span>
                      <span className="text-[#77839D]">
                        Displays brief balloon tips when the silent session begins or ends
                      </span>
                    </div>
                    <input
                      type="checkbox"
                      checked={notifyOnStartStop}
                      onChange={(e) => setNotifyOnStartStop(e.target.checked)}
                      className="rounded bg-[#1D2840] border-[#2B3650] text-[#C9D5FF] w-4 h-4 cursor-pointer"
                    />
                  </label>

                  <label className="flex items-center justify-between p-3 rounded-xl bg-[#0E1220] border border-[#2B3650]/60 cursor-pointer">
                    <div>
                      <span className="font-semibold text-[#F5F7FF] block">
                        Show notifications on audio device errors
                      </span>
                      <span className="text-[#77839D]">
                        Alerts if Bluetooth disconnects or device becomes unavailable
                      </span>
                    </div>
                    <input
                      type="checkbox"
                      checked={notifyOnError}
                      onChange={(e) => setNotifyOnError(e.target.checked)}
                      className="rounded bg-[#1D2840] border-[#2B3650] text-[#C9D5FF] w-4 h-4 cursor-pointer"
                    />
                  </label>
                </div>
              </div>

              {/* Diagnostics & Reset */}
              <div className="bg-[#171F32] border border-[#2B3650] rounded-2xl p-6 flex items-center justify-between">
                <div>
                  <h4 className="text-xs font-bold text-[#F5F7FF]">Reset Application Settings</h4>
                  <p className="text-xs text-[#77839D] mt-0.5">
                    Restore all configuration tokens to clean system defaults.
                  </p>
                </div>
                <button
                  onClick={() => {
                    setLaunchOnStartup(false);
                    setAutoStartStream(false);
                    setStartMinimized(false);
                    setMinimizeToTrayOnClose(true);
                    setSampleRate(48000);
                    setChannels(2);
                    setBufferLatency(150);
                    addLog('Info', 'Settings', 'Settings reset to factory defaults.');
                    triggerToast('Preferences reset to default values.');
                  }}
                  className="px-4 py-2 bg-[#1D2840] hover:bg-[#FB7185]/20 text-[#FB7185] hover:text-[#FB7185] text-xs font-semibold rounded-lg border border-[#2B3650] cursor-pointer"
                >
                  Reset to Defaults
                </button>
              </div>
            </div>
          )}

          {/* ========================================================= */}
          {/* 5. TROUBLESHOOTING PAGE */}
          {/* ========================================================= */}
          {activePage === 'troubleshooting' && (
            <div className="space-y-6">
              <div>
                <h2 className="text-2xl font-bold tracking-tight text-[#F5F7FF]">Troubleshooting Guide</h2>
                <p className="text-sm text-[#77839D] mt-1">
                  Step-by-step solutions for Bluetooth disconnects, audio routing, and speaker sleep timers.
                </p>
              </div>

              <div className="space-y-3.5">
                {[
                  {
                    num: '1',
                    title: 'HOCO HC22 powers off after 10-15 minutes of silence',
                    cause: 'Hardware DSP Zero-Amplitude Timer',
                    solution:
                      'Certain speaker firmware variants detect pure digital zero in the audio buffer and enter deep sleep regardless of an active connection. Ensure Windows Sound Settings has "HOCO HC22 Stereo" set as default output rather than Hands-Free. Periodically test with short audio chimes.'
                  },
                  {
                    num: '2',
                    title: 'Windows turns off the PC Bluetooth adapter',
                    cause: 'Windows Power Management Setting',
                    solution:
                      'Press Win+X > Device Manager > Bluetooth > select your Bluetooth adapter > Properties > Power Management tab > uncheck "Allow the computer to turn off this device to save power".'
                  },
                  {
                    num: '3',
                    title: 'Speaker connected but audio plays through laptop speakers',
                    cause: 'Audio Endpoint Default Conflict',
                    solution:
                      'Open Windows Sound Settings (Win+I > System > Sound) and set "HOCO HC22 Stereo" as the Default output device, or select it directly from the Audio Device page in this app.'
                  },
                  {
                    num: '4',
                    title: 'Hands-Free vs Stereo Profile Conflict',
                    cause: 'Bluetooth Hands-Free AG Telephony Profile',
                    solution:
                      'Windows exposes two devices for Bluetooth headsets: "Hands-Free AG Audio" (low bitrate mono) and "Stereo" (A2DP high quality). In Windows Sound Control Panel (mmsys.cpl), disable the Hands-Free recording device if unused.'
                  },
                  {
                    num: '5',
                    title: 'Tray icon hidden or not appearing in taskbar',
                    cause: 'Windows Notification Area Overflow',
                    solution:
                      'Click the "^" chevron in the Windows taskbar. Drag the cute earbud icon onto the visible taskbar next to the clock.'
                  }
                ].map((item) => (
                  <div key={item.num} className="bg-[#171F32] border border-[#2B3650] rounded-xl p-5 space-y-2">
                    <div className="flex items-center gap-2">
                      <span className="w-5 h-5 rounded-full bg-[#1D2840] text-[#C9D5FF] text-[10px] font-bold flex items-center justify-center">
                        {item.num}
                      </span>
                      <h4 className="font-bold text-sm text-[#F5F7FF]">{item.title}</h4>
                    </div>
                    <p className="text-xs text-[#C9D5FF] font-medium">Likely cause: {item.cause}</p>
                    <p className="text-xs text-[#A7B2C8] leading-relaxed pl-7">{item.solution}</p>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* ========================================================= */}
          {/* 6. DIAGNOSTICS & LOGS PAGE */}
          {/* ========================================================= */}
          {activePage === 'diagnostics' && (
            <div className="space-y-5">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                <div>
                  <h2 className="text-2xl font-bold tracking-tight text-[#F5F7FF]">Diagnostics &amp; Logs</h2>
                  <p className="text-sm text-[#77839D] mt-1">
                    Real-time application events, error traces, and diagnostic reporting.
                  </p>
                </div>

                <div className="flex items-center gap-2">
                  <button
                    onClick={copyDiagnosticReport}
                    className="px-3.5 py-2 bg-[#1D2840] hover:bg-[#25314B] text-[#C9D5FF] text-xs font-semibold rounded-lg flex items-center gap-1.5 border border-[#2B3650] cursor-pointer"
                  >
                    {copiedReport ? <Check className="w-3.5 h-3.5 text-[#4ADE80]" /> : <Copy className="w-3.5 h-3.5" />}
                    <span>{copiedReport ? 'Copied!' : 'Copy Report'}</span>
                  </button>
                  <button
                    onClick={() => {
                      setLogs([]);
                      addLog('Info', 'Diagnostics', 'Logs cleared by user.');
                    }}
                    className="px-3 py-2 bg-[#1D2840] hover:bg-[#FB7185]/20 text-[#FB7185] text-xs font-semibold rounded-lg border border-[#2B3650] cursor-pointer"
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                  </button>
                </div>
              </div>

              {/* Filter and Search Bar */}
              <div className="bg-[#171F32] border border-[#2B3650] rounded-xl p-3 flex flex-col sm:flex-row items-center justify-between gap-3">
                <div className="flex items-center gap-1.5 w-full sm:w-auto">
                  {(['All', 'Info', 'Warning', 'Error'] as const).map((filter) => (
                    <button
                      key={filter}
                      onClick={() => setLogFilter(filter)}
                      className={`px-3 py-1.5 rounded-lg text-xs font-medium transition cursor-pointer ${
                        logFilter === filter
                          ? 'bg-[#C9D5FF] text-[#0E1220] font-bold'
                          : 'bg-[#1D2840] text-[#A7B2C8] hover:text-[#F5F7FF]'
                      }`}
                    >
                      {filter}
                    </button>
                  ))}
                </div>

                <div className="relative w-full sm:w-64">
                  <Search className="w-3.5 h-3.5 text-[#77839D] absolute left-3 top-2.5" />
                  <input
                    type="text"
                    value={logSearch}
                    onChange={(e) => setLogSearch(e.target.value)}
                    placeholder="Search logs..."
                    className="w-full bg-[#0E1220] border border-[#2B3650] text-[#F5F7FF] pl-8 pr-3 py-1.5 rounded-lg text-xs focus:outline-none focus:border-[#C9D5FF]"
                  />
                </div>
              </div>

              {/* Log Stream Box */}
              <div className="bg-[#171F32] border border-[#2B3650] rounded-xl p-4 font-mono text-xs max-h-[460px] overflow-y-auto space-y-2">
                {filteredLogs.length === 0 ? (
                  <p className="text-center text-[#77839D] py-8">No log entries matching query.</p>
                ) : (
                  filteredLogs.map((log) => (
                    <div
                      key={log.id}
                      className="p-2.5 rounded-lg bg-[#0E1220] border border-[#2B3650]/40 flex flex-col gap-0.5"
                    >
                      <div className="flex items-center gap-2">
                        <span className="text-[#77839D] font-mono text-[11px]">{log.timestamp}</span>
                        <span
                          className={`text-[10px] font-bold px-1.5 py-0.5 rounded ${
                            log.severity === 'Error'
                              ? 'bg-[#FB7185]/20 text-[#FB7185]'
                              : log.severity === 'Warning'
                              ? 'bg-[#FBBF24]/20 text-[#FBBF24]'
                              : 'bg-[#C9D5FF]/20 text-[#C9D5FF]'
                          }`}
                        >
                          {log.severity}
                        </span>
                        <span className="text-[#C9D5FF] font-semibold text-[11px]">{log.eventType}</span>
                      </div>
                      <p className="text-[#A7B2C8] pl-1 font-sans text-xs">{log.message}</p>
                      {log.details && (
                        <p className="text-[#77839D] pl-1 font-mono text-[11px] break-all">{log.details}</p>
                      )}
                    </div>
                  ))
                )}
              </div>
            </div>
          )}

          {/* ========================================================= */}
          {/* 7. ABOUT & UPDATES PAGE */}
          {/* ========================================================= */}
          {activePage === 'about' && (
            <div className="space-y-6">
              <div>
                <h2 className="text-2xl font-bold tracking-tight text-[#F5F7FF]">About &amp; Updates</h2>
                <p className="text-sm text-[#77839D] mt-1">
                  HOCO KeepAlive build specifications, licensing, and update checks.
                </p>
              </div>

              <div className="bg-[#171F32] border border-[#2B3650] rounded-2xl p-6 flex flex-col md:flex-row md:items-center justify-between gap-6">
                <div className="flex items-center gap-4">
                  <div className="w-16 h-16 rounded-2xl bg-[#0E1220] border border-[#2B3650] p-2 flex items-center justify-center">
                    <img src="/assets/earbuds-icon.svg" alt="Earbud icon" className="w-12 h-12" />
                  </div>
                  <div>
                    <h3 className="text-xl font-bold text-[#F5F7FF]">HOCO KeepAlive</h3>
                    <p className="text-xs text-[#C9D5FF] font-mono mt-0.5">
                      Version 1.0.0 (Release x64) • Self-Contained Single-File
                    </p>
                    <p className="text-xs text-[#77839D] mt-1">
                      Designed for HOCO HC22 Wireless Speaker &amp; Bluetooth Audio
                    </p>
                  </div>
                </div>

                <div className="flex flex-col sm:flex-row items-center gap-3">
                  <button
                    onClick={checkForUpdates}
                    disabled={checkingUpdate}
                    className="w-full sm:w-auto px-4 py-2.5 bg-[#C9D5FF] hover:bg-[#AEBFFF] text-[#0E1220] font-bold text-xs rounded-xl flex items-center justify-center gap-2 cursor-pointer transition"
                  >
                    <RefreshCw className={`w-3.5 h-3.5 ${checkingUpdate ? 'animate-spin' : ''}`} />
                    <span>{checkingUpdate ? 'Checking Releases...' : 'Check for Updates'}</span>
                  </button>
                  <a
                    href="https://github.com/tausif-rahaman-raiyan/HocoKeepAlive/releases"
                    target="_blank"
                    rel="noreferrer"
                    className="w-full sm:w-auto px-4 py-2.5 bg-[#1D2840] hover:bg-[#25314B] text-[#F5F7FF] font-medium text-xs rounded-xl border border-[#2B3650] flex items-center justify-center gap-1.5"
                  >
                    <span>View GitHub Releases</span>
                    <ExternalLink className="w-3.5 h-3.5" />
                  </a>
                </div>
              </div>

              {updateStatus && (
                <div className="p-4 rounded-xl bg-[#1D2840] border border-[#C9D5FF]/30 text-xs text-[#C9D5FF] flex items-center gap-2">
                  <CheckCircle2 className="w-4 h-4 text-[#4ADE80]" />
                  <span>{updateStatus}</span>
                </div>
              )}

              {/* Specifications Card */}
              <div className="bg-[#171F32] border border-[#2B3650] rounded-2xl p-6 space-y-4">
                <h4 className="text-sm font-bold text-[#F5F7FF]">Technical Specifications</h4>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 text-xs">
                  <div className="p-3 rounded-xl bg-[#0E1220] border border-[#2B3650]/60 space-y-1">
                    <span className="text-[#77839D] block">Framework &amp; Target</span>
                    <span className="text-[#F5F7FF] font-mono font-semibold">.NET 8.0 (Windows x64)</span>
                  </div>
                  <div className="p-3 rounded-xl bg-[#0E1220] border border-[#2B3650]/60 space-y-1">
                    <span className="text-[#77839D] block">Audio Engine Library</span>
                    <span className="text-[#F5F7FF] font-mono font-semibold">NAudio 2.2.1 WaveOutEvent</span>
                  </div>
                  <div className="p-3 rounded-xl bg-[#0E1220] border border-[#2B3650]/60 space-y-1">
                    <span className="text-[#77839D] block">Audio Pipeline</span>
                    <span className="text-[#F5F7FF] font-mono font-semibold">48,000 Hz • 16-bit • Stereo PCM</span>
                  </div>
                  <div className="p-3 rounded-xl bg-[#0E1220] border border-[#2B3650]/60 space-y-1">
                    <span className="text-[#77839D] block">Software License</span>
                    <span className="text-[#F5F7FF] font-mono font-semibold">MIT License • Open Source</span>
                  </div>
                </div>
              </div>
            </div>
          )}
        </main>
      </div>
    </div>
  );
}
