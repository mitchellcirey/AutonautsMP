# AutonautsMP

A multiplayer mod for [Autonauts](https://store.steampowered.com/app/979120/Autonauts/) using BepInEx 5.

![Hosting Indicator](assets/screenshot-hosting-indicator.png)

![Multiplayer Panel](assets/screenshot-multiplayer-panel.png)

## Features

- **Host & Join** - Start a server or connect to a friend's game
- **In-game UI** - Easy-to-use multiplayer panel accessible via F10 or the AMP button
- **Player List** - See who's connected with host/client indicators
- **Minimal HUD** - Non-intrusive hosting indicator while playing

## Requirements

- Autonauts (Steam version)
- BepInEx 5.x (NOT version 6)

## Installation

1. Install [BepInEx 5.4.22](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.22) into your Autonauts game folder
2. Copy `AutonautsMP.dll` to `Autonauts/BepInEx/plugins/`
3. Launch the game

## Building from Source

```powershell
dotnet build -c Release
```

Output: `bin/Release/AutonautsMP.dll`

## Usage

1. Press **F10** or click the **AMP** button (top-right corner) to open the multiplayer panel
2. Enter your display name
3. Click **HOST** to start a server, or enter an IP and click **JOIN** to connect
4. Share your IP address with friends so they can join (default port: 7777)
5. Press **F10** or **ESC** to close the panel

## Troubleshooting

**ReflectionTypeLoadException errors:**
1. Ensure BepInEx 5.4.22 is installed (NOT version 6)
2. Make sure the mod DLL is in `BepInEx/plugins/` (not a subfolder)
3. Delete the `BepInEx/cache/` folder and restart the game

**Can't connect to host:**
1. The host may need to port forward port 7777 (TCP/UDP)
2. Ensure firewall isn't blocking the connection
3. Verify you're using the correct IP address

## License

MIT
