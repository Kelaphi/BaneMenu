<h1 align="center">🔪 BaneMenu 🔪</h1>

<p align="center">
  <a href="https://github.com/Kelaphi/BaneMenu/releases/latest"><img src="https://img.shields.io/github/v/release/Kelaphi/BaneMenu?style=flat-square&label=Version&color=brightgreen" alt="Version"></a>
  <a href="https://github.com/Kelaphi/BaneMenu/releases/latest"><img src="https://img.shields.io/github/downloads/Kelaphi/BaneMenu/total?style=flat-square&label=Downloads&color=deeppink" alt="Downloads"></a>
</p>

The most powerful BepInEx mod menu for Among Us ever, with QoL tools, host utilities, troll options, visuals, and lobby protections.

## Table of Contents
- [Features](#features)
- [Supported Platforms](#supported-platforms)
- [Installation and Usage](#installation-and-usage)
- [Credits](#credits)

---

## Features

### Self / Movement
- Speed options, vent / ladder / zipline helpers
- Shift + right-click teleport (single) and Ctrl + right-click hold teleport
- Hide yourself, immortality, and related self tools

### Visuals
- Zoom
- Fullbright
- Reveal Roles
- Show ghosts, ghost chat visibility helpers, protections display

### Host / Lobby
- Role assignment and lobby host utilities
- Map / lobby spawn helpers where available
- Start timer and lobby options (gated where appropriate)

### Troll / Rejoin
- Quick Rejoin with delay
- Force Host / Kick Host / Anti Start Game rejoin loops
- Nullstorm and other lobby pressure tools (use responsibly)

### Protections / Detection
- Mod menu fingerprint detection
- Anticheat-style checks and notifications (server / game patches may limit older bypass ideas)

> [!IMPORTANT]
> Older **anticheat bypass** style behavior has been **patched** by the game / servers. Do not expect previous bypass methods to keep working.

---

## Supported Platforms

| Platform | Status |
|----------|--------|
| Steam (Windows, 64-bit) | Supported |
| Epic Games | Generally supported (match BepInEx arch) |
| itch.io | Supported (32-bit BepInEx) |
| Microsoft Store / Xbox app | Supported |
| Mobile / Consoles | Not supported |

---

## Installation and Usage

### 1. Install BepInEx (IL2CPP)

Among Us uses Unity IL2CPP. You need **BepInEx Unity IL2CPP**, with the **same architecture** as your game (x64 for Steam).

1. Open your Among Us install folder (the folder that contains `Among Us.exe` and `GameAssembly.dll`).
2. Extract BepInEx so that `winhttp.dll`, `BepInEx/`, and `dotnet/` sit next to `Among Us.exe`.
3. Launch the game once so BepInEx can generate folders (first launch can be slow and may show a console).

**Steam:** Library -> Among Us -> Manage -> Browse local files.

### 2. Install BaneMenu

1. Download `BaneMenu.dll` from [Releases](https://github.com/Kelaphi/BaneMenu/releases).
2. Copy it into `BepInEx/plugins/`.
3. Start Among Us. A successful load shows BaneMenu available in-game.

### 3. Open the menu

Default open key is **Delete**. Use the section list on the left and feature panes on the right.

---

## Credits

- [Jelly](https://github.com/Kelaphi/BaneMenu)
- [Kelaphi](https://github.com/Kelaphi/BaneMenu)
- [hpwd](https://github.com/MrDiamond64/Hydra)
- [g0aty](https://github.com/g0aty/SickoMenu)
- [TyGamer4](https://github.com/NewTabGames/Hydralum)
- [alexor_27](https://discord.gg/uacSnNpXN)

---
