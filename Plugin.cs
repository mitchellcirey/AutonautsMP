using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using AutonautsMP.Network;

namespace AutonautsMP
{
    /// <summary>
    /// AutonautsMP - Multiplayer mod for Autonauts
    /// Full networking implementation with host/join functionality
    /// </summary>
    [BepInPlugin(GUID, NAME, VERSION)]
    internal class Plugin : BaseUnityPlugin
    {
        public const string GUID = "com.autonautsmp.mod";
        public const string NAME = "AutonautsMP";
        public const string VERSION = "1.0.0";

        // Config
        internal static ConfigEntry<KeyCode> ToggleKey;
        internal static ConfigEntry<int> WindowX;
        internal static ConfigEntry<int> WindowY;

        // State
        private static bool _showWindow = false;
        private static bool _initialized = false;
        private string _ip = "127.0.0.1";
        private string _port = "7777";
        private string _status = "Ready";
        private Rect _windowRect;
        private int _windowId;
        
        // Network state tracking for UI
        private ConnectionState _lastNetworkState = ConnectionState.Disconnected;

        private void Awake()
        {
            // Bind config
            ToggleKey = Config.Bind("UI", "ToggleKey", KeyCode.F10, "Key to toggle multiplayer panel");
            WindowX = Config.Bind("UI", "WindowX", 20, "Window X position");
            WindowY = Config.Bind("UI", "WindowY", 20, "Window Y position");

            // Initialize window
            _windowRect = new Rect(WindowX.Value, WindowY.Value, 350, 280);
            _windowId = GetInstanceID();

            // Log startup
            Logger.LogInfo("================================================");
            Logger.LogInfo($"{NAME} v{VERSION} initializing...");
            Logger.LogInfo("================================================");
            Logger.LogInfo($"Press {ToggleKey.Value} or click 'MP' button to open UI");
            Logger.LogInfo("Mod loaded successfully!");

            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized) return;

            // Update network manager (poll events)
            NetworkManager.Instance.Update();
            
            // Update status based on network state
            UpdateNetworkStatus();

            // Toggle key
            if (Input.GetKeyDown(ToggleKey.Value))
            {
                _showWindow = !_showWindow;
                Logger.LogInfo(_showWindow ? "Multiplayer UI opened" : "Multiplayer UI closed");
            }
        }
        
        private void UpdateNetworkStatus()
        {
            var currentState = NetworkManager.Instance.State;
            if (currentState != _lastNetworkState)
            {
                _lastNetworkState = currentState;
                switch (currentState)
                {
                    case ConnectionState.Disconnected:
                        _status = "Disconnected";
                        break;
                    case ConnectionState.Connecting:
                        _status = "Connecting...";
                        break;
                    case ConnectionState.Connected:
                        _status = "Connected to server!";
                        break;
                    case ConnectionState.Hosting:
                        _status = $"Hosting on port {_port}";
                        break;
                }
                Logger.LogInfo($"Network state changed: {currentState}");
            }
            
            // Update player count for host
            if (currentState == ConnectionState.Hosting)
            {
                _status = $"Hosting ({NetworkManager.Instance.ConnectedPlayerCount} players)";
            }
        }
        
        private void OnApplicationQuit()
        {
            // Clean up network connection when game closes
            NetworkManager.Instance.Disconnect();
        }

        private void OnGUI()
        {
            if (!_initialized) return;

            // Always show "Mod Loaded" text in top-left
            GUI.color = Color.green;
            GUI.Label(new Rect(10, 10, 300, 25), "<b>AutonautsMP Loaded Successfully</b>");
            GUI.color = Color.white;

            // Toggle button in top-right
            if (GUI.Button(new Rect(Screen.width - 70, 10, 60, 40), "MP"))
            {
                _showWindow = !_showWindow;
                Logger.LogInfo(_showWindow ? "Multiplayer UI opened" : "Multiplayer UI closed");
            }

            // Main window
            if (_showWindow)
            {
                _windowRect = GUI.Window(_windowId, _windowRect, DrawWindow, "");
            }
        }

        private void DrawWindow(int id)
        {
            float y = 10;
            float w = _windowRect.width - 20;

            // Title
            GUI.Label(new Rect(10, y, w, 30), "<size=18><b>AutonautsMP</b></size>");
            y += 35;

            // Separator
            GUI.Box(new Rect(10, y, w, 2), "");
            y += 10;

            // Status with color based on connection state
            string statusColor = NetworkManager.Instance.IsConnected ? "lime" : "cyan";
            GUI.Label(new Rect(10, y, w, 20), $"<color={statusColor}>Status: {_status}</color>");
            y += 30;

            bool isConnected = NetworkManager.Instance.IsConnected;
            
            if (!isConnected)
            {
                // IP field
                GUI.Label(new Rect(10, y, 80, 20), "IP Address:");
                _ip = GUI.TextField(new Rect(95, y, w - 85, 22), _ip);
                y += 30;

                // Port field
                GUI.Label(new Rect(10, y, 80, 20), "Port:");
                _port = GUI.TextField(new Rect(95, y, 80, 22), _port);
                y += 35;

                // Host button
                if (GUI.Button(new Rect(10, y, w, 30), "Host Game"))
                {
                    OnHostClicked();
                }
                y += 40;

                // Join button
                if (GUI.Button(new Rect(10, y, w, 30), "Join Game"))
                {
                    OnJoinClicked();
                }
                y += 45;
            }
            else
            {
                // Show player count for host
                if (NetworkManager.Instance.IsHost)
                {
                    GUI.Label(new Rect(10, y, w, 20), $"Players connected: {NetworkManager.Instance.ConnectedPlayerCount}");
                    y += 30;
                }
                
                // Disconnect button
                if (GUI.Button(new Rect(10, y, w, 30), "Disconnect"))
                {
                    OnDisconnectClicked();
                }
                y += 45;
            }

            // Close button
            if (GUI.Button(new Rect(10, _windowRect.height - 40, w, 25), "Close"))
            {
                Logger.LogInfo("Close button clicked");
                _showWindow = false;
            }

            // Make draggable
            GUI.DragWindow(new Rect(0, 0, _windowRect.width, 30));
        }
        
        private void OnHostClicked()
        {
            if (!int.TryParse(_port, out int port) || port < 1 || port > 65535)
            {
                _status = "Invalid port number";
                Logger.LogWarning("Invalid port number entered");
                return;
            }
            
            Logger.LogInfo($"Starting host on port {port}...");
            _status = "Starting server...";
            
            if (NetworkManager.Instance.StartHost(port))
            {
                Logger.LogInfo($"Server started successfully on port {port}");
            }
            else
            {
                _status = "Failed to start server";
                Logger.LogError("Failed to start server");
            }
        }
        
        private void OnJoinClicked()
        {
            if (string.IsNullOrWhiteSpace(_ip))
            {
                _status = "Please enter an IP address";
                return;
            }
            
            if (!int.TryParse(_port, out int port) || port < 1 || port > 65535)
            {
                _status = "Invalid port number";
                return;
            }
            
            Logger.LogInfo($"Connecting to {_ip}:{port}...");
            _status = "Connecting...";
            
            if (NetworkManager.Instance.JoinGame(_ip, port))
            {
                Logger.LogInfo($"Connection initiated to {_ip}:{port}");
            }
            else
            {
                _status = "Failed to connect";
                Logger.LogError("Failed to initiate connection");
            }
        }
        
        private void OnDisconnectClicked()
        {
            Logger.LogInfo("Disconnecting...");
            NetworkManager.Instance.Disconnect();
            _status = "Disconnected";
        }
    }
}
