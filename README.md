# DashX360 — Xbox Metro Dashboard for Windows

Created by [ZivvoZ](https://youtube.com/@zivvoz)

If you like this project, donations are welcome but never required: [ko-fi.com/zivvoz](https://ko-fi.com/zivvoz)

DashX360 is a fan-made Windows recreation of the Xbox 360 Metro dashboard: big tiles, controller navigation, Guide overlays, and the panel-based layout from that era.

## Features

- Dashboard tabs: Bing, Home, Social, Video, Games, Music, Apps, Settings
- Controller-first navigation with keyboard and mouse support
- Xbox Guide overlay (Friends, Party, Profile, search)
- Local profile and friend data with cached gamer pictures
- Boot video, dashboard audio cues, and Metro-style tiles
- Open Tray launches a selected game like a disc was inserted
- Customize Open Tray game, cover art, and Home tile artwork in Settings

## Quick start

1. Build or download a release build (see [Building](#building) below).
2. Launch the app (best in fullscreen).
3. Connect a controller.
4. In Steam, disable **Enable Guide Button Chords for controllers** if you use Steam.
5. Open the Guide with **Back + Start** together, or **Win + Left Shift + Left Ctrl**.
6. Navigate with the controller like the original Xbox 360 dashboard.

## Controls

| Input | Action |
| --- | --- |
| `A` / `Enter` | Select |
| `B` / `Escape` | Back |
| `X` | Context actions where available |
| `Y` | Secondary actions where available |
| Back + Start / hotkey | Open Xbox Guide |
| Mouse | Tiles, buttons, and popup menus |

## Building

### Requirements

- Windows 10 or Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Visual Studio 2022 or the .NET CLI

### Commands

```powershell
dotnet restore XboxMetroLauncher.sln
dotnet build XboxMetroLauncher.sln -c Release
```

To publish a self-contained release folder:

```powershell
.\Build\Publish-PublicRelease.ps1
```

Output is written to `Build\publish\` by default. Pass `-OutputDir` to override.

## Configuration

- Settings, profile, and library data are stored outside the repository at runtime.
- This public release uses local-only social and profile data.
- Seed game library: `Data\library.seed.json` (copied on first run).

## Repository layout

| Path | Purpose |
| --- | --- |
| `Assets/` | Tiles, audio, boot video, profile placeholders |
| `Data/` | Seed JSON for the game library |
| `Build/` | Publish script (not committed build output) |
| `Docs/` | Changelog, asset audit, screenshot placeholders |

See [Docs/ASSET_AUDIT.md](Docs/ASSET_AUDIT.md) before redistributing bundled media.

## Legal / disclaimer

This is an unofficial, non-commercial fan project. Xbox, Xbox 360, Xbox LIVE, Microsoft, and related names, logos, and imagery are property of Microsoft. This project is not affiliated with, endorsed by, or sponsored by Microsoft.

Some bundled art, sounds, and reference assets may be derived from commercial software, media, or platform branding. Replace any assets you do not have the right to redistribute before publishing your own build or fork.

This repository is intended for educational, preservation, and fan-project purposes. If a rights holder requests changes or removal, comply promptly.

## Credits

- Original app concept, implementation, and cleanup by the project author and contributors
- Visual inspiration from the Xbox 360 Metro dashboard by Microsoft
