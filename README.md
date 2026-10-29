# HOCO KeepAlive

[![Build HOCO KeepAlive for Windows](https://github.com/tausif-rahaman-raiyan/HocoKeepAlive/actions/workflows/build.yml/badge.svg)](https://github.com/tausif-rahaman-raiyan/HocoKeepAlive/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20(x64)-0078d4.svg)]()
[![Framework](https://img.shields.io/badge/.NET-8.0-512bd4.svg)]()

**HOCO KeepAlive** is a lightweight, low-overhead Windows notification-area (system tray) utility designed to help keep the **HOCO HC22** Bluetooth speaker (and other wireless speakers or headphones) connected during long periods of silence by maintaining an active, continuous digital silent audio stream through the selected Windows playback output device.

---

## Table of Contents
1. [Overview & How It Works](#overview--how-it-works)
2. [Supported Windows Versions](#supported-windows-versions)
3. [Features](#features)
4. [Download & Deliverables](#download--deliverables)
5. [Installation & Setup](#installation--setup)
6. [Portable Mode](#portable-mode)
7. [How to Use](#how-to-use)
8. [Device Selection (HOCO HC22)](#device-selection-hoco-hc22)
9. [Windows Startup Configuration](#windows-startup-configuration)
10. [Battery & Power Consumption Notice](#battery--power-consumption-notice)
11. [Known Limitations & Disclaimers](#known-limitations--disclaimers)
12. [Troubleshooting Guide](#troubleshooting-guide)
13. [Local Build from Source](#local-build-from-source)
14. [License](#license)

---

## Overview & How It Works

Many Bluetooth speakers—including the HOCO HC22—incorporate aggressive power-saving firmware. When Windows stops playing audio for several minutes, the Bluetooth audio sink goes idle. The speaker or adapter may turn off its internal amplifier, suspend the Bluetooth link, or enter a deep sleep timeout, requiring reconnecting or power cycling.

**HOCO KeepAlive** solves this by supplying a continuous stream of digital silence (zero-value PCM audio buffers) at **48,000 Hz, 16-bit stereo** directly to the audio output endpoint via NAudio:
- Zero audible noise, music, or volume changes.
- Uses low-latency audio callbacks with static buffer clearing (`Array.Clear`), consuming near **0% CPU** and negligible memory.
- Windows audio subsystem and Bluetooth A2DP audio link remain actively engaged.

---

## Supported Windows Versions

- **Windows 11** (64-bit, all editions)
- **Windows 10** (64-bit, version 1809 or later)
- No separate .NET installation is required (the executable is compiled as a self-contained single-file application).

---

## Features

- **Custom Cute Earbuds Icon**: Features the dual-earbud visual design in multiple resolutions (16x16 to 256x256), embedded directly into the executable, tray icon, and installer.
- **System Tray Native**: Runs silently in the Windows taskbar notification area; double-click the tray icon to open the status dashboard.
- **Audio Output Selection**: Easily select the HOCO HC22 or any connected Windows playback device.
- **Graceful Error Recovery**: Detects when devices disconnect or become unavailable without crashing or looping indefinitely.
- **Call-Friendly Audio Handoff**: When foreground playback is detected, releases the keep-alive output handle instead of leaving it paused, giving Windows a better chance to switch a Bluetooth headset/speakerphone to its hands-free profile for microphone calls; it resumes after playback becomes quiet.
- **Optional Windows Sign-in Startup**: Start automatically when logging into Windows via clean user registry configuration.
- **Single Instance Enforcement**: Protects against running multiple conflicting instances.

---

## Download & Deliverables

A single sequential GitHub Actions workflow builds and packages all Windows deliverables together on each push, pull request, or manual run. Release assets are published from that same workflow when requested or when a version tag is pushed. The installer keeps the existing AppId and install directory so version 1.0.4 is designed to update the existing installation in place:

1. **`HOCO-KeepAlive-Setup-1.0.4.exe`**: Clean Inno Setup installer with optional desktop shortcut and startup registration.
2. **`HOCO-KeepAlive-Portable-1.0.4-win-x64.zip`**: Zero-install portable edition.
3. **`HOCO KeepAlive.exe`**: Self-contained single-file executable.

### How to Download from GitHub Actions
1. Go to **Actions** in the GitHub repository.
2. Click on the latest workflow run on `main`.
3. Scroll to the **Artifacts** section at the bottom.
4. Download the **`HocoKeepAlive-Windows`** artifact ZIP containing all three files.

### How to Install from a GitHub Release
When a release tag like `v1.0.4` is published, the setup executable, portable ZIP, and standalone EXE are attached directly to the release page.

---

## Installation & Setup

1. Download **`HOCO-KeepAlive-Setup-1.0.4.exe`**.
2. Run the installer (runs with standard user privileges; no administrator rights required).
3. Follow the wizard prompts to optionally add a desktop shortcut or enable launch on Windows sign-in.
4. Click **Finish** to launch HOCO KeepAlive.

---

## Portable Mode

If you prefer not to install:
1. Download **`HOCO-KeepAlive-Portable-1.0.4-win-x64.zip`**.
2. Extract the ZIP into any user folder (e.g., `Documents\HOCO KeepAlive`).
3. Double-click **`HOCO KeepAlive.exe`**.
4. Settings are stored in your per-user `%APPDATA%\HocoKeepAlive\settings.json` file.

---

## How to Use

1. Launch **HOCO KeepAlive**.
2. The app icon appears in your notification area (taskbar tray, next to the clock). Windows may place it under the **^ (Show hidden icons)** chevron.
3. **Start Keep-Alive**: Right-click the tray icon and select **Start silent keep-alive** (or click the button in the main window).
4. **Stop Keep-Alive**: Right-click the tray icon and select **Stop silent keep-alive** whenever you do not need it.
5. **Open Status Window**: Double-click the tray icon or right-click and select **Open Status & Devices...**.

---

## Device Selection (HOCO HC22)

1. Open the Status Window by double-clicking the tray icon.
2. In the **Audio Playback Device** dropdown, select your **HOCO HC22** (or keep "Windows Default Playback Device" if the HC22 is already set as your default).
3. If you just paired your speaker, click **Refresh** to reload the device list.
4. If the speaker disconnects, the app displays `Device unavailable` and safely waits for the device to return without busy-waiting.

---

## Windows Startup Configuration

To have HOCO KeepAlive launch automatically when you sign in to Windows:
1. Open the Status Window and click **Settings...**.
2. Check **Start HOCO KeepAlive when I sign in to Windows**.
3. Click **Save**.
*(This registers an entry under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` and requires no administrator privileges).*

---

## Battery & Power Consumption Notice

> **Important Battery Note:**
> An active silent audio stream prevents the Bluetooth radio and speaker amplifier from entering standby. While playing digital silence consumes less computational power than music, **silence does NOT guarantee zero battery drain**.
>
> The Bluetooth transmitter on your PC and the receiver/amplifier inside the HOCO HC22 remain in high-power communication mode. For best battery life on both your PC and your speaker, **stop the keep-alive stream when not in use**.

---

## Known Limitations & Disclaimers

- **Bluetooth microphone compatibility:** Classic Bluetooth devices commonly cannot provide high-quality stereo A2DP playback and the hands-free microphone profile at the same time. HOCO KeepAlive now releases its playback handle when other output audio is detected, but Windows/driver/device profile switching still controls microphone availability. If a WhatsApp call still reports no microphone, stop Keep-Alive for the call and select the device's **Hands-Free / Headset** microphone in Windows and WhatsApp. This app does not capture, route, or modify microphone audio.
- **Firmware Hard Sleeps**: Some Bluetooth speaker models or firmware revisions have strict internal power-off timers that measure audio sample amplitude directly in hardware. If zero amplitude is detected for 10-15 minutes, the hardware may shut off regardless of an active connection.
- **Physical Disconnections**: This software cannot prevent disconnects caused by low battery, excessive distance, radio frequency interference, or Windows entering Sleep / Modern Standby.
- **No Inaudible Tone Guarantee**: This utility uses absolute zero PCM (`0x00`), which is completely silent. It does not inject ultrasonic or sub-audible dither tones that could distort your hardware.

---

## Troubleshooting Guide

| Issue | Solution |
|---|---|
| **Speaker still powers off after 10 minutes** | Verify in Windows Sound Settings that **HOCO HC22 Stereo** is set as default output rather than Hands-Free AG Audio. Check speaker battery level. |
| **Tray icon not visible** | Click the **^** (arrow) on the Windows taskbar. Drag the cute earbud icon into the visible taskbar area. |
| **"Device unavailable" warning** | Turn on the HOCO HC22 and ensure Bluetooth is paired and connected in Windows Settings. Click **Refresh** in the main window. |
| **Windows turns off Bluetooth adapter** | Open **Device Manager** > **Bluetooth** > your Bluetooth adapter > **Properties** > **Power Management** > uncheck *"Allow the computer to turn off this device to save power"*. |

---

## Local Build from Source

Requirements:
- Windows 10/11 x64
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Inno Setup 6 (optional, for compiling the setup installer)

```powershell
# 1. Clone repository
git clone https://github.com/tausif-rahaman-raiyan/HocoKeepAlive.git
cd HocoKeepAlive

# 2. Generate and validate multi-resolution icon
./scripts/Generate-Icon.ps1

# 3. Restore and run automated tests
dotnet test tests/HocoKeepAlive.Tests/HocoKeepAlive.Tests.csproj

# 4. Publish self-contained executable
dotnet publish HocoKeepAlive.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish

# 5. Build installer (requires Inno Setup)
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer.iss
```

---

## License

This project is licensed under the [MIT License](LICENSE).
