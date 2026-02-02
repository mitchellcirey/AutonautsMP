# AutonautsMP

Peer-to-peer multiplayer mod for Autonauts using BepInEx 5 and LiteNetLib.

## Features

- **Host/Join Games** - One player hosts, others connect via IP:Port
- **Player List** - See who's online with customizable usernames
- **Real-time Farmer Sync** - See other players' farmers moving in your world
- **Position Sync** - 10Hz position updates with interpolation
- **World State Sync** - Share your world with joining players

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
1. Load your save game
2. Press F10 to open the multiplayer panel
3. Enter your name and click "Host Game"
4. Share your IP and port (default: 9050) with friends

### As Client
1. Press F10 to open the multiplayer panel
2. Enter your name
3. Enter the host's IP and port
4. Click "Join Game"

### World Sync (Work in Progress)
When you join a game, the host's recent save will be transferred. Load the "_multiplayer_sync" save from the main menu to enter the shared world.

## Architecture

The mod uses a split-assembly design to avoid Unity 2018's assembly scanning issues:

1. **AutonautsMP.dll** (net46) - Main BepInEx plugin
   - Installed to: `Autonauts\BepInEx\plugins\AutonautsMP\`
   - Contains UI, FarmerSync, WorldSync
   - Loaded at game startup

2. **AutonautsMP.Network.dll** + **LiteNetLib.dll** (net471)
   - Installed to: `%APPDATA%\AutonautsMP\`
   - Contains all networking code
   - Loaded dynamically via reflection when you click Host/Join
   - Hidden from Unity's assembly scanner

## Components

- **MultiplayerUI** - IMGUI-based multiplayer panel
- **FarmerSync** - Tracks local farmer position, renders remote farmers as ghosts
- **WorldSync** - Handles world state capture and transfer
- **NetworkBridge** - LiteNetLib wrapper with player tracking and packet handling

## Packet Types

| Type | Description |
|------|-------------|
| PlayerInfo | Player name and ID exchange on connect |
| PlayerPosition | Real-time position/rotation sync (10Hz) |
| PlayerList | Current player roster |
| WorldStateChunk | Chunked world data for initial sync |
| PlayerLeft | Disconnection notification |

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
├── Plugin.cs              # Main plugin entry point
├── FarmerSync.cs          # Local/remote farmer synchronization
├── WorldSync.cs           # World state transfer
├── AutonautsMP.csproj     # Main project (net46)
├── Network/
│   └── Impl/
│       ├── NetworkBridge.cs           # LiteNetLib wrapper
│       ├── Packets.cs                 # Packet type definitions
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

### Players not visible?
- Make sure both players are in the same game world
- Try saving and loading the "_multiplayer_sync" save

### Mod not loading?
- Ensure BepInEx is installed correctly
- Check `BepInEx/LogOutput.log` for errors
