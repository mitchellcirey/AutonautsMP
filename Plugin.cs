using BepInEx;
using UnityEngine;
using AutonautsMP.Core;
using AutonautsMP.Network;
using AutonautsMP.UI;

namespace AutonautsMP
{
    /// <summary>
    /// AutonautsMP - Multiplayer mod for Autonauts
    /// Full networking implementation with host/join functionality
    /// </summary>
    [BepInPlugin(GUID, NAME, DevSettings.Version)]
    internal class Plugin : BaseUnityPlugin
    {
        public const string GUID = "com.autonautsmp.mod";
        public const string NAME = "AutonautsMP";

        // State
        private static bool _initialized = false;
        private MultiplayerUI _multiplayerUI;

        private void Awake()
        {
            // Initialize debug logger first so we can log everything
            DebugLogger.Initialize(Logger);
            
            // Initialize configuration
            ModConfig.Initialize(Config);
            
            // Initialize user settings (for saved servers, etc.)
            UserSettings.Initialize();
            
            // Initialize TestSyncManager (for network testing)
            TestSyncManager.Initialize();

            // Log startup
            Logger.LogInfo("================================================");
            Logger.LogInfo($"{NAME} v{DevSettings.Version} initializing...");
            Logger.LogInfo("================================================");
            Logger.LogInfo($"Press {ModConfig.ToggleKey} or click 'MP' button to open UI");
            Logger.LogInfo("Mod loaded successfully!");

            _initialized = true;
        }
        
        private void Start()
        {
            // Add the MultiplayerUI component to this GameObject
            _multiplayerUI = gameObject.AddComponent<MultiplayerUI>();
            Logger.LogInfo("MultiplayerUI component added");
        }

        private void Update()
        {
            if (!_initialized) return;

            // Update network manager (poll events)
            NetworkManager.Instance.Update();
        }
        
        private void OnApplicationQuit()
        {
            // Clean up network connection when game closes
            NetworkManager.Instance.Disconnect();
        }
    }
}
