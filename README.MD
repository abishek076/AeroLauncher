# Aero Launcher

A console-inspired, full-screen Windows game library launcher built with **C#**, **.NET 10**, and **WPF**. Designed for controller, keyboard, and mouse navigation, Aero Launcher automatically fetches game artwork from SteamGridDB, manages local libraries via JSON persistence, and ships with automated xUnit testing, Velopack delta updates, and a GitHub Actions CI/CD release pipeline.

![Aero Launcher Dashboard](docs/dashboard.png)

---

## Key Features

- **Horizontal Console Dashboard:** Borderless full-screen interface featuring a blurred 90% wide Hero Banner backdrop, centered library branding, and a smooth horizontal game carousel.
- **Automated Dual-Artwork Pipeline:** Queries the **SteamGridDB API** asynchronously to download both vertical `600x900` grid posters and wide `1920x1080` hero banners without file-locking conflicts, with manual file-browser fallbacks.
- **Multi-Input Navigation:** Supports Xbox controllers (`SharpDX.XInput` D-Pad, `A`, `B`, `Y`, `Start`), keyboard shortcuts (`Arrow Keys`, `Enter`, `Space`), and mouse clicks.
- **Multi-Format Game Launching:** Launches `.exe` binaries, `.lnk` shortcuts, and silently mounts `.iso` disc images via PowerShell, minimizing to the Windows System Tray (`NotifyIcon`) until the game exits.
- **Built-In Auto-Updater:** Powered by **Velopack**, checking GitHub Releases for new versions and applying updates without overwriting user data in `%AppData%`.

---

## Architecture

Business logic is decoupled from `MainWindow.xaml.cs` into isolated, dependency-injected services so core persistence, networking, and process execution can be tested headlessly without a running WPF window:

```text
AeroLauncher/
├── GameLibraryLauncher/
│   ├── Models/
│   │   └── GameItem.cs               # Observable game model with JSON serialization rules
│   ├── Services/
│   │   ├── LibraryService.cs         # JSON persistence for games.json and settings.json
│   │   ├── SteamGridService.cs       # Async SteamGridDB API search & artwork downloader
│   │   ├── GameLauncherService.cs    # Process execution, .iso mounting & UAC cancellation handling
│   │   └── UpdateService.cs          # Velopack GitHub Release update checker & applier
│   ├── MainWindow.xaml               # Console dashboard & modal overlays
│   └── MainWindow.xaml.cs            # Thin UI controller, XInput loop & SoundPlayer cues
├── AeroLauncher.Tests/
│   ├── LibraryServiceTests.cs        # Isolated temp-directory persistence & state reset tests
│   ├── SteamGridServiceTests.cs      # Offline StubHttpMessageHandler API & 401 fallback tests
│   └── GameLauncherServiceTests.cs   # Delegate-injected launch, .iso & UAC (1223) tests
└── .github/workflows/
    └── ci-release.yml                # Automated xUnit test gate & Velopack release builder
