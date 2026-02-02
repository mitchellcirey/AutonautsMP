using BepInEx.Configuration;
using UnityEngine;

namespace AutonautsMP.Core
{
    /// <summary>
    /// Configuration manager for AutonautsMP mod settings.
    /// Uses BepInEx ConfigEntry for persistent config storage.
    /// </summary>
    internal static class ModConfig
    {
        // Config entries
        private static ConfigEntry<KeyCode>? _toggleKey;
        private static ConfigEntry<int>? _windowX;
        private static ConfigEntry<int>? _windowY;
        private static ConfigEntry<int>? _windowWidth;
        private static ConfigEntry<int>? _windowHeight;
        private static ConfigEntry<string>? _defaultIP;
        private static ConfigEntry<int>? _defaultPort;
        
        /// <summary>
        /// Key to toggle the multiplayer UI window.
        /// </summary>
        public static KeyCode ToggleKey => _toggleKey?.Value ?? KeyCode.F10;
        
        /// <summary>
        /// X position of the UI window.
        /// </summary>
        public static int WindowX => _windowX?.Value ?? 20;
        
        /// <summary>
        /// Y position of the UI window.
        /// </summary>
        public static int WindowY => _windowY?.Value ?? 20;
        
        /// <summary>
        /// Width of the UI window.
        /// </summary>
        public static int WindowWidth => _windowWidth?.Value ?? 350;
        
        /// <summary>
        /// Height of the UI window.
        /// </summary>
        public static int WindowHeight => _windowHeight?.Value ?? 300;
        
        /// <summary>
        /// Default IP address for joining games.
        /// </summary>
        public static string DefaultIP => _defaultIP?.Value ?? "127.0.0.1";
        
        /// <summary>
        /// Default port for hosting/joining games.
        /// </summary>
        public static int DefaultPort => _defaultPort?.Value ?? 7777;
        
        /// <summary>
        /// Initialize configuration with BepInEx config file.
        /// Must be called once during plugin startup.
        /// </summary>
        public static void Initialize(ConfigFile config)
        {
            DebugLogger.Info("Loading configuration...");
            
            // UI Settings
            _toggleKey = config.Bind(
                "UI",
                "ToggleKey",
                KeyCode.F10,
                "Key to toggle the multiplayer panel (F10 by default)"
            );
            
            _windowX = config.Bind(
                "UI",
                "WindowX",
                20,
                "X position of the multiplayer window"
            );
            
            _windowY = config.Bind(
                "UI",
                "WindowY",
                20,
                "Y position of the multiplayer window"
            );
            
            _windowWidth = config.Bind(
                "UI",
                "WindowWidth",
                350,
                "Width of the multiplayer window"
            );
            
            _windowHeight = config.Bind(
                "UI",
                "WindowHeight",
                300,
                "Height of the multiplayer window"
            );
            
            // Network Settings (for future use)
            _defaultIP = config.Bind(
                "Network",
                "DefaultIP",
                "127.0.0.1",
                "Default IP address when joining a game"
            );
            
            _defaultPort = config.Bind(
                "Network",
                "DefaultPort",
                7777,
                "Default port for hosting/joining games"
            );
            
            DebugLogger.Info("Configuration loaded successfully");
        }
        
        /// <summary>
        /// Save the current window position to config.
        /// Call this when the window is moved/resized.
        /// </summary>
        public static void SaveWindowPosition(int x, int y)
        {
            if (_windowX != null) _windowX.Value = x;
            if (_windowY != null) _windowY.Value = y;
        }
    }
}
