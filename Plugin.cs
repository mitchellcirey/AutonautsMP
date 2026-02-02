using System;
using System.IO;
using System.Reflection;
using BepInEx;
using UnityEngine;

namespace AutonautsMP
{
    [BepInPlugin("com.autonautsmp.mod", "AutonautsMP", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin? Instance { get; private set; }
        public static FarmerSync? FarmerSyncInstance { get; private set; }
        public static WorldSync? WorldSyncInstance { get; private set; }

        private void Awake()
        {
            Instance = this;
            Logger.LogInfo("AutonautsMP loaded");
            var go = new GameObject("AutonautsMP");
            DontDestroyOnLoad(go);
            go.AddComponent<MultiplayerUI>();
            FarmerSyncInstance = go.AddComponent<FarmerSync>();
            WorldSyncInstance = go.AddComponent<WorldSync>();
        }

        public void Log(string message)
        {
            Logger.LogInfo(message);
        }
    }

    public class MultiplayerUI : MonoBehaviour
    {
        private bool _show = true;
        private string _ip = "127.0.0.1";
        private string _port = "9050";
        private string _playerName = "Player";
        private string _status = "Not connected";
        private Vector2 _playerListScroll;

        // Network loaded via reflection
        private object? _network;
        private MethodInfo? _hostMethod;
        private MethodInfo? _joinMethod;
        private MethodInfo? _disconnectMethod;
        private MethodInfo? _updateMethod;
        private MethodInfo? _getStatusMethod;
        private MethodInfo? _setPlayerNameMethod;
        private MethodInfo? _getPlayersMethod;
        private MethodInfo? _getPlayerCountMethod;
        private MethodInfo? _isHostMethod;
        private MethodInfo? _isConnectedMethod;
        private MethodInfo? _sendPositionMethod;
        private MethodInfo? _getLocalPlayerIdMethod;
        private MethodInfo? _sendWorldStateMethod;
        private bool _triedLoad;
        private string? _loadError;

        // Cached player data for display
        private object[]? _players;
        private float _lastPlayerRefresh;

        private void Start()
        {
            // Try to get Steam name on start
            TryGetSteamName();
        }

        private void TryGetSteamName()
        {
            // Try to get Steam persona name from the game
            try
            {
                // Look for SteamManager or similar in the game
                var steamManagerType = Type.GetType("SteamManager, Assembly-CSharp");
                if (steamManagerType != null)
                {
                    // Game has Steam integration, try to get name
                    Plugin.Instance?.Log("Found SteamManager, attempting to get Steam name...");
                }
            }
            catch
            {
                // Fallback to default name
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F10))
                _show = !_show;

            // Poll network
            if (_network != null && _updateMethod != null)
            {
                try { _updateMethod.Invoke(_network, null); } catch { }
            }

            // Refresh player list periodically
            if (_network != null && Time.time - _lastPlayerRefresh > 0.5f)
            {
                _lastPlayerRefresh = Time.time;
                RefreshPlayerList();
                
                // Update FarmerSync with local player ID
                if (_getLocalPlayerIdMethod != null && Plugin.FarmerSyncInstance != null)
                {
                    try
                    {
                        int localId = (int)_getLocalPlayerIdMethod.Invoke(_network, null)!;
                        Plugin.FarmerSyncInstance.SetLocalPlayerId(localId);
                    }
                    catch { }
                }
            }
        }

        private void RefreshPlayerList()
        {
            if (_getPlayersMethod == null) return;
            try
            {
                _players = (object[]?)_getPlayersMethod.Invoke(_network, null);
            }
            catch { }
        }

        private void OnGUI()
        {
            // MP button always visible
            if (GUI.Button(new Rect(Screen.width - 70, 10, 60, 40), "MP"))
                _show = !_show;

            if (!_show) return;

            float x = 20, y = 20, w = 520, h = 520;
            GUI.Box(new Rect(x, y, w, h), "AutonautsMP");

            // Update status from network
            if (_network != null && _getStatusMethod != null)
            {
                try { _status = (string)_getStatusMethod.Invoke(_network, null)!; } catch { }
            }

            float cy = y + 25;

            // Player name input
            GUI.Label(new Rect(x + 10, cy, 80, 20), "Your Name:");
            _playerName = GUI.TextField(new Rect(x + 90, cy, 200, 22), _playerName);
            cy += 30;

            // Status
            GUI.Label(new Rect(x + 10, cy, w - 20, 20), "Status: " + _status);
            cy += 30;

            // Host button
            if (GUI.Button(new Rect(x + 10, cy, w - 20, 30), "Host Game"))
            {
                if (LoadNetwork())
                {
                    try
                    {
                        _setPlayerNameMethod?.Invoke(_network, new object[] { _playerName });
                        _hostMethod?.Invoke(_network, new object[] { int.Parse(_port) });
                    }
                    catch (Exception e) { _status = "Error: " + e.Message; }
                }
            }
            cy += 40;

            // IP/Port input
            GUI.Label(new Rect(x + 10, cy, 25, 20), "IP:");
            _ip = GUI.TextField(new Rect(x + 35, cy, 200, 25), _ip);
            GUI.Label(new Rect(x + 250, cy, 35, 20), "Port:");
            _port = GUI.TextField(new Rect(x + 290, cy, 80, 25), _port);
            cy += 35;

            // Join button
            if (GUI.Button(new Rect(x + 10, cy, w - 20, 30), "Join Game"))
            {
                if (LoadNetwork())
                {
                    try
                    {
                        _setPlayerNameMethod?.Invoke(_network, new object[] { _playerName });
                        _joinMethod?.Invoke(_network, new object[] { _ip, int.Parse(_port) });
                    }
                    catch (Exception e) { _status = "Error: " + e.Message; }
                }
            }
            cy += 40;

            // Disconnect button
            if (GUI.Button(new Rect(x + 10, cy, w - 20, 30), "Disconnect"))
            {
                if (_network != null && _disconnectMethod != null)
                {
                    try { _disconnectMethod.Invoke(_network, null); } catch { }
                }
                _status = "Disconnected";
            }
            cy += 45;

            // Player list section
            GUI.Box(new Rect(x + 10, cy, w - 20, 150), "Players Online");
            cy += 20;

            // Player list scroll view
            if (_players != null && _players.Length > 0)
            {
                float listY = cy;
                _playerListScroll = GUI.BeginScrollView(
                    new Rect(x + 15, listY, w - 30, 120),
                    _playerListScroll,
                    new Rect(0, 0, w - 50, _players.Length * 25)
                );

                float itemY = 0;
                foreach (var playerObj in _players)
                {
                    if (playerObj == null) continue;

                    // Get player properties via reflection
                    var playerType = playerObj.GetType();
                    var idField = playerType.GetField("Id");
                    var nameField = playerType.GetField("Name");

                    int id = idField != null ? (int)idField.GetValue(playerObj)! : 0;
                    string name = nameField != null ? (string)nameField.GetValue(playerObj)! : "Unknown";

                    // Check if this is self
                    bool isHost = false;
                    if (_isHostMethod != null)
                    {
                        try { isHost = (bool)_isHostMethod.Invoke(_network, null)!; } catch { }
                    }
                    string prefix = (id == 1 && !isHost) || (id == 1 && isHost) ? "[Host] " : "";

                    GUI.Label(new Rect(5, itemY, w - 60, 22), $"{prefix}{name}");
                    itemY += 25;
                }

                GUI.EndScrollView();
            }
            else
            {
                GUI.Label(new Rect(x + 20, cy + 40, w - 40, 20), "No players connected");
            }

            cy += 155;

            // World Sync section
            GUI.Box(new Rect(x + 10, cy, w - 20, 60), "World Sync");
            cy += 20;

            // Show world sync status
            string worldStatus = Plugin.WorldSyncInstance?.GetLoadStatus() ?? "";
            if (!string.IsNullOrEmpty(worldStatus))
            {
                GUI.Label(new Rect(x + 15, cy, w - 30, 35), "<color=cyan>" + worldStatus + "</color>");
            }
            else
            {
                GUI.Label(new Rect(x + 15, cy, w - 30, 20), "Save your game before syncing");
            }
            cy += 45;

            // Error display
            if (_loadError != null)
            {
                GUI.Label(new Rect(x + 10, cy, w - 20, 40), "<color=yellow>" + _loadError + "</color>");
            }
        }

        private static string? _libDir;
        private static Assembly? _liteNetLib;

        private bool LoadNetwork()
        {
            if (_network != null) return true;
            if (_triedLoad) return false;
            _triedLoad = true;

            try
            {
                // Network DLLs in AppData
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                _libDir = Path.Combine(appData, "AutonautsMP");
                string networkDll = Path.Combine(_libDir, "AutonautsMP.Network.dll");
                string liteNetDll = Path.Combine(_libDir, "LiteNetLib.dll");

                if (!File.Exists(networkDll) || !File.Exists(liteNetDll))
                {
                    _loadError = "DLLs not found in:\n" + _libDir;
                    return false;
                }

                // Add resolver to help CLR find LiteNetLib
                AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;

                // Load LiteNetLib first
                _liteNetLib = Assembly.LoadFrom(liteNetDll);

                // Load network assembly
                var asm = Assembly.LoadFrom(networkDll);
                var bridgeType = asm.GetType("AutonautsMP.Network.NetworkBridge");

                if (bridgeType == null)
                {
                    _loadError = "NetworkBridge type not found";
                    return false;
                }

                _network = Activator.CreateInstance(bridgeType);
                _hostMethod = bridgeType.GetMethod("StartHost");
                _joinMethod = bridgeType.GetMethod("Connect");
                _disconnectMethod = bridgeType.GetMethod("Disconnect");
                _updateMethod = bridgeType.GetMethod("Update");
                _getStatusMethod = bridgeType.GetMethod("GetStatus");
                _setPlayerNameMethod = bridgeType.GetMethod("SetPlayerName");
                _getPlayersMethod = bridgeType.GetMethod("GetPlayers");
                _getPlayerCountMethod = bridgeType.GetMethod("GetPlayerCount");
                _isHostMethod = bridgeType.GetMethod("IsHost");
                _isConnectedMethod = bridgeType.GetMethod("IsConnected");
                _sendPositionMethod = bridgeType.GetMethod("SendPosition");
                _getLocalPlayerIdMethod = bridgeType.GetMethod("GetLocalPlayerId");
                _sendWorldStateMethod = bridgeType.GetMethod("SendWorldState");

                // Wire up FarmerSync with the network
                if (Plugin.FarmerSyncInstance != null && _sendPositionMethod != null && 
                    _getPlayersMethod != null && _isConnectedMethod != null && _isHostMethod != null)
                {
                    Plugin.FarmerSyncInstance.SetNetwork(_network, _sendPositionMethod, 
                        _getPlayersMethod, _isConnectedMethod, _isHostMethod);
                }

                // Wire up WorldSync with the network
                if (Plugin.WorldSyncInstance != null && _isHostMethod != null)
                {
                    Plugin.WorldSyncInstance.SetNetwork(_network, _sendWorldStateMethod, _isHostMethod);
                }

                _status = "Network loaded";
                return true;
            }
            catch (Exception e)
            {
                _loadError = "Load failed: " + e.Message;
                return false;
            }
        }

        private static Assembly? OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            // Help CLR find LiteNetLib
            if (args.Name.StartsWith("LiteNetLib"))
                return _liteNetLib;

            // Try to load from our lib directory
            if (_libDir == null) return null;
            string name = new AssemblyName(args.Name).Name;
            string path = Path.Combine(_libDir, name + ".dll");
            if (File.Exists(path))
                return Assembly.LoadFrom(path);

            return null;
        }

        // Public accessor for network (for FarmerSync to use)
        public object? GetNetwork() => _network;
        public MethodInfo? GetSendPositionMethod() => _sendPositionMethod;
    }
}
