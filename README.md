# HOCO KeepAlive

A lightweight Windows tray utility that streams digital silence to the current Windows audio output to test whether this prevents idle Bluetooth audio disconnections, especially with the HOCO HC22.

## Features
- Native Windows Forms app with a standard application icon and notification-area icon.
- Windows may place the tray icon under Show hidden icons depending on taskbar settings.
- Right-click the tray icon to start/stop the silent stream or exit.
- 48 kHz, 16-bit stereo PCM silence; no audible tones, music, microphone access, recording, or network activity.
- GitHub Actions builds a self-contained Windows x64 executable and a setup installer.
- No registry, driver, or power-plan changes.

## Important limitation
Digital silence keeps an audio stream open, but Windows, the Bluetooth adapter, or the speaker firmware can still disconnect an idle Bluetooth link. This utility cannot guarantee a permanent connection. It uses a small continuous audio stream, so it is not zero CPU or battery use. Stop it when you do not need it.

## Download
Open the repository Actions tab, select the latest successful Build Windows app run, and download the HocoKeepAlive-Windows artifact. A versioned installer is also attached to the GitHub Release created by the workflow.

## Run
Launch HOCO KeepAlive.exe. The app starts in the tray. Right-click the tray icon and choose Start silent keep-alive. Make sure HOCO HC22 is the selected Windows playback output. Choose Stop silent keep-alive to stop the stream; choose Exit to close the app.

## Build locally
Requires .NET 8 SDK on Windows:
    dotnet restore
    dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true

## License
MIT