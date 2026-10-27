# HOCO KeepAlive

A small Windows 10/11 notification-area utility that plays digital silence to test whether keeping a Windows audio stream open helps prevent the HOCO HC22 from disconnecting during pauses.

## Features
- Standard Windows application icon and notification-area icon. Windows may place the tray icon under **Show hidden icons** based on taskbar settings.
- Right-click the tray icon to start or stop the silent stream, view battery guidance, or exit.
- No audible tones, music, microphone access, recording, registry changes, or driver changes.
- Low-overhead audio callback; the app does not poll continuously.
- GitHub Actions builds a self-contained Windows x64 app ZIP and setup installer.

## Speaker battery note
There is no way to guarantee both a permanently active Bluetooth audio link and zero additional speaker battery use. The speaker and Bluetooth radio remain active while the audio link is maintained. This app uses silence rather than music, but silence does **not** guarantee lower Bluetooth battery consumption. For best battery life, leave keep-alive stopped when you do not need it. Do not use continuous keep-alive while running on battery unless the connection benefit is worth it.

## Limitations
Windows, the Bluetooth adapter, or the speaker firmware may still disconnect the link. This app cannot guarantee a permanent connection. It starts stopped so you can choose when to trade battery life for a potentially more persistent audio connection.

## Download
Open **Actions → latest successful Build Windows app run → Artifacts → HocoKeepAlive-Windows**. The ZIP and installer are in the artifact. Tag a release like `v1.0.0` to attach both files to a GitHub Release.

## Use
1. Install or extract the ZIP.
2. Launch **HOCO KeepAlive.exe**; it stays in the notification area.
3. Right-click the tray icon and select **Start silent keep-alive**.
4. Ensure **HOCO HC22** is selected as the Windows playback output.
5. Select **Stop silent keep-alive** when you no longer need it.

## Build locally
On Windows with the .NET 8 SDK and Inno Setup installed:
```powershell
./scripts/Generate-Icon.ps1
dotnet restore
dotnet publish HocoKeepAlive.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
iscc installer.iss
```

## License
MIT
