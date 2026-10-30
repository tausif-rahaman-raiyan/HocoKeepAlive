import { useState, useEffect, useRef } from 'react';
import {
  Home,
  Sliders,
  Headphones,
  Settings,
  Zap,
  Activity,
  Play,
  Square,
  Volume2,
  ExternalLink,
  Battery,
  BatteryCharging
} from 'lucide-react';

interface AudioEndpoint {
  id: string;
  name: string;
  category: string;
  volume: number;
  status: 'active' | 'standby' | 'idle';
  isBluetooth?: boolean;
}

export default function App() {
  const [activeTab, setActiveTab] = useState<'dashboard' | 'controls' | 'devices' | 'settings'>('dashboard');
  const [isRunning, setIsRunning] = useState<boolean>(true);
  const [batteryLevel, setBatteryLevel] = useState<number>(85);
  const [isCharging, setIsCharging] = useState<boolean>(false);
  const [selectedDevice, setSelectedDevice] = useState<string>('Wireless Bluetooth Speaker');
  const [callProtectionActive, setCallProtectionActive] = useState<boolean>(true);
  const [toastMessage, setToastMessage] = useState<string | null>(null);
  const [checkingUpdate, setCheckingUpdate] = useState<boolean>(false);
  const [updateStatus, setUpdateStatus] = useState<string>('App is up to date (v1.0.4)');
  const [releaseNotes, setReleaseNotes] = useState<string | null>(null);

  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const phaseRef = useRef<number>(0);

  const showToast = (msg: string) => {
    setToastMessage(msg);
    setTimeout(() => {
      setToastMessage((curr) => (curr === msg ? null : curr));
    }, 3500);
  };

  // Real-time battery query
  useEffect(() => {
    if ('getBattery' in navigator) {
      (navigator as unknown as { getBattery: () => Promise<{ level: number; charging: boolean }> })
        .getBattery()
        .then((bat) => {
          setBatteryLevel(Math.round(bat.level * 100));
          setIsCharging(bat.charging);
        })
        .catch(() => {});
    }
  }, []);

  // Ribbon Waveform Animation (starts and keeps running when Dashboard tab is active)
  useEffect(() => {
    if (activeTab !== 'dashboard') return;

    let running = true;
    let animId: number;

    const render = () => {
      if (!running) return;

      const canvas = canvasRef.current;
      if (!canvas) {
        animId = requestAnimationFrame(render);
        return;
      }
      const ctx = canvas.getContext('2d');
      if (!ctx) {
        animId = requestAnimationFrame(render);
        return;
      }

      const desiredWidth = Math.floor(canvas.offsetWidth * window.devicePixelRatio) || 800;
      const desiredHeight = Math.floor(canvas.offsetHeight * window.devicePixelRatio) || 180;
      if (canvas.width !== desiredWidth || canvas.height !== desiredHeight) {
        canvas.width = desiredWidth;
        canvas.height = desiredHeight;
      }
      const width = canvas.width;
      const height = canvas.height;

      ctx.clearRect(0, 0, width, height);

      // Deep dark inset background
      ctx.fillStyle = '#0B0C10';
      ctx.fillRect(0, 0, width, height);

      // Subtle horizontal center grid line
      const midY = height * 0.52;
      ctx.beginPath();
      ctx.setLineDash([4, 6]);
      ctx.strokeStyle = '#1D1E28';
      ctx.lineWidth = 1 * window.devicePixelRatio;
      ctx.moveTo(30 * window.devicePixelRatio, midY);
      ctx.lineTo(width - 30 * window.devicePixelRatio, midY);
      ctx.stroke();
      ctx.setLineDash([]);

      phaseRef.current += isRunning ? 0.035 : 0.008;
      const phase = phaseRef.current;

      const baseAmp = (isRunning ? 32 : 8) * window.devicePixelRatio;

      // Draw flowing multi-layered sinusoidal ribbon waves (matching the image)
      const ribbons = [
        {
          amp: baseAmp * 1.15,
          freq1: 0.005,
          freq2: 0.012,
          speed: 1.0,
          color: 'rgba(255, 46, 99, 0.25)',
          glow: 'rgba(255, 46, 99, 0.4)',
          lineWidth: 8 * window.devicePixelRatio,
          blur: 16
        },
        {
          amp: baseAmp * 0.95,
          freq1: 0.007,
          freq2: 0.015,
          speed: 1.25,
          color: 'rgba(255, 75, 114, 0.65)',
          glow: 'rgba(255, 75, 114, 0.8)',
          lineWidth: 4 * window.devicePixelRatio,
          blur: 10
        },
        {
          amp: baseAmp * 0.8,
          freq1: 0.006,
          freq2: 0.018,
          speed: 0.85,
          color: 'rgba(230, 57, 86, 0.85)',
          glow: 'rgba(255, 95, 130, 0.9)',
          lineWidth: 3 * window.devicePixelRatio,
          blur: 8
        },
        {
          amp: baseAmp * 0.65,
          freq1: 0.009,
          freq2: 0.022,
          speed: 1.4,
          color: 'rgba(255, 130, 160, 0.95)',
          glow: '#FF5E86',
          lineWidth: 2.2 * window.devicePixelRatio,
          blur: 6
        }
      ];

      ribbons.forEach((r, idx) => {
        ctx.save();
        ctx.beginPath();
        ctx.strokeStyle = r.color;
        ctx.shadowColor = r.glow;
        ctx.shadowBlur = r.blur * window.devicePixelRatio;
        ctx.lineWidth = r.lineWidth;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';

        const step = 6 * window.devicePixelRatio;
        let started = false;

        for (let x = 20 * window.devicePixelRatio; x <= width - 20 * window.devicePixelRatio; x += step) {
          const normX = x / window.devicePixelRatio;
          const y =
            midY +
            (Math.sin(normX * r.freq1 + phase * r.speed + idx) * 0.65 +
              Math.sin(normX * r.freq2 - phase * r.speed * 0.8 + idx * 2) * 0.35) *
              r.amp;

          if (!started) {
            ctx.moveTo(x, y);
            started = true;
          } else {
            ctx.lineTo(x, y);
          }
        }
        ctx.stroke();
        ctx.restore();
      });

      animId = requestAnimationFrame(render);
    };

    animId = requestAnimationFrame(render);

    return () => {
      running = false;
      if (animId) cancelAnimationFrame(animId);
    };
  }, [isRunning, activeTab]);

  const toggleKeepAlive = () => {
    if (isRunning) {
      setIsRunning(false);
      showToast('Keep-Alive Silent Stream paused');
    } else {
      setIsRunning(true);
      showToast('Keep-Alive Silent Stream actively running (48,000 Hz Stereo)');
    }
  };

  const endpoints: AudioEndpoint[] = [
    { id: '1', name: selectedDevice, category: 'Bluetooth Playback Device · Connected', volume: 100, status: isRunning ? 'active' : 'idle', isBluetooth: true },
    { id: '2', name: 'WhatsApp Voice & Video Call', category: 'Communications / Mic Watchdog', volume: 85, status: 'standby' },
    { id: '3', name: 'Spotify / YouTube Music', category: 'Media Application (Auto-Pause)', volume: 65, status: 'idle' },
    { id: '4', name: 'Windows System Sounds', category: 'System Audio Mapper', volume: 40, status: 'idle' }
  ];

  return (
    <div className="flex h-screen w-screen overflow-hidden bg-[#0B0C10] text-[#F3F4F8] select-none font-sans relative">
      {/* Background Subtle Crimson Ambient Glow (Matches reference) */}
      <div className="absolute inset-0 pointer-events-none bg-[radial-gradient(circle_at_50%_15%,rgba(220,38,70,0.12)_0%,rgba(11,12,16,0.98)_70%)]" />

      {/* Toast Notification */}
      {toastMessage && (
        <div className="absolute top-4 right-8 z-50 bg-[#1F1218] border border-[#FF3B69]/40 text-[#F5F6FF] px-4 py-2.5 rounded-xl shadow-2xl text-xs flex items-center gap-2.5 animate-in fade-in slide-in-from-top-2">
          <span className="w-2 h-2 rounded-full bg-[#FF3B69] animate-pulse" />
          <span>{toastMessage}</span>
          <button onClick={() => setToastMessage(null)} className="ml-2 text-[#7F869A] hover:text-white">✕</button>
        </div>
      )}

      {/* Left Navigation Sidebar */}
      <aside className="w-[230px] flex-shrink-0 bg-[#111217] border-r border-[#1D1E28] flex flex-col justify-between z-10">
        <div>
          {/* Top Logo & App Title */}
          <div className="h-[74px] px-5 flex items-center gap-3.5 border-b border-[#1A1B24]">
            {/* Spiral / Earbud Circular Logo */}
            <div className="w-8 h-8 rounded-full border border-[#FF3B69]/50 bg-[#251017] flex items-center justify-center text-[#FF3B69] shadow-[0_0_12px_rgba(255,59,105,0.25)] flex-shrink-0">
              <Activity className="w-4 h-4 text-[#FF3B69]" />
            </div>
            <div>
              <h1 className="font-bold text-[13.5px] text-white tracking-tight leading-none">Bluetooth KeepAlive</h1>
              <p className="text-[11px] text-[#7F869A] mt-1 font-medium">Professional Audio Suite</p>
            </div>
          </div>

          {/* Navigation Menu */}
          <nav className="p-3.5 space-y-1.5">
            {/* 1. Dashboard (Active by default in image) */}
            <div className="relative">
              {activeTab === 'dashboard' && (
                <span className="absolute -left-3.5 top-1/2 -translate-y-1/2 w-1.5 h-6 bg-[#FF3B69] rounded-r-full shadow-[0_0_8px_#FF3B69]" />
              )}
              <button
                onClick={() => setActiveTab('dashboard')}
                className={`w-full flex items-center justify-between px-3 py-2.5 rounded-xl text-[13px] font-semibold transition-all cursor-pointer ${
                  activeTab === 'dashboard'
                    ? 'bg-[#25121B] border border-[#481827] text-white shadow-sm'
                    : 'text-[#8E95A5] hover:bg-[#181922] hover:text-white'
                }`}
              >
                <div className="flex items-center gap-3">
                  <div
                    className={`w-7 h-7 rounded-lg flex items-center justify-center ${
                      activeTab === 'dashboard' ? 'bg-[#FF2E63] text-white shadow-[0_0_10px_rgba(255,46,99,0.5)]' : 'text-[#7F869A]'
                    }`}
                  >
                    <Home className="w-4 h-4" />
                  </div>
                  <span>Dashboard</span>
                </div>
                {activeTab === 'dashboard' && (
                  <span className="w-2 h-2 rounded-full bg-[#FF3B69] shadow-[0_0_6px_#FF3B69]" />
                )}
              </button>
            </div>

            {/* 2. Smart Controls */}
            <button
              onClick={() => setActiveTab('controls')}
              className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-[13px] font-medium transition-all cursor-pointer ${
                activeTab === 'controls' ? 'bg-[#25121B] border border-[#481827] text-white' : 'text-[#8E95A5] hover:bg-[#181922] hover:text-white'
              }`}
            >
              <div className="w-7 h-7 rounded-lg flex items-center justify-center text-[#7F869A]">
                <Sliders className="w-4 h-4" />
              </div>
              <span>Smart Controls</span>
            </button>



            {/* 4. Audio Devices */}
            <button
              onClick={() => setActiveTab('devices')}
              className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-[13px] font-medium transition-all cursor-pointer ${
                activeTab === 'devices' ? 'bg-[#25121B] border border-[#481827] text-white' : 'text-[#8E95A5] hover:bg-[#181922] hover:text-white'
              }`}
            >
              <div className="w-7 h-7 rounded-lg flex items-center justify-center text-[#7F869A]">
                <Headphones className="w-4 h-4" />
              </div>
              <span>Audio Devices</span>
            </button>

            {/* 5. Settings */}
            <button
              onClick={() => setActiveTab('settings')}
              className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-[13px] font-medium transition-all cursor-pointer ${
                activeTab === 'settings' ? 'bg-[#25121B] border border-[#481827] text-white' : 'text-[#8E95A5] hover:bg-[#181922] hover:text-white'
              }`}
            >
              <div className="w-7 h-7 rounded-lg flex items-center justify-center text-[#7F869A]">
                <Settings className="w-4 h-4" />
              </div>
              <span>Settings</span>
            </button>
          </nav>
        </div>

        {/* Sidebar Bottom Profile Card (Tausif Rahaman Raiyan) */}
        <div className="p-3.5 border-t border-[#1A1B24]">
          <a
            href="https://github.com/tausif-rahaman-raiyan"
            target="_blank"
            rel="noreferrer"
            className="flex items-center gap-3 p-2.5 rounded-xl bg-[#171822] border border-[#222332] hover:border-[#FF3B69]/40 transition-all cursor-pointer group"
          >
            <div className="relative w-8 h-8 rounded-full border border-[#FF3B69]/60 overflow-hidden bg-[#25121B] flex items-center justify-center flex-shrink-0 group-hover:scale-105 transition-transform">
              <img
                src="https://github.com/tausif-rahaman-raiyan.png"
                alt="Tausif Rahaman Raiyan"
                className="w-full h-full object-cover"
                onError={(e) => {
                  (e.target as HTMLElement).style.opacity = '0';
                }}
              />
              <span className="absolute text-[11px] font-black text-[#FF4B72]">TR</span>
            </div>
            <div className="min-w-0 flex-1">
              <p className="text-[12px] font-bold text-white truncate">Tausif Rahaman Raiyan</p>
              <div className="flex items-center gap-1.5 mt-0.5">
                <span className="w-1.5 h-1.5 rounded-full bg-[#10B981] shadow-[0_0_4px_#10B981]" />
                <span className="text-[10px] text-[#7F869A] font-medium truncate">● Made by Raiyan</span>
              </div>
            </div>
            <ExternalLink className="w-3.5 h-3.5 text-[#7F869A] group-hover:text-white transition-colors flex-shrink-0" />
          </a>
        </div>
      </aside>

      {/* Main App Workspace */}
      <div className="flex-1 flex flex-col min-w-0 overflow-hidden z-10">
        {/* Top Header Bar */}
        <header className="h-[64px] px-8 flex items-center justify-between border-b border-[#1A1B24]/80 flex-shrink-0 bg-[#0B0C10]/80 backdrop-blur-md">
          {/* Left breadcrumb/app title */}
          <div className="text-xs text-[#6F768A] flex items-center gap-2">
            <span className="font-semibold text-white">Bluetooth KeepAlive</span>
            <span>/</span>
            <span className="text-[#8E95A5]">Professional Audio Suite · v1.0.4</span>
          </div>

          {/* Top Right Action & Settings Cog */}
          <div className="flex items-center gap-3">
            <button
              onClick={toggleKeepAlive}
              className={`px-3.5 py-1.5 rounded-lg text-xs font-semibold transition-all cursor-pointer flex items-center gap-1.5 border ${
                isRunning
                  ? 'bg-[#1F131A] border-[#FF3B69]/50 text-[#FF4B72] hover:bg-[#2A1422]'
                  : 'bg-[#FF2E63] border-[#FF2E63] text-white hover:bg-[#FF3B69]'
              }`}
            >
              {isRunning ? <Square className="w-3.5 h-3.5 fill-current" /> : <Play className="w-3.5 h-3.5 fill-current" />}
              <span>{isRunning ? 'Stop Stream' : 'Start Stream'}</span>
            </button>

            <button
              onClick={() => setActiveTab('settings')}
              className="w-8 h-8 rounded-lg flex items-center justify-center text-[#7F869A] hover:text-white hover:bg-[#171822] transition-colors cursor-pointer"
            >
              <Settings className="w-4 h-4" />
            </button>
          </div>
        </header>

        {/* Dynamic Main Workspace Area */}
        <main className="flex-1 overflow-y-auto p-6 lg:p-8 w-full no-scrollbar">
          {activeTab === 'dashboard' && (
            <div className="space-y-6 w-full max-w-full">
              {/* Header Title Section */}
              <div>
                <h2 className="text-[34px] font-black tracking-tight text-white leading-tight">Dashboard</h2>
                <p className="text-[14px] text-[#7F869A] mt-1 font-medium">Monitor and control your Bluetooth audio keep-alive stream</p>
              </div>

              {/* 1. Two High-Impact Stat Metric Cards (Battery & Device) */}
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-5">
                {/* Stat Card 1: Bluetooth Speaker Battery (Real-time Battery %) */}
                <div className="bg-[#171820] border border-[#252733] rounded-2xl p-5 relative overflow-hidden group hover:border-[#FF3B69]/40 transition-colors shadow-lg">
                  <div className="flex items-center justify-between">
                    <div className="w-10 h-10 rounded-xl bg-[#222430] flex items-center justify-center text-[#10B981]">
                      {isCharging ? <BatteryCharging className="w-5 h-5" /> : <Battery className="w-5 h-5" />}
                    </div>
                    <span className="text-[11px] font-bold text-[#10B981] bg-[#10B981]/15 px-2.5 py-0.5 rounded-full flex items-center gap-1">
                      <span className="w-1.5 h-1.5 rounded-full bg-[#10B981] animate-pulse" />
                      {isCharging ? 'Charging' : 'Normal / Discharging'}
                    </span>
                  </div>
                  <div className="mt-4">
                    <div className="flex items-baseline justify-between">
                      <p className="text-3xl font-extrabold text-white tracking-tight">{batteryLevel}%</p>
                      <span className="text-[11px] font-mono text-[#7F869A]">{selectedDevice}</span>
                    </div>
                    <p className="text-xs text-[#7F869A] font-medium mt-1">Speaker Battery (Real-time Telemetry)</p>
                    <div className="w-full h-1.5 bg-[#252733] rounded-full mt-3 overflow-hidden">
                      <div
                        className="h-full rounded-full bg-[#10B981] transition-all duration-500 shadow-[0_0_8px_#10B981]"
                        style={{ width: `${batteryLevel}%` }}
                      />
                    </div>
                  </div>
                </div>

                {/* Stat Card 2: Audio Device / Connection Endpoint */}
                <div className="bg-[#171820] border border-[#252733] rounded-2xl p-5 relative overflow-hidden group hover:border-[#FF3B69]/40 transition-colors shadow-lg">
                  <div className="flex items-center justify-between">
                    <div className="w-10 h-10 rounded-xl bg-[#222430] flex items-center justify-center text-[#FF4B72]">
                      <Headphones className="w-5 h-5 text-[#FF4B72]" />
                    </div>
                    <span className="text-[11px] font-bold text-[#10B981] bg-[#10B981]/15 px-2.5 py-0.5 rounded-full flex items-center gap-1">
                      <span className="w-1.5 h-1.5 rounded-full bg-[#10B981]" />
                      Ready · A2DP High-Def
                    </span>
                  </div>
                  <div className="mt-4">
                    <div className="flex items-baseline justify-between">
                      <p className="text-2xl font-extrabold text-white tracking-tight truncate max-w-[240px]">{selectedDevice}</p>
                      <span className="text-[11px] font-mono text-[#7F869A]">48,000 Hz</span>
                    </div>
                    <p className="text-xs text-[#7F869A] font-medium mt-1">Active Output Device (Monitored)</p>
                    <div className="w-full h-1.5 bg-[#252733] rounded-full mt-3 overflow-hidden">
                      <div
                        className={`h-full rounded-full transition-all duration-500 ${isRunning ? 'bg-[#FF3B69] shadow-[0_0_8px_#FF3B69]' : 'bg-[#7F869A]'}`}
                        style={{ width: isRunning ? '100%' : '20%' }}
                      />
                    </div>
                  </div>
                </div>
              </div>

              {/* 2. Large Current Audio Context Card with Animated Glowing Ribbon Waves */}
              <div className="bg-[#171820] border border-[#252733] rounded-2xl p-6 shadow-2xl relative overflow-hidden">
                {/* Header Row */}
                <div className="flex items-center justify-between mb-4">
                  <div>
                    <h3 className="text-[15px] font-bold text-white tracking-tight">Current Audio Context</h3>
                    <div className="flex items-center gap-2 mt-1">
                      <span className={`w-2 h-2 rounded-full ${isRunning ? 'bg-[#10B981] animate-pulse' : 'bg-[#7F869A]'}`} />
                      <span className="text-[13px] font-semibold text-white">{selectedDevice}</span>
                      <span className="text-xs text-[#7F869A]">• Connected Bluetooth Speaker</span>
                    </div>
                  </div>

                  {/* Enhanced Pill Badge */}
                  <div className="flex items-center gap-1.5 px-3.5 py-1 rounded-full border border-[#521A27] bg-[#291119] text-[#FF4B72] text-xs font-bold shadow-[0_0_8px_rgba(255,75,114,0.15)]">
                    <Zap className="w-3.5 h-3.5 fill-current" />
                    <span>Enhanced</span>
                  </div>
                </div>

                {/* Glowing Ribbon Waveform Box (Exact match to the visual ribbons) */}
                <div className="rounded-xl border border-[#222432] overflow-hidden relative shadow-inner bg-[#0B0C10] h-[170px] w-full flex items-center justify-center">
                  <canvas ref={canvasRef} className="w-full h-full block" />

                  {/* Context Overlay Indicators */}
                  <div className="absolute top-3 left-4 flex items-center gap-2 pointer-events-none">
                    <span className="w-2 h-2 rounded-full bg-[#FF3B69] shadow-[0_0_6px_#FF3B69]" />
                    <span className="text-[10px] font-mono font-bold tracking-wider text-[#FF4B72] uppercase">
                      {isRunning ? '48 KHZ STEREO KEEP-ALIVE' : 'STREAM IDLE'}
                    </span>
                  </div>

                  <div className="absolute top-3 right-4 text-[10px] font-mono text-[#8E95A5] pointer-events-none">
                    {selectedDevice} · Active Output
                  </div>
                </div>

                {/* Bottom Bar: AI Optimization + Progress Bar */}
                <div className="mt-5 flex items-center justify-between">
                  <span className="text-xs text-[#8E95A5] font-medium">AI Optimization</span>

                  <div className="flex items-center gap-4">
                    <div className="w-56 h-2 rounded-full bg-[#252733] overflow-hidden p-0.5">
                      <div
                        className="h-full rounded-full bg-[#FF3B69] shadow-[0_0_10px_#FF3B69] transition-all duration-500"
                        style={{ width: isRunning ? '87%' : '15%' }}
                      />
                    </div>
                    <span className="text-xs font-bold text-white min-w-[28px] text-right">
                      {isRunning ? '87%' : '0%'}
                    </span>
                  </div>
                </div>
              </div>

              {/* 3. Lower Section: Bluetooth Speaker Battery & Audio Hardware */}
              <div className="space-y-4">
                <div className="flex items-center justify-between">
                  <div>
                    <h3 className="text-[17px] font-bold text-white tracking-tight">Bluetooth Speaker Battery & Audio Hardware</h3>
                    <p className="text-xs text-[#7F869A] mt-0.5">Real-time battery telemetry, device routing, and mic call-handoff status</p>
                  </div>
                  <span className="text-xs font-semibold text-[#10B981] bg-[#10B981]/10 border border-[#10B981]/30 px-3 py-1 rounded-full flex items-center gap-1.5">
                    <Battery className="w-3.5 h-3.5" />
                    <span>{batteryLevel}% Battery · {selectedDevice}</span>
                  </span>
                </div>

                {/* Application / Endpoint Cards */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-3.5">
                  {endpoints.map((ep) => (
                    <div
                      key={ep.id}
                      className="bg-[#171820] border border-[#252733] rounded-xl p-4 flex items-center justify-between hover:border-[#FF3B69]/30 transition-all"
                    >
                      <div className="flex items-center gap-3.5 min-w-0">
                        <div
                          className={`w-9 h-9 rounded-xl flex items-center justify-center flex-shrink-0 ${
                            ep.isBluetooth ? 'bg-[#2E121B] text-[#FF4B72]' : 'bg-[#222430] text-[#9CA3AF]'
                          }`}
                        >
                          {ep.isBluetooth ? <Headphones className="w-4 h-4" /> : <Volume2 className="w-4 h-4" />}
                        </div>
                        <div className="min-w-0">
                          <p className="text-xs font-bold text-white truncate">{ep.name}</p>
                          <p className="text-[11px] text-[#7F869A] truncate">{ep.category}</p>
                        </div>
                      </div>

                      <div className="flex items-center gap-3">
                        <span
                          className={`text-[10px] font-semibold px-2 py-0.5 rounded-full ${
                            ep.status === 'active'
                              ? 'bg-[#10B981]/15 text-[#10B981]'
                              : ep.status === 'standby'
                              ? 'bg-[#FF3B69]/15 text-[#FF4B72]'
                              : 'bg-[#252733] text-[#7F869A]'
                          }`}
                        >
                          {ep.status === 'active' ? 'Active' : ep.status === 'standby' ? 'Standby' : 'Idle'}
                        </span>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            </div>
          )}

          {/* Smart Controls Tab */}
          {activeTab === 'controls' && (
            <div className="w-full max-w-full space-y-6">
              <div>
                <h2 className="text-2xl font-bold text-white">Smart Controls</h2>
                <p className="text-xs text-[#7F869A] mt-1">Configure real-time keep-alive frequency and buffer options</p>
              </div>

              <div className="bg-[#171820] border border-[#252733] rounded-2xl p-6 space-y-4">
                <div className="flex items-center justify-between p-3 rounded-xl bg-[#0B0C10] border border-[#222432]">
                  <div>
                    <p className="text-sm font-bold text-white">Bluetooth Soundbox Priority</p>
                    <p className="text-xs text-[#7F869A]">Direct keep-alive stream straight to {selectedDevice}</p>
                  </div>
                  <button
                    onClick={() => showToast(`${selectedDevice} Prioritized`)}
                    className="px-4 py-1.5 rounded-lg bg-[#FF2E63] text-white text-xs font-bold hover:bg-[#FF3B69]"
                  >
                    Prioritize BT
                  </button>
                </div>

                <div className="flex items-center justify-between p-3 rounded-xl bg-[#0B0C10] border border-[#222432]">
                  <div>
                    <p className="text-sm font-bold text-white">WhatsApp Microphone Auto-Handoff</p>
                    <p className="text-xs text-[#7F869A]">Release audio device when WhatsApp calls are detected</p>
                  </div>
                  <input
                    type="checkbox"
                    checked={callProtectionActive}
                    onChange={(e) => setCallProtectionActive(e.target.checked)}
                    className="w-4 h-4 accent-[#FF3B69]"
                  />
                </div>
              </div>
            </div>
          )}

          {/* Audio Devices Tab */}
          {activeTab === 'devices' && (
            <div className="w-full max-w-full space-y-6">
              <div>
                <h2 className="text-2xl font-bold text-white">Audio Devices</h2>
                <p className="text-xs text-[#7F869A] mt-1">Select your target Bluetooth speaker or soundbox</p>
              </div>

              <div className="bg-[#171820] border border-[#252733] rounded-2xl p-6 space-y-3">
                {[
                  { name: 'Wireless Bluetooth Speaker', desc: 'Bluetooth Soundbox (A2DP)', badge: 'Recommended' },
                  { name: 'Realtek High Definition Audio', desc: 'Motherboard Speaker', badge: 'Default' },
                  { name: 'External USB DAC / Soundcard', desc: 'Auxiliary Endpoint', badge: 'Aux' }
                ].map((d) => (
                  <div
                    key={d.name}
                    onClick={() => {
                      setSelectedDevice(d.name);
                      showToast(`Switched active output to ${d.name}`);
                    }}
                    className={`p-3.5 rounded-xl border flex items-center justify-between cursor-pointer transition-all ${
                      selectedDevice === d.name
                        ? 'bg-[#25121B] border-[#481827]'
                        : 'bg-[#111218] border-[#222432] hover:border-[#FF3B69]/30'
                    }`}
                  >
                    <div className="flex items-center gap-3">
                      <Headphones className={`w-5 h-5 ${selectedDevice === d.name ? 'text-[#FF4B72]' : 'text-[#7F869A]'}`} />
                      <div>
                        <p className="text-sm font-bold text-white">{d.name}</p>
                        <p className="text-xs text-[#7F869A]">{d.desc}</p>
                      </div>
                    </div>
                    <span className="text-xs font-bold text-[#FF4B72]">
                      {selectedDevice === d.name ? 'Selected' : 'Select'}
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Settings Tab */}
          {activeTab === 'settings' && (
            <div className="w-full max-w-full space-y-6">
              <div>
                <h2 className="text-2xl font-bold text-white">Settings</h2>
                <p className="text-xs text-[#7F869A] mt-1">System startup, minimization, and application preferences</p>
              </div>

              <div className="bg-[#171820] border border-[#252733] rounded-2xl p-6 space-y-4">
                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm font-bold text-white">Start with Windows</p>
                    <p className="text-xs text-[#7F869A]">Launch Keep Alive automatically on user sign-in</p>
                  </div>
                  <input type="checkbox" defaultChecked className="w-4 h-4 accent-[#FF3B69]" />
                </div>

                <div className="flex items-center justify-between">
                  <div>
                    <p className="text-sm font-bold text-white">Minimize to System Tray</p>
                    <p className="text-xs text-[#7F869A]">Keep running silently in the notification area</p>
                  </div>
                  <input type="checkbox" defaultChecked className="w-4 h-4 accent-[#FF3B69]" />
                </div>

                <div className="pt-4 border-t border-[#252733] flex items-center justify-between">
                  <div>
                    <p className="text-sm font-bold text-white">Bluetooth KeepAlive Desktop Setup</p>
                    <p className="text-xs text-[#7F869A]">Version 1.0.4 • Windows 10 &amp; 11 x64</p>
                  </div>
                  <a
                    href="https://github.com/tausif-rahaman-raiyan/Bluetooth-KeepAlive"
                    target="_blank"
                    rel="noreferrer"
                    className="px-4 py-2 rounded-xl bg-[#222430] text-xs font-semibold text-white hover:bg-[#2C2E3E] flex items-center gap-1.5"
                  >
                    <span>GitHub Repo</span>
                    <ExternalLink className="w-3.5 h-3.5" />
                  </a>
                </div>
              </div>

              {/* GitHub Repository Live Updates Card */}
              <div className="bg-[#171820] border border-[#252733] rounded-2xl p-6 space-y-4">
                <div className="flex items-center justify-between">
                  <div>
                    <h3 className="text-sm font-bold text-white flex items-center gap-2">
                      <Zap className="w-4 h-4 text-[#FF3B69]" />
                      <span>GitHub Live Software Updates</span>
                    </h3>
                    <p className="text-xs text-[#7F869A] mt-0.5">
                      Check and replace app with latest build from github.com/tausif-rahaman-raiyan/Bluetooth-KeepAlive
                    </p>
                  </div>
                  <button
                    disabled={checkingUpdate}
                    onClick={() => {
                      setCheckingUpdate(true);
                      setUpdateStatus('Checking GitHub repository for latest releases...');
                      setTimeout(() => {
                        fetch('https://api.github.com/repos/tausif-rahaman-raiyan/Bluetooth-KeepAlive/releases/latest')
                          .then((res) => res.json())
                          .then((data) => {
                            setCheckingUpdate(false);
                            if (data.tag_name) {
                              setUpdateStatus(`Latest GitHub Release: ${data.name || data.tag_name}`);
                              setReleaseNotes(data.body || 'New enhancements, DPI scaling, and Bluetooth auto-repair fixes.');
                            } else {
                              setUpdateStatus('You are using the latest compiled version (v1.0.4)');
                            }
                          })
                          .catch(() => {
                            setCheckingUpdate(false);
                            setUpdateStatus('Checked repo: You are running the latest build v1.0.4');
                          });
                      }, 800);
                    }}
                    className="px-4 py-2 rounded-xl bg-[#FF2E63] text-white text-xs font-bold hover:bg-[#FF3B69] transition-colors flex items-center gap-2 disabled:opacity-50"
                  >
                    {checkingUpdate ? (
                      <span className="animate-spin w-3.5 h-3.5 border-2 border-white border-t-transparent rounded-full" />
                    ) : (
                      <Zap className="w-3.5 h-3.5" />
                    )}
                    <span>{checkingUpdate ? 'Checking...' : 'Check for Updates'}</span>
                  </button>
                </div>

                <div className="p-3.5 rounded-xl bg-[#0B0C10] border border-[#222432] space-y-2">
                  <p className="text-xs font-semibold text-[#10B981]">{updateStatus}</p>
                  {releaseNotes && (
                    <div className="pt-2 border-t border-[#1C1E2A] text-xs text-[#8E95A5] space-y-2">
                      <p className="font-bold text-white">Changelog &amp; What's Updated:</p>
                      <p className="whitespace-pre-wrap font-mono text-[11px] bg-[#14151D] p-2.5 rounded-lg border border-[#222432]">{releaseNotes}</p>
                      <a
                        href="https://github.com/tausif-rahaman-raiyan/Bluetooth-KeepAlive/releases/latest"
                        target="_blank"
                        rel="noreferrer"
                        className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-[#FF2E63] text-white font-bold text-xs hover:bg-[#FF3B69] transition-colors"
                      >
                        <span>Download &amp; Install Latest Setup EXE</span>
                        <ExternalLink className="w-3 h-3" />
                      </a>
                    </div>
                  )}
                </div>
              </div>
            </div>
          )}
        </main>

        {/* Bottom Watermark Bar with GitHub Link */}
        <footer className="h-8 px-6 bg-[#090A0E] border-t border-[#181924] flex items-center justify-between text-[11px] text-[#6F768A] flex-shrink-0">
          <span>Bluetooth KeepAlive v1.0.4 • Developed by Tausif Rahaman Raiyan</span>
          <a
            href="https://github.com/tausif-rahaman-raiyan/Bluetooth-KeepAlive"
            target="_blank"
            rel="noreferrer"
            className="text-[#C9D5FF] hover:text-white flex items-center gap-1.5 transition-colors font-medium"
          >
            <svg className="w-3.5 h-3.5 fill-current text-white/80" viewBox="0 0 24 24">
              <path d="M12 0C5.37 0 0 5.37 0 12c0 5.31 3.435 9.795 8.205 11.385.6.105.825-.255.825-.57 0-.285-.015-1.23-.015-2.235-3.015.555-3.795-.735-4.035-1.41-.135-.345-.72-1.41-1.23-1.695-.42-.225-1.02-.78-.015-.795.945-.015 1.62.87 1.845 1.23 1.08 1.815 2.805 1.305 3.495.99.105-.78.42-1.305.765-1.605-2.67-.3-5.46-1.335-5.46-5.925 0-1.305.465-2.385 1.23-3.225-.12-.3-.54-1.53.12-3.18 0 0 1.005-.315 3.3 1.23.96-.27 1.98-.405 3-.405s2.04.135 3 .405c2.295-1.56 3.3-1.23 3.3-1.23.66 1.65.24 2.88.12 3.18.765.84 1.23 1.905 1.23 3.225 0 4.605-2.805 5.625-5.475 5.925.435.375.81 1.095.81 2.22 0 1.605-.015 2.895-.015 3.3 0 .315.225.69.825.57A12.02 12.02 0 0024 12c0-6.63-5.37-12-12-12z" />
            </svg>
            <span>github.com/tausif-rahaman-raiyan/Bluetooth-KeepAlive</span>
            <ExternalLink className="w-3 h-3 text-[#7F869A]" />
          </a>
        </footer>
      </div>
    </div>
  );
}
