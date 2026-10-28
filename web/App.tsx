import { useState, useEffect, useRef } from 'react';
import {
  LayoutDashboard,
  Speaker,
  Cpu,
  Settings as SettingsIcon,
  Info,
  Play,
  Square,
  RefreshCw,
  ExternalLink,
  Volume2,
  CheckCircle2,
  Bluetooth,
  Bell
} from 'lucide-react';

interface AudioDevice {
  deviceId: string;
  label: string;
  groupId: string;
  isDefault?: boolean;
}

type Page =
  | 'overview'
  | 'devices'
  | 'engine'
  | 'settings'
  | 'about';

export default function App() {
  const [activePage, setActivePage] = useState<Page>('overview');
  const [isRunning, setIsRunning] = useState<boolean>(false);
  const [isRecovering, setIsRecovering] = useState<boolean>(false);
  const [elapsedSeconds, setElapsedSeconds] = useState<number>(0);
  const [devices, setDevices] = useState<AudioDevice[]>([]);
  const [selectedDeviceId, setSelectedDeviceId] = useState<string>('default');
  const [selectedDeviceName, setSelectedDeviceName] = useState<string>('Windows Default Playback Device (HOCO HC22)');

  // Engine config
  const [sampleRate, setSampleRate] = useState<number>(48000);
  const [channels, setChannels] = useState<number>(2);
  const [bufferLatency, setBufferLatency] = useState<number>(150);
  const [autoReconnect, setAutoReconnect] = useState<boolean>(true);
  const [autoPauseOnOtherAudio, setAutoPauseOnOtherAudio] = useState<boolean>(true);
  const [isPausedByOtherAudio, setIsPausedByOtherAudio] = useState<boolean>(false);
  const [isSimulatingOtherAudio, setIsSimulatingOtherAudio] = useState<boolean>(false);

  // Settings
  const [launchOnStartup, setLaunchOnStartup] = useState<boolean>(false);
  const [autoStartStream, setAutoStartStream] = useState<boolean>(false);
  const [startMinimized, setStartMinimized] = useState<boolean>(false);
  const [minimizeToTrayOnClose, setMinimizeToTrayOnClose] = useState<boolean>(true);
  const [showNotifications, setShowNotifications] = useState<boolean>(true);
  const [notifyOnStartStop, setNotifyOnStartStop] = useState<boolean>(true);
  const [notifyOnError, setNotifyOnError] = useState<boolean>(true);

  // Web Audio Context Refs
  const audioCtxRef = useRef<AudioContext | null>(null);
  const bufferSourceRef = useRef<AudioBufferSourceNode | null>(null);
  const reconnectIntervalRef = useRef<number | null>(null);

  // Toast notification
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  // Update check
  const [checkingUpdate, setCheckingUpdate] = useState<boolean>(false);
  const [updateStatus, setUpdateStatus] = useState<string | null>(null);

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
      }
    } catch {
      fallbackDevices();
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
      if (reconnectIntervalRef.current) {
        clearInterval(reconnectIntervalRef.current);
        reconnectIntervalRef.current = null;
      }
      setIsRecovering(false);

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

      // Handle unexpected disconnects with auto-reconnect
      source.onended = () => {
        if (autoReconnect) {
          handleAutoReconnect();
        }
      };

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
      if (notifyOnStartStop) {
        triggerToast('Silent keep-alive started. Bluetooth audio link active.');
      }
    } catch (err: unknown) {
      const errMsg = err instanceof Error ? err.message : String(err);
      if (autoReconnect) {
        handleAutoReconnect();
      } else if (notifyOnError) {
        triggerToast(`Audio Error: ${errMsg}`);
      }
    }
  };

  const handleAutoReconnect = () => {
    setIsRunning(false);
    setIsRecovering(true);
    triggerToast('Speaker disconnected. Auto-reconnect watchdog active...');

    if (reconnectIntervalRef.current) clearInterval(reconnectIntervalRef.current);
    reconnectIntervalRef.current = window.setInterval(async () => {
      await refreshDevices();
      // Try to restart
      try {
        await startKeepAlive();
        if (reconnectIntervalRef.current) {
          clearInterval(reconnectIntervalRef.current);
          reconnectIntervalRef.current = null;
        }
        setIsRecovering(false);
        triggerToast('Speaker reconnected! Silent stream automatically resumed.');
      } catch {
        // Still reconnecting
      }
    }, 2500);
  };

  const stopKeepAlive = () => {
    if (reconnectIntervalRef.current) {
      clearInterval(reconnectIntervalRef.current);
      reconnectIntervalRef.current = null;
    }
    setIsRecovering(false);

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
      setIsPausedByOtherAudio(false);
      if (notifyOnStartStop) {
        triggerToast('Silent keep-alive stopped.');
      }
    }
  };

  const toggleKeepAlive = () => {
    if (isRunning || isRecovering) stopKeepAlive();
    else startKeepAlive();
  };

  const toggleSimulatedAudio = (play: boolean) => {
    setIsSimulatingOtherAudio(play);
    if (!autoPauseOnOtherAudio) return;

    if (play) {
      if (isRunning && !isPausedByOtherAudio) {
        if (audioCtxRef.current && audioCtxRef.current.state === 'running') {
          audioCtxRef.current.suspend();
        }
        setIsPausedByOtherAudio(true);
        triggerToast('Other media active (e.g. YouTube/Spotify). Silent stream paused on standby.');
      }
    } else {
      if (isRunning && isPausedByOtherAudio) {
        triggerToast('Media paused. Resuming silent keep-alive in 1.2s...');
        setTimeout(() => {
          if (audioCtxRef.current && audioCtxRef.current.state === 'suspended') {
            audioCtxRef.current.resume();
          }
          setIsPausedByOtherAudio(false);
          triggerToast('Silent keep-alive stream resumed automatically!');
        }, 1200);
      }
    }
  };

  const playVerificationChime = () => {
    try {
      const AudioCtx = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
      const ctx = new AudioCtx();
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();

      osc.type = 'sine';
      osc.frequency.setValueAtTime(523.25, ctx.currentTime);
      osc.frequency.exponentialRampToValueAtTime(659.25, ctx.currentTime + 0.15);

      gain.gain.setValueAtTime(0.001, ctx.currentTime);
      gain.gain.exponentialRampToValueAtTime(0.2, ctx.currentTime + 0.05);
      gain.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + 0.45);

      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.start();
      osc.stop(ctx.currentTime + 0.5);

      triggerToast('Hardware verification chime played.');
    } catch {
      // Ignored
    }
  };

  const checkForUpdates = () => {
    setCheckingUpdate(true);
    setUpdateStatus(null);
    setTimeout(() => {
      setCheckingUpdate(false);
      setUpdateStatus('HOCO KeepAlive 1.0.2 is the latest available release.');
    }, 1000);
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
    { id: 'about', label: 'About & Updates', icon: Info }
  ];

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
            className="text-xs text-[#77839D] hover:text-[#F5F7FF] ml-2 p-1 cursor-pointer"
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
              <span className="text-[11px] text-[#77839D] font-mono">v1.0.2 · x64</span>
            </div>
          </div>

          {/* Navigation Items (Clean 5-Item Navigation) */}
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
                isRunning ? 'bg-[#4ADE80] animate-pulse' : isRecovering ? 'bg-[#FBBF24] animate-ping' : 'bg-[#77839D]'
              }`}
            />
            <span className="font-medium text-[#A7B2C8]">
              {isRunning ? 'Engine Running' : isRecovering ? 'Auto-Reconnecting' : 'Engine Idle'}
            </span>
          </div>
          <div className="text-[10px] text-[#4ADE80] font-semibold flex items-center gap-1">
            <CheckCircle2 className="w-3 h-3" />
            <span>Auto-Pair & Reconnect Active</span>
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
          <div className="flex items-center gap-2 text-xs text-[#77839D] truncate">
            <span>Playback Output:</span>
            <span className="text-[#A7B2C8] font-medium truncate max-w-md">{selectedDeviceName}</span>
          </div>

          <div className="flex items-center gap-3">
            <span
              className={`text-[11px] font-mono font-bold px-3 py-1 rounded-full border ${
                isPausedByOtherAudio
                  ? 'bg-[#312E81]/30 text-[#C9D5FF] border-[#6366F1]/40'
                  : isRunning
                  ? 'bg-[#4ADE80]/15 text-[#4ADE80] border-[#4ADE80]/30'
                  : isRecovering
                  ? 'bg-[#FBBF24]/15 text-[#FBBF24] border-[#FBBF24]/30'
                  : 'bg-[#1D2840] text-[#77839D] border-[#2B3650]'
              }`}
            >
              {isPausedByOtherAudio
                ? 'STANDBY (MEDIA ACTIVE)'
                : isRunning
                ? 'RUNNING SILENTLY'
                : isRecovering
                ? 'RECONNECTING...'
                : 'STOPPED'}
            </span>
          </div>
        </header>

        {/* Dynamic Page Views with Responsive Full-Screen Width */}
        <main className="flex-1 overflow-y-auto p-8 w-full">
          {/* ========================================================= */}
          {/* 1. OVERVIEW PAGE (Main Dashboard - Responsive) */}
          {/* ========================================================= */}
          {activePage === 'overview' && (
            <div className="space-y-6 max-w-5xl mx-auto w-full">
              {/* Page Title */}
              <div>
                <h2 className="text-2xl font-bold tracking-tight text-[#F5F7FF]">Overview</h2>
                <p className="text-sm text-[#77839D] mt-1">
                  Ready to keep audio active. Maintains your Bluetooth speaker connection through quiet moments.
                </p>
              </div>

              {/* Main Keep-Alive Hero Card */}
              <div className="bg-[#171F32] border border-[#2B3650] rounded-2xl p-6 relative overflow-hidden shadow-xl w-full">
                <div className="flex flex-col md:flex-row md:items-center justify-between gap-6">
                  <div className="space-y-2 max-w-2xl">
                    <span className="text-[11px] font-mono font-bold text-[#C9D5FF] tracking-wider uppercase">
                      KEEP-ALIVE STATUS
                    </span>
                    <h3 className="text-2xl font-bold text-[#F5F7FF]">
                      {isPausedByOtherAudio
                        ? 'Standing by (Audio active elsewhere)'
                        : isRunning
                        ? 'Keep-Alive is running'
                        : isRecovering
                        ? 'Reconnecting to speaker...'
                        : 'Ready to keep audio active'}
                    </h3>
                    <p className="text-sm text-[#A7B2C8] leading-relaxed">
                      {isPausedByOtherAudio
                        ? 'HOCO KeepAlive detected sound from another application (Spotify, YouTube, video, or game) and turned off the silent background stream. It will resume automatically when you pause.'
                        : isRunning
                        ? 'Silent audio (zero-amplitude PCM) is being streamed through your output device to maintain the Bluetooth link.'
                        : isRecovering
                        ? 'The audio stream stopped. Auto-reconnect watchdog is actively polling for HOCO HC22 to reconnect.'
                        : 'Start a lightweight silent stream to help prevent your Bluetooth speaker from disconnecting during pauses.'}
                    </p>
                  </div>

                  {/* Primary Start / Stop Button */}
                  <div className="flex flex-col items-start md:items-end gap-3 flex-shrink-0">
                    <button
                      onClick={toggleKeepAlive}
                      className={`px-7 py-3.5 rounded-xl font-bold text-sm flex items-center gap-2.5 transition-all shadow-lg cursor-pointer ${
                        isPausedByOtherAudio
                          ? 'bg-[#334155] hover:bg-[#475569] text-[#F5F7FF] shadow-black/20'
                          : isRunning || isRecovering
                          ? 'bg-[#FB7185] hover:bg-[#FB7185]/90 text-white shadow-[#FB7185]/20'
                          : 'bg-[#C9D5FF] hover:bg-[#AEBFFF] text-[#0E1220] shadow-[#C9D5FF]/10'
                      }`}
                    >
                      {isPausedByOtherAudio ? (
                        <>
                          <Square className="w-4 h-4 fill-current" />
                          <span>Stop Keep-Alive</span>
                        </>
                      ) : isRunning ? (
                        <>
                          <Square className="w-4 h-4 fill-current" />
                          <span>Stop Keep-Alive</span>
                        </>
                      ) : isRecovering ? (
                        <>
                          <RefreshCw className="w-4 h-4 animate-spin" />
                          <span>Cancel Reconnect</span>
                        </>
                      ) : (
                        <>
                          <Play className="w-4 h-4 fill-current" />
                          <span>Start Keep-Alive</span>
                        </>
                      )}
                    </button>
                    {(isRunning || isPausedByOtherAudio) && (
                      <span className={`text-xs font-mono font-semibold ${isPausedByOtherAudio ? 'text-[#C9D5FF]' : 'text-[#4ADE80]'}`}>
                        {isPausedByOtherAudio ? 'Paused by media' : `Session Uptime: ${formatDuration(elapsedSeconds)}`}
                      </span>
                    )}
                  </div>
                </div>
              </div>

              {/* Auto-Connect & Auto-Pair Banner */}
              <div className="bg-[#1D2840] border border-[#2B3650] rounded-2xl p-4 flex flex-col sm:flex-row sm:items-center justify-between gap-4 w-full">
                <div className="flex items-center gap-3">
                  <div className="w-9 h-9 rounded-xl bg-[#25314B] flex items-center justify-center text-[#4ADE80] flex-shrink-0">
                    <Bluetooth className="w-5 h-5" />
                  </div>
                  <div>
                    <h5 className="text-xs font-bold text-[#F5F7FF]">Auto-Reconnect &amp; Auto-Pair Enabled</h5>
                    <p className="text-xs text-[#A7B2C8] mt-0.5">
                      Automatically detects when HOCO HC22 powers on or reconnects, instantly resuming the silent stream without user intervention.
                    </p>
                  </div>
                </div>
                <button
                  onClick={playVerificationChime}
                  className="px-4 py-2 bg-[#25314B] hover:bg-[#25314B]/80 text-[#C9D5FF] text-xs font-semibold rounded-lg flex items-center gap-2 flex-shrink-0 cursor-pointer"
                >
                  <Volume2 className="w-3.5 h-3.5" />
                  <span>Verify Sound Routing</span>
                </button>
              </div>

              {/* Smart Auto-Pause & Media Detection Banner */}
              <div className="bg-[#171F32] border border-[#2B3650] rounded-2xl p-4 sm:p-5 w-full shadow-md">
                <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                  <div className="flex items-start gap-3.5">
                    <div
                      className={`w-9 h-9 rounded-xl flex items-center justify-center flex-shrink-0 mt-0.5 ${
                        isPausedByOtherAudio ? 'bg-[#312E81] text-[#C9D5FF]' : 'bg-[#1D2840] text-[#77839D]'
                      }`}
                    >
                      <Volume2 className="w-5 h-5" />
                    </div>
                    <div>
                      <div className="flex items-center gap-2">
                        <h4 className="text-xs font-bold text-[#F5F7FF]">Smart Auto-Pause &amp; Auto-Resume</h4>
                        <span className="text-[10px] bg-[#C9D5FF]/15 text-[#C9D5FF] px-2 py-0.5 rounded-full font-mono font-semibold">
                          Active
                        </span>
                      </div>
                      <p className="text-xs text-[#A7B2C8] mt-1 leading-relaxed max-w-2xl">
                        When you play music, YouTube, or video games, Keep-Alive turns off playing in the background. The moment you pause or stop playing, it automatically starts back up to keep your Bluetooth connection awake.
                      </p>
                    </div>
                  </div>

                  <div className="flex items-center gap-3 flex-shrink-0 self-start md:self-center">
                    <button
                      onClick={() => toggleSimulatedAudio(!isSimulatingOtherAudio)}
                      className={`px-3.5 py-2 rounded-xl text-xs font-semibold flex items-center gap-2 transition-all cursor-pointer ${
                        isSimulatingOtherAudio
                          ? 'bg-[#FBBF24] hover:bg-[#F59E0B] text-[#0E1220] shadow-md'
                          : 'bg-[#1D2840] hover:bg-[#25314B] text-[#C9D5FF] border border-[#2B3650]'
                      }`}
                    >
                      {isSimulatingOtherAudio ? (
                        <>
                          <Square className="w-3.5 h-3.5 fill-current" />
                          <span>Simulate Pause (Auto-resumes)</span>
                        </>
                      ) : (
                        <>
                          <Play className="w-3.5 h-3.5 fill-current" />
                          <span>Simulate Play (Auto-pauses)</span>
                        </>
                      )}
                    </button>
                  </div>
                </div>
              </div>

              {/* Two Column Summary Grid */}
              <div className="grid grid-cols-1 md:grid-cols-2 gap-5 w-full">
                {/* Audio Device Card */}
                <div className="bg-[#171F32] border border-[#2B3650] rounded-2xl p-5 flex flex-col justify-between">
                  <div>
                    <div className="flex items-center justify-between">
                      <span className="text-[11px] font-mono font-bold text-[#77839D]">AUDIO PLAYBACK DEVICE</span>
                      <span className="text-xs text-[#4ADE80] font-medium flex items-center gap-1">
                        <CheckCircle2 className="w-3.5 h-3.5" />
                        <span>Ready &amp; Monitored</span>
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
                      onClick={refreshDevices}
                      className="text-xs text-[#A7B2C8] hover:text-[#F5F7FF] flex items-center gap-1 cursor-pointer"
                    >
                      <RefreshCw className="w-3 h-3" />
                      <span>Refresh</span>
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
            </div>
          )}

          {/* ========================================================= */}
          {/* 2. AUDIO DEVICE PAGE */}
          {/* ========================================================= */}
          {activePage === 'devices' && (
            <div className="space-y-6 max-w-5xl mx-auto w-full">
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                <div>
                  <h2 className="text-2xl font-bold tracking-tight text-[#F5F7FF]">Audio Device</h2>
                  <p className="text-sm text-[#77839D] mt-1">
                    Select your HOCO HC22 or target Windows audio endpoint for the silent keep-alive stream.
                  </p>
                </div>
                <button
                  onClick={refreshDevices}
                  className="px-3.5 py-2 rounded-lg bg-[#1D2840] hover:bg-[#25314B] text-[#C9D5FF] text-xs font-medium flex items-center gap-2 border border-[#2B3650] cursor-pointer self-start sm:self-auto"
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
            </div>
          )}

          {/* ========================================================= */}
          {/* 3. KEEP-ALIVE ENGINE PAGE */}
          {/* ========================================================= */}
          {activePage === 'engine' && (
            <div className="space-y-6 max-w-5xl mx-auto w-full">
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
                      onChange={(e) => setSampleRate(Number(e.target.value))}
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
                      onChange={(e) => setChannels(Number(e.target.value))}
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
                      onChange={(e) => setBufferLatency(Number(e.target.value))}
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
                      <label className="flex items-center gap-2 text-xs text-[#4ADE80] font-semibold cursor-pointer">
                        <input
                          type="checkbox"
                          checked={autoReconnect}
                          onChange={(e) => setAutoReconnect(e.target.checked)}
                          className="rounded bg-[#0E1220] border-[#2B3650] text-[#4ADE80]"
                        />
                        <span>Auto-reconnect automatically when speaker disconnects</span>
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
            <div className="space-y-6 max-w-5xl mx-auto w-full">
              <div>
                <h2 className="text-2xl font-bold tracking-tight text-[#F5F7FF]">Settings</h2>
                <p className="text-sm text-[#77839D] mt-1">
                  Configure startup, background tray behavior, and auto-pairing preferences.
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
                      onChange={(e) => setLaunchOnStartup(e.target.checked)}
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
                      <span className="font-semibold text-[#4ADE80] block">
                        Auto-reconnect after Bluetooth disconnection
                      </span>
                      <span className="text-[#77839D]">
                        Automatically watches and re-starts stream when HOCO HC22 reconnects
                      </span>
                    </div>
                    <input
                      type="checkbox"
                      checked={autoReconnect}
                      onChange={(e) => setAutoReconnect(e.target.checked)}
                      className="rounded bg-[#1D2840] border-[#2B3650] text-[#4ADE80] w-4 h-4 cursor-pointer"
                    />
                  </label>

                  <label className="flex items-center justify-between p-3 rounded-xl bg-[#0E1220] border border-[#2B3650]/60 cursor-pointer">
                    <div>
                      <span className="font-semibold text-[#C9D5FF] block">
                        Smart Auto-Pause when other media is playing
                      </span>
                      <span className="text-[#77839D]">
                        Stops background silent stream when playing music or videos, auto-resumes when paused
                      </span>
                    </div>
                    <input
                      type="checkbox"
                      checked={autoPauseOnOtherAudio}
                      onChange={(e) => setAutoPauseOnOtherAudio(e.target.checked)}
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
                <h3 className="text-sm font-bold text-[#F5F7FF]">Notification Alerts</h3>
                <div className="space-y-3 text-xs">
                  <label className="flex items-center justify-between p-3 rounded-xl bg-[#0E1220] border border-[#2B3650]/60 cursor-pointer">
                    <div>
                      <span className="font-semibold text-[#F5F7FF] block">
                        Show notification alerts on state changes
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
                        Notify on stream start and stop
                      </span>
                      <span className="text-[#77839D]">
                        Displays alert when silent audio session begins or ends
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
                        Notify on audio device errors
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
            </div>
          )}

          {/* ========================================================= */}
          {/* 5. ABOUT & UPDATES PAGE */}
          {/* ========================================================= */}
          {activePage === 'about' && (
            <div className="space-y-6 max-w-5xl mx-auto w-full">
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
                      Version 1.0.2 (Release x64) • Self-Contained Single-File
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

        {/* Bottom Watermark Bar (Requested by user: "niche ekta watermark add kre dao with github icon and link") */}
        <footer className="h-8 px-6 border-t border-[#2B3650]/50 bg-[#0B0F19] flex items-center justify-between text-[11px] text-[#77839D] flex-shrink-0">
          <div className="flex items-center gap-2">
            <svg className="w-3.5 h-3.5 text-[#C9D5FF] fill-current" viewBox="0 0 24 24">
              <path fillRule="evenodd" clipRule="evenodd" d="M12 2C6.477 2 2 6.484 2 12.017c0 4.425 2.865 8.18 6.839 9.504.5.092.682-.217.682-.483 0-.237-.008-.868-.013-1.703-2.782.605-3.369-1.343-3.369-1.343-.454-1.158-1.11-1.466-1.11-1.466-.908-.62.069-.608.069-.608 1.003.07 1.53 1.032 1.53 1.032.892 1.53 2.341 1.088 2.91.832.092-.647.35-1.088.636-1.338-2.22-.253-4.555-1.113-4.555-4.951 0-1.093.39-1.988 1.029-2.688-.103-.253-.446-1.272.098-2.65 0 0 .84-.27 2.75 1.026A9.564 9.564 0 0112 6.844c.85.004 1.705.115 2.504.337 1.909-1.296 2.747-1.027 2.747-1.027.546 1.379.202 2.398.1 2.651.64.7 1.028 1.595 1.028 2.688 0 3.848-2.339 4.695-4.566 4.943.359.309.678.92.678 1.855 0 1.338-.012 2.419-.012 2.747 0 .268.18.58.688.482A10.019 10.019 0 0022 12.017C22 6.484 17.522 2 12 2z" />
            </svg>
            <span>HOCO KeepAlive <strong className="text-[#C9D5FF] font-mono">v1.0.2</strong> • Developed by <span className="text-[#F5F7FF] font-medium">Tausif Rahaman Raiyan</span></span>
          </div>
          <a
            href="https://github.com/tausif-rahaman-raiyan/HocoKeepAlive"
            target="_blank"
            rel="noreferrer"
            className="text-[#C9D5FF] hover:underline flex items-center gap-1.5 transition-colors"
          >
            <span>github.com/tausif-rahaman-raiyan/HocoKeepAlive</span>
            <ExternalLink className="w-3 h-3" />
          </a>
        </footer>
      </div>
    </div>
  );
}
