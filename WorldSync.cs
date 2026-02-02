using System;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using UnityEngine;

namespace AutonautsMP
{
    /// <summary>
    /// Handles world state synchronization between host and clients.
    /// On join, the host sends their current world state to the client.
    /// </summary>
    public class WorldSync : MonoBehaviour
    {
        public static WorldSync? Instance { get; private set; }

        // Game save/load system access via reflection
        private Type? _saveLoadManagerType;
        private object? _saveLoadManagerInstance;
        private MethodInfo? _saveWorldMethod;
        private MethodInfo? _loadWorldMethod;

        // World state
        private bool _isLoadingWorld;
        private byte[]? _pendingWorldData;
        private float _loadProgress;
        private string _loadStatus = "";

        // Network reference
        private object? _network;
        private MethodInfo? _sendWorldStateMethod;
        private MethodInfo? _isHostMethod;

        private void Awake()
        {
            Instance = this;
            Plugin.Instance?.Log("WorldSync initialized");
        }

        private void Start()
        {
            // Find the game's save/load system
            FindSaveLoadSystem();
        }

        private void FindSaveLoadSystem()
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name == "Assembly-CSharp")
                    {
                        // Look for SaveLoadManager or similar
                        _saveLoadManagerType = asm.GetType("SaveLoadManager");
                        if (_saveLoadManagerType == null)
                        {
                            // Try other common names
                            _saveLoadManagerType = asm.GetType("GameSave");
                            if (_saveLoadManagerType == null)
                            {
                                _saveLoadManagerType = asm.GetType("WorldManager");
                            }
                        }

                        if (_saveLoadManagerType != null)
                        {
                            Plugin.Instance?.Log("Found save system type: " + _saveLoadManagerType.FullName);
                            
                            // Get instance (might be a singleton)
                            var instanceProp = _saveLoadManagerType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
                            if (instanceProp != null)
                            {
                                _saveLoadManagerInstance = instanceProp.GetValue(null);
                            }

                            // Find save/load methods
                            _saveWorldMethod = _saveLoadManagerType.GetMethod("Save", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            _loadWorldMethod = _saveLoadManagerType.GetMethod("Load", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance?.Log("Error finding save system: " + ex.Message);
            }
        }

        public void SetNetwork(object network, MethodInfo? sendWorldState, MethodInfo isHost)
        {
            _network = network;
            _sendWorldStateMethod = sendWorldState;
            _isHostMethod = isHost;

            // Subscribe to world state received callback
            SubscribeToWorldStateCallback();
        }

        private void SubscribeToWorldStateCallback()
        {
            if (_network == null) return;

            try
            {
                // Get the OnWorldStateReceived event/callback
                var networkType = _network.GetType();
                var callbackField = networkType.GetField("OnWorldStateReceived");
                if (callbackField != null)
                {
                    // Create a delegate for our handler
                    var handler = new Action<byte[]>(OnWorldStateReceived);
                    callbackField.SetValue(_network, handler);
                    Plugin.Instance?.Log("Subscribed to OnWorldStateReceived");
                }
            }
            catch (Exception ex)
            {
                Plugin.Instance?.Log("Error subscribing to world state callback: " + ex.Message);
            }
        }

        /// <summary>
        /// Capture current world state as bytes (for sending to clients)
        /// </summary>
        public byte[]? CaptureWorldState()
        {
            try
            {
                // Method 1: Try to use the game's save system
                if (_saveLoadManagerInstance != null && _saveWorldMethod != null)
                {
                    // This would return the save data
                    // Implementation depends on the actual game API
                }

                // Method 2: Read the most recent auto-save or quick-save
                string savePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low",
                    "Denki Ltd", "Autonauts", "Saves"
                );

                if (Directory.Exists(savePath))
                {
                    // Find the most recent save
                    var saveDirectories = Directory.GetDirectories(savePath);
                    string? mostRecentSave = null;
                    DateTime mostRecentTime = DateTime.MinValue;

                    foreach (var dir in saveDirectories)
                    {
                        var worldFile = Path.Combine(dir, "World.txt");
                        if (File.Exists(worldFile))
                        {
                            var fileTime = File.GetLastWriteTime(worldFile);
                            if (fileTime > mostRecentTime)
                            {
                                mostRecentTime = fileTime;
                                mostRecentSave = worldFile;
                            }
                        }
                    }

                    if (mostRecentSave != null)
                    {
                        Plugin.Instance?.Log("Reading world state from: " + mostRecentSave);
                        string json = File.ReadAllText(mostRecentSave);
                        return Encoding.UTF8.GetBytes(json);
                    }
                }

                Plugin.Instance?.Log("Could not capture world state - no save found");
                return null;
            }
            catch (Exception ex)
            {
                Plugin.Instance?.Log("Error capturing world state: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Send world state to a newly connected client (host only)
        /// </summary>
        public void SendWorldStateToClient()
        {
            if (_network == null || _sendWorldStateMethod == null) return;

            bool isHost = false;
            try
            {
                isHost = _isHostMethod != null && (bool)_isHostMethod.Invoke(_network, null)!;
            }
            catch { }

            if (!isHost) return;

            var worldData = CaptureWorldState();
            if (worldData != null)
            {
                Plugin.Instance?.Log($"Sending world state ({worldData.Length} bytes)...");
                // The NetworkBridge.SendWorldState method needs a peer parameter
                // For now, we'll need to enhance this
            }
        }

        /// <summary>
        /// Called when world state is received from host
        /// </summary>
        private void OnWorldStateReceived(byte[] worldData)
        {
            Plugin.Instance?.Log($"Received world state: {worldData.Length} bytes");
            _pendingWorldData = worldData;
            _isLoadingWorld = true;
            _loadStatus = "World data received, preparing to load...";
        }

        private void Update()
        {
            if (_isLoadingWorld && _pendingWorldData != null)
            {
                // For now, save the world data to a temp file and show instructions
                // In a full implementation, we'd trigger the game's load system
                try
                {
                    string tempPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "Low",
                        "Denki Ltd", "Autonauts", "Saves", "_multiplayer_sync"
                    );
                    
                    Directory.CreateDirectory(tempPath);
                    
                    string worldFile = Path.Combine(tempPath, "World.txt");
                    File.WriteAllBytes(worldFile, _pendingWorldData);

                    // Also create a summary file
                    string summaryFile = Path.Combine(tempPath, "Summary.txt");
                    string summary = "{\"AutonautsSummary\":1,\"Version\":\"140.2\",\"GameOptions\":{\"GameModeName\":\"ModeSettlement\",\"Name\":\"Multiplayer Sync\"},\"DateDay\":0,\"DateTime\":0}";
                    File.WriteAllText(summaryFile, summary);

                    _loadStatus = "World saved to '_multiplayer_sync' save slot!\nLoad it from the main menu to sync.";
                    Plugin.Instance?.Log("World state saved to: " + worldFile);
                }
                catch (Exception ex)
                {
                    _loadStatus = "Error saving world: " + ex.Message;
                    Plugin.Instance?.Log("Error saving world state: " + ex.Message);
                }

                _isLoadingWorld = false;
                _pendingWorldData = null;
            }
        }

        /// <summary>
        /// Get current load status for UI display
        /// </summary>
        public string GetLoadStatus() => _loadStatus;

        /// <summary>
        /// Check if currently loading
        /// </summary>
        public bool IsLoading() => _isLoadingWorld;
    }
}
