# AutonautsMP

Multiplayer mod for Autonauts using BepInEx 5.

## Phase 1: Mod Scaffold

This is a minimal implementation to verify mod injection works.

## Building

```powershell
dotnet build -c Release
```

Output: `bin/Release/AutonautsMP.dll`

## Installation

1. Install BepInEx 5.x in Autonauts folder
2. Copy `AutonautsMP.dll` to `Autonauts/BepInEx/plugins/`
3. Launch game

## Usage

- Press **F10** or click **MP** button (top-right) to open panel
- "Multiplayer Mod Loaded" text confirms injection worked

## Troubleshooting

If you get `ReflectionTypeLoadException`, ensure:
1. BepInEx 5.4.22 is installed (NOT version 6)
2. The mod DLL is in `BepInEx/plugins/` (not a subfolder)
3. Delete `BepInEx/cache/` folder and restart
