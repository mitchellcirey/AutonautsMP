using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace AutonautsMP
{
    /// <summary>
    /// AutonautsMP - Multiplayer mod for Autonauts
    /// Phase 1: Mod injection verification and UI scaffolding
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

            // Toggle key
            if (Input.GetKeyDown(ToggleKey.Value))
            {
                _showWindow = !_showWindow;
                Logger.LogInfo(_showWindow ? "Multiplayer UI opened" : "Multiplayer UI closed");
            }
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

            // Status
            GUI.Label(new Rect(10, y, w, 20), $"<color=cyan>Status: {_status}</color>");
            y += 30;

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
                Logger.LogInfo($"Host Game clicked (Port: {_port})");
                _status = "Host clicked - networking not implemented";
            }
            y += 40;

            // Join button
            if (GUI.Button(new Rect(10, y, w, 30), "Join Game"))
            {
                Logger.LogInfo($"Join Game clicked (IP: {_ip}, Port: {_port})");
                _status = "Join clicked - networking not implemented";
            }
            y += 45;

            // Close button
            if (GUI.Button(new Rect(10, _windowRect.height - 40, w, 25), "Close"))
            {
                Logger.LogInfo("Close button clicked");
                _showWindow = false;
            }

            // Make draggable
            GUI.DragWindow(new Rect(0, 0, _windowRect.width, 30));
        }
    }
}
