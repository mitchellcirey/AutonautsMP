# AutonautsMP

Peer-to-peer multiplayer mod for Autonauts using BepInEx 5 and LiteNetLib.

## Features

- **Host/Join Games** - One player hosts, others connect via IP:Port
- **Simple UI** - Click the MP button (top-right) or press F10

## Quick Start

```powershell
.\build.ps1    # Build the mod
.\install.ps1  # Install to game
```

Or use the one-click installer:
```powershell
.\package.ps1  # Creates dist/ folder with installer
```

Then launch Autonauts and click the **MP** button (top-right) or press **F10**.

## How to Play Multiplayer

### As Host
1. Launch Autonauts
2. Press F10 to open the multiplayer panel
3. Click "Host Game"
4. Share your IP and port (default: 9050) with friends

### As Client
1. Press F10 to open the multiplayer panel
2. Enter the host's IP and port
3. Click "Join Game"

## Architecture

The mod uses a split-assembly design to avoid Unity 2018's assembly scanning issues:

1. **AutonautsMP.dll** (net46) - Main BepInEx plugin
   - Installed to: `Autonauts\BepInEx\plugins\AutonautsMP\`
   - Contains UI
   - Loaded at game startup

2. **AutonautsMP.Network.dll** + **LiteNetLib.dll** (net471)
   - Installed to: `%APPDATA%\AutonautsMP\`
   - Contains all networking code
   - Loaded dynamically via reflection when you click Host/Join
   - Hidden from Unity's assembly scanner

## Controls

- **F10** - Toggle multiplayer panel
- **MP Button** (top-right) - Toggle multiplayer panel

## Building from Source

Requirements:
- .NET SDK 6.0+
- Autonauts with BepInEx 5 installed

The build script automatically copies required DLLs from your game installation.

## Files

```
AutonautsMP/
├── Plugin.cs              # Main plugin entry point and UI
├── AutonautsMP.csproj     # Main project (net46)
├── Network/
│   └── Impl/
│       ├── NetworkBridge.cs           # LiteNetLib wrapper
│       └── AutonautsMP.Network.csproj # Network project (net471)
├── Installer/
│   └── Program.cs         # One-click installer
├── build.ps1              # Build script
├── install.ps1            # Install script
└── package.ps1            # Create distributable
```

## Troubleshooting

### Can't connect?
- Make sure the host's firewall allows UDP port 9050
- For internet play, the host may need to port-forward 9050

### Mod not loading?
- Ensure BepInEx is installed correctly
- Check `BepInEx/LogOutput.log` for errors
