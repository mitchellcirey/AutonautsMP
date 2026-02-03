using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using AutonautsMP.Core;
using AutonautsMP.Network;
using AutonautsMP.Patches;

namespace AutonautsMP.Sync
{
    /// <summary>
    /// State of snapshot transfer.
    /// </summary>
    public enum SnapshotState
    {
        Idle,
        PreparingSend,      // Host: preparing snapshot to send
        Sending,            // Host: actively sending chunks
        ReceivingStart,     // Client: waiting for first message
        Receiving,          // Client: receiving chunks
        Loading,            // Client: loading the received snapshot
        Complete,           // Transfer complete
        Error               // Transfer failed
    }

    /// <summary>
    /// Manages world snapshot transfer for client join synchronization.
    /// When a client joins, the host sends their current save state.
    /// </summary>
    public class WorldSnapshotManager
    {
        // Singleton instance
        private static WorldSnapshotManager _instance;
        public static WorldSnapshotManager Instance => _instance ?? (_instance = new WorldSnapshotManager());

        // Chunk size for transfer (8KB to stay under Telepathy's 16KB limit)
        private const int CHUNK_SIZE = 8192;

        // Autonauts save paths (will be discovered at runtime)
        private static string _saveFolderPath;
        private static string _tempSavePath;

        // Host-side: pending transfers per client
        private readonly Dictionary<int, SnapshotSendState> _pendingSends = new Dictionary<int, SnapshotSendState>();
        
        // Client-side: receiving state
        private SnapshotReceiveState _receiveState;

        // Current state
        public SnapshotState State { get; private set; } = SnapshotState.Idle;
        
        // Progress (0-100)
        public float Progress { get; private set; } = 0f;
        
        // Status message for UI
        public string StatusMessage { get; private set; } = "";

        // Events
        public event Action<SnapshotState> OnStateChanged;
        public event Action<float> OnProgressChanged;
        public event Action OnSnapshotLoadComplete;
        public event Action<string> OnSnapshotError;

        // Reflection cache for game's save/load system
        private Type _saveLoadManagerType;
        private MethodInfo _saveMethod;
        private MethodInfo _loadMethod;
        private object _saveLoadManagerInstance;

        private WorldSnapshotManager()
        {
            // Discover save paths
            DiscoverSavePaths();
            
            // Try to find game's save/load system via reflection
            DiscoverSaveLoadManager();

            // Subscribe to network events
            NetworkManager.Instance.OnDataReceived += HandleDataReceived;
            NetworkManager.Instance.OnStateChanged += HandleStateChanged;

            DebugLogger.Info("WorldSnapshotManager initialized");
        }

        /// <summary>
        /// Initialize the WorldSnapshotManager (call once at startup).
        /// </summary>
        public static void Initialize()
        {
            var _ = Instance;
        }

        /// <summary>
        /// Discover Autonauts save folder path.
        /// </summary>
        private void DiscoverSavePaths()
        {
            // Autonauts saves to: %USERPROFILE%\AppData\LocalLow\Denki Ltd\Autonauts\Saves
            string basePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", "Denki Ltd", "Autonauts"
            );

            _saveFolderPath = Path.Combine(basePath, "Saves");
            _tempSavePath = Path.Combine(basePath, "MP_TempSave");

            DebugLogger.Info($"Autonauts base path: {basePath}");
            DebugLogger.Info($"Save folder path: {_saveFolderPath}");
            DebugLogger.Info($"Temp save path: {_tempSavePath}");
            
            // Check if paths exist
            DebugLogger.Info($"Base path exists: {Directory.Exists(basePath)}");
            DebugLogger.Info($"Save folder exists: {Directory.Exists(_saveFolderPath)}");

            // Ensure temp directory exists
            if (!Directory.Exists(_tempSavePath))
            {
                try
                {
                    Directory.CreateDirectory(_tempSavePath);
                }
                catch (Exception ex)
                {
                    DebugLogger.Error($"Failed to create temp save directory: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Try to find the game's SaveLoadManager via reflection.
        /// </summary>
        private void DiscoverSaveLoadManager()
        {
            try
            {
                // Search through loaded assemblies for SaveLoadManager
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    // Skip system assemblies
                    string name = assembly.GetName().Name;
                    if (name.StartsWith("System") || name.StartsWith("Unity") || 
                        name.StartsWith("mscorlib") || name.StartsWith("Mono"))
                        continue;

                    try
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            if (type.Name == "SaveLoadManager" || type.Name == "SaveManager")
                            {
                                _saveLoadManagerType = type;
                                DebugLogger.Info($"Found SaveLoadManager: {_saveLoadManagerType.FullName}");
                                
                                // Try to find singleton instance
                                var instanceProp = _saveLoadManagerType.GetProperty("Instance", 
                                    BindingFlags.Public | BindingFlags.Static);
                                if (instanceProp != null)
                                {
                                    _saveLoadManagerInstance = instanceProp.GetValue(null);
                                }
                                
                                // Find Save and Load methods
                                _saveMethod = _saveLoadManagerType.GetMethod("Save", 
                                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                                _loadMethod = _saveLoadManagerType.GetMethod("Load", 
                                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                                
                                DebugLogger.Info($"SaveMethod found: {_saveMethod != null}, LoadMethod found: {_loadMethod != null}");
                                return;
                            }
                        }
                    }
                    catch { /* Ignore assembly search errors */ }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Warning($"Failed to discover SaveLoadManager: {ex.Message}");
            }
        }

        /// <summary>
        /// Called by host to send snapshot to a newly connected client.
        /// </summary>
        public void SendSnapshotToClient(int clientId)
        {
            if (!NetworkManager.Instance.IsHost)
            {
                DebugLogger.Warning("SendSnapshotToClient called but not host");
                return;
            }

            DebugLogger.Info($"Preparing snapshot for client {clientId}");
            SetState(SnapshotState.PreparingSend);
            StatusMessage = "Preparing world snapshot...";

            try
            {
                // Get current save data
                byte[] saveData = GetCurrentSaveData();
                
                if (saveData == null || saveData.Length == 0)
                {
                    DebugLogger.Error("Failed to get save data");
                    SendSnapshotError(clientId, "Failed to prepare world snapshot");
                    return;
                }

                // Use simple RLE-like compression (or no compression for compatibility)
                byte[] compressedData = SimpleCompress(saveData);
                DebugLogger.Info($"Save data: {saveData.Length} bytes, compressed: {compressedData.Length} bytes " +
                    $"({(float)compressedData.Length / saveData.Length * 100:F1}% of original)");

                // Calculate checksum of uncompressed data
                uint checksum = CalculateCRC32(saveData);

                // Chunk the compressed data
                int chunkCount = (compressedData.Length + CHUNK_SIZE - 1) / CHUNK_SIZE;
                
                // Create send state
                var sendState = new SnapshotSendState
                {
                    ClientId = clientId,
                    CompressedData = compressedData,
                    Checksum = checksum,
                    TotalChunks = chunkCount,
                    CurrentChunk = 0,
                    SaveName = GetCurrentSaveName()
                };
                
                _pendingSends[clientId] = sendState;

                // Send start message
                var startPacket = NetworkMessages.BuildSnapshotStart(
                    compressedData.Length, 
                    chunkCount, 
                    sendState.SaveName
                );
                NetworkManager.Instance.SendTo(clientId, startPacket);
                
                DebugLogger.Info($"Sent SnapshotStart: {compressedData.Length} bytes, {chunkCount} chunks");
                
                SetState(SnapshotState.Sending);
                StatusMessage = $"Sending snapshot to client...";
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Failed to prepare snapshot: {ex}");
                SendSnapshotError(clientId, $"Failed to prepare snapshot: {ex.Message}");
            }
        }

        /// <summary>
        /// Update method - called every frame to process chunk sending.
        /// </summary>
        public void Update()
        {
            // Process pending sends (host side)
            if (NetworkManager.Instance.IsHost && _pendingSends.Count > 0)
            {
                ProcessPendingSends();
            }
        }

        /// <summary>
        /// Process chunk sends for all pending transfers.
        /// </summary>
        private void ProcessPendingSends()
        {
            // Send multiple chunks per frame for faster transfer
            const int CHUNKS_PER_FRAME = 5;
            
            // Create a list of keys to iterate (to avoid modifying while iterating)
            var keys = new List<int>(_pendingSends.Keys);
            
            foreach (var clientId in keys)
            {
                if (!_pendingSends.TryGetValue(clientId, out var sendState))
                    continue;
                    
                for (int i = 0; i < CHUNKS_PER_FRAME && sendState.CurrentChunk < sendState.TotalChunks; i++)
                {
                    SendNextChunk(sendState);
                }

                // Update progress
                Progress = (float)sendState.CurrentChunk / sendState.TotalChunks * 100f;
                OnProgressChanged?.Invoke(Progress);

                // Check if complete
                if (sendState.CurrentChunk >= sendState.TotalChunks)
                {
                    // Send complete message
                    var completePacket = NetworkMessages.BuildSnapshotComplete(sendState.Checksum);
                    NetworkManager.Instance.SendTo(sendState.ClientId, completePacket);
                    
                    DebugLogger.Info($"Snapshot transfer complete to client {sendState.ClientId}");
                    _pendingSends.Remove(clientId);
                    
                    if (_pendingSends.Count == 0)
                    {
                        SetState(SnapshotState.Complete);
                        StatusMessage = "Snapshot sent successfully";
                    }
                }
            }
        }

        /// <summary>
        /// Send next chunk to client.
        /// </summary>
        private void SendNextChunk(SnapshotSendState sendState)
        {
            int offset = sendState.CurrentChunk * CHUNK_SIZE;
            int length = Math.Min(CHUNK_SIZE, sendState.CompressedData.Length - offset);
            
            byte[] chunkData = new byte[length];
            Array.Copy(sendState.CompressedData, offset, chunkData, 0, length);
            
            var chunkPacket = NetworkMessages.BuildSnapshotChunk(sendState.CurrentChunk, chunkData);
            NetworkManager.Instance.SendTo(sendState.ClientId, chunkPacket);
            
            sendState.CurrentChunk++;
        }

        /// <summary>
        /// Handle incoming network data.
        /// </summary>
        private void HandleDataReceived(int senderId, byte[] data)
        {
            if (data == null || data.Length < 1)
                return;

            var msgType = NetworkMessages.ReadType(data);

            switch (msgType)
            {
                case MessageType.SnapshotStart:
                    HandleSnapshotStart(data);
                    break;
                case MessageType.SnapshotChunk:
                    HandleSnapshotChunk(data);
                    break;
                case MessageType.SnapshotComplete:
                    HandleSnapshotComplete(data);
                    break;
                case MessageType.SnapshotAck:
                    HandleSnapshotAck(senderId, data);
                    break;
                case MessageType.SnapshotError:
                    HandleSnapshotErrorMessage(data);
                    break;
            }
        }

        /// <summary>
        /// Client handles snapshot start message.
        /// </summary>
        private void HandleSnapshotStart(byte[] data)
        {
            if (NetworkManager.Instance.IsHost)
                return;

            var startData = SnapshotStartData.Read(data);
            DebugLogger.Info($"Receiving snapshot: {startData.TotalSize} bytes, {startData.ChunkCount} chunks, save: {startData.SaveName}");

            _receiveState = new SnapshotReceiveState
            {
                TotalSize = startData.TotalSize,
                TotalChunks = startData.ChunkCount,
                SaveName = startData.SaveName,
                ReceivedChunks = new byte[startData.ChunkCount][],
                ChunksReceived = 0
            };

            SetState(SnapshotState.Receiving);
            StatusMessage = "Receiving world snapshot...";
            Progress = 0f;
            OnProgressChanged?.Invoke(Progress);
        }

        /// <summary>
        /// Client handles snapshot chunk message.
        /// </summary>
        private void HandleSnapshotChunk(byte[] data)
        {
            if (NetworkManager.Instance.IsHost || _receiveState == null)
                return;

            var chunkData = SnapshotChunkData.Read(data);
            
            if (chunkData.ChunkIndex >= 0 && chunkData.ChunkIndex < _receiveState.ReceivedChunks.Length)
            {
                _receiveState.ReceivedChunks[chunkData.ChunkIndex] = chunkData.Data;
                _receiveState.ChunksReceived++;
                
                Progress = (float)_receiveState.ChunksReceived / _receiveState.TotalChunks * 100f;
                StatusMessage = $"Receiving snapshot... {Progress:F0}%";
                OnProgressChanged?.Invoke(Progress);
            }
        }

        /// <summary>
        /// Client handles snapshot complete message.
        /// </summary>
        private void HandleSnapshotComplete(byte[] data)
        {
            if (NetworkManager.Instance.IsHost || _receiveState == null)
                return;

            var completeData = SnapshotCompleteData.Read(data);
            DebugLogger.Info($"Snapshot transfer complete, verifying checksum...");

            SetState(SnapshotState.Loading);
            StatusMessage = "Loading world...";

            try
            {
                // Reassemble chunks
                byte[] compressedData = ReassembleChunks(_receiveState.ReceivedChunks);
                
                // Decompress
                byte[] saveData = SimpleDecompress(compressedData);
                DebugLogger.Info($"Decompressed snapshot: {saveData.Length} bytes");

                // Verify checksum
                uint calculatedChecksum = CalculateCRC32(saveData);
                if (calculatedChecksum != completeData.Checksum)
                {
                    throw new Exception($"Checksum mismatch: expected {completeData.Checksum}, got {calculatedChecksum}");
                }
                DebugLogger.Info("Checksum verified");

                // Write to temp save location
                string savePath = WriteTempSave(saveData, _receiveState.SaveName);
                
                // Load the save
                LoadSnapshot(savePath);

                // Send acknowledgment
                var ackPacket = NetworkMessages.BuildSnapshotAck(true);
                NetworkManager.Instance.SendToServer(ackPacket);

                SetState(SnapshotState.Complete);
                StatusMessage = "World loaded successfully";
                OnSnapshotLoadComplete?.Invoke();
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Failed to load snapshot: {ex}");
                SetState(SnapshotState.Error);
                StatusMessage = $"Failed: {ex.Message}";
                OnSnapshotError?.Invoke(ex.Message);

                // Send error ack
                var ackPacket = NetworkMessages.BuildSnapshotAck(false);
                NetworkManager.Instance.SendToServer(ackPacket);
            }
            finally
            {
                _receiveState = null;
            }
        }

        /// <summary>
        /// Host handles snapshot acknowledgment from client.
        /// </summary>
        private void HandleSnapshotAck(int clientId, byte[] data)
        {
            if (!NetworkManager.Instance.IsHost)
                return;

            var ackData = SnapshotAckData.Read(data);
            
            if (ackData.Success)
            {
                DebugLogger.Info($"Client {clientId} successfully loaded snapshot");
            }
            else
            {
                DebugLogger.Warning($"Client {clientId} failed to load snapshot");
            }
        }

        /// <summary>
        /// Handle snapshot error message.
        /// </summary>
        private void HandleSnapshotErrorMessage(byte[] data)
        {
            var errorData = SnapshotErrorData.Read(data);
            DebugLogger.Error($"Snapshot error: {errorData.ErrorMessage}");
            
            SetState(SnapshotState.Error);
            StatusMessage = $"Error: {errorData.ErrorMessage}";
            OnSnapshotError?.Invoke(errorData.ErrorMessage);
            
            _receiveState = null;
        }

        /// <summary>
        /// Send error message to client.
        /// </summary>
        private void SendSnapshotError(int clientId, string message)
        {
            var errorPacket = NetworkMessages.BuildSnapshotError(message);
            NetworkManager.Instance.SendTo(clientId, errorPacket);
            
            SetState(SnapshotState.Error);
            StatusMessage = message;
        }

        /// <summary>
        /// Handle connection state changes.
        /// </summary>
        private void HandleStateChanged(ConnectionState state)
        {
            if (state == ConnectionState.Disconnected)
            {
                // Clean up any pending transfers
                _pendingSends.Clear();
                _receiveState = null;
                SetState(SnapshotState.Idle);
                Progress = 0f;
                StatusMessage = "";
            }
        }

        /// <summary>
        /// Get current save data from the game.
        /// </summary>
        private byte[] GetCurrentSaveData()
        {
            DebugLogger.Info("GetCurrentSaveData: Starting to find save data...");
            
            try
            {
                // Check if save folder exists
                if (!Directory.Exists(_saveFolderPath))
                {
                    DebugLogger.Error($"Save folder does not exist: {_saveFolderPath}");
                    return null;
                }
                
                // Try to trigger a fresh save first
                DebugLogger.Info("Attempting to trigger game save...");
                TriggerGameSave();
                
                // Find save files (World.txt files in save subfolders)
                var saveFiles = GetSaveFiles(_saveFolderPath);
                
                if (saveFiles.Count > 0)
                {
                    string mostRecentSave = saveFiles[0];
                    string saveName = Path.GetFileName(Path.GetDirectoryName(mostRecentSave));
                    long fileSize = new FileInfo(mostRecentSave).Length;
                    
                    DebugLogger.Info($"Reading save file: {saveName}/World.txt ({fileSize} bytes)");
                    byte[] data = File.ReadAllBytes(mostRecentSave);
                    DebugLogger.Info($"Successfully read {data.Length} bytes from save file");
                    return data;
                }
                
                DebugLogger.Warning("No save files found in save folder!");
                
                // Fallback: Try to get save data via reflection
                if (_saveMethod != null && _saveLoadManagerInstance != null)
                {
                    DebugLogger.Info("Trying to get save data via reflection...");
                    var result = _saveMethod.Invoke(_saveLoadManagerInstance, null);
                    if (result is byte[] bytes)
                    {
                        DebugLogger.Info($"Got {bytes.Length} bytes from SaveMethod reflection");
                        return bytes;
                    }
                    if (result is string str)
                    {
                        DebugLogger.Info($"Got string result from SaveMethod ({str.Length} chars)");
                        return System.Text.Encoding.UTF8.GetBytes(str);
                    }
                }

                DebugLogger.Error("FAILED: Could not find any save data to send!");
                DebugLogger.Error("Please ensure the host has at least one saved game.");
                return null;  // Return null instead of placeholder - this is a real error
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Error getting save data: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Get list of World.txt save files from save folders, sorted by modification time (newest first).
        /// Autonauts saves are stored as folders containing World.txt, Summary.txt, and Thumbnail.jpg.
        /// </summary>
        private List<string> GetSaveFiles(string folderPath)
        {
            var worldFiles = new List<string>();
            
            if (!Directory.Exists(folderPath))
            {
                DebugLogger.Warning($"Save folder does not exist: {folderPath}");
                return worldFiles;
            }

            DebugLogger.Info($"Scanning for save folders in: {folderPath}");
            
            // Autonauts saves are folders containing World.txt
            var saveFolders = Directory.GetDirectories(folderPath);
            DebugLogger.Info($"Found {saveFolders.Length} save folders");
            
            foreach (var saveFolder in saveFolders)
            {
                string saveName = Path.GetFileName(saveFolder);
                string worldFile = Path.Combine(saveFolder, "World.txt");
                
                if (File.Exists(worldFile))
                {
                    DebugLogger.Info($"  Found save: {saveName} -> {worldFile}");
                    worldFiles.Add(worldFile);
                }
                else
                {
                    DebugLogger.Debug($"  Folder {saveName} has no World.txt");
                }
            }

            DebugLogger.Info($"Found {worldFiles.Count} valid save files (World.txt)");
            
            // Sort by modification time, newest first
            worldFiles.Sort((a, b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));
            
            if (worldFiles.Count > 0)
            {
                string mostRecent = worldFiles[0];
                string saveName = Path.GetFileName(Path.GetDirectoryName(mostRecent));
                DebugLogger.Info($"Most recent save: {saveName} (modified: {File.GetLastWriteTime(mostRecent)})");
            }
            
            return worldFiles;
        }

        /// <summary>
        /// Try to trigger the game to save.
        /// </summary>
        private void TriggerGameSave()
        {
            try
            {
                if (_saveMethod != null)
                {
                    if (_saveMethod.IsStatic)
                    {
                        _saveMethod.Invoke(null, null);
                    }
                    else if (_saveLoadManagerInstance != null)
                    {
                        _saveMethod.Invoke(_saveLoadManagerInstance, null);
                    }
                    DebugLogger.Info("Triggered game save");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Warning($"Failed to trigger game save: {ex.Message}");
            }
        }

        /// <summary>
        /// Get current save name.
        /// </summary>
        private string GetCurrentSaveName()
        {
            try
            {
                var saveFiles = GetSaveFiles(_saveFolderPath);
                if (saveFiles.Count > 0)
                {
                    // saveFiles[0] is like "Saves/SaveName/World.txt", get the folder name
                    string worldPath = saveFiles[0];
                    string saveFolder = Path.GetDirectoryName(worldPath);
                    return Path.GetFileName(saveFolder);
                }
            }
            catch { }
            
            return "MP_Snapshot";
        }

        /// <summary>
        /// Create a valid JSON Summary.txt content that Autonauts expects.
        /// </summary>
        private string CreateSummaryJson(string saveName)
        {
            // Autonauts expects Summary.txt to be valid JSON with GameOptions
            // Field names must match exactly what GameOptions.Save() writes:
            // - GameModeName (string)
            // - GameSize (int)
            // - RandomObjectsEnabled (bool)
            // - BadgeUnlocksEnabled (bool)
            // - RecordingEnabled (bool)
            // - TutorialEnabled (bool)
            // - BotRechargingEnabled (bool)
            // - BotLimitEnabled (bool)
            // - Seed (int)
            // - Name (string)
            
            var sb = new System.Text.StringBuilder();
            sb.Append("{");
            sb.Append("\"Version\":\"140.2\",");
            sb.Append("\"External\":0,");
            sb.Append("\"GameOptions\":{");
            sb.Append("\"GameModeName\":\"ModeCampaign\",");
            sb.Append("\"GameSize\":1,");  // 1 = Medium
            sb.Append("\"RandomObjectsEnabled\":true,");
            sb.Append("\"BadgeUnlocksEnabled\":true,");
            sb.Append("\"RecordingEnabled\":false,");
            sb.Append("\"TutorialEnabled\":false,");
            sb.Append("\"BotRechargingEnabled\":true,");
            sb.Append("\"BotLimitEnabled\":false,");
            sb.Append("\"Seed\":12345,");
            sb.Append("\"Name\":\"" + saveName.Replace("\"", "\\\"") + "\"");
            sb.Append("},");
            sb.Append("\"DateDay\":1,");
            sb.Append("\"DateTime\":0");
            sb.Append("}");
            
            return sb.ToString();
        }

        /// <summary>
        /// Write save data to temp location.
        /// </summary>
        private string WriteTempSave(byte[] saveData, string saveName)
        {
            // Create proper save folder structure: Saves/SaveName/World.txt
            // Autonauts expects saves as folders containing World.txt, Summary.txt, Thumbnail.jpg
            
            // Use the actual Saves folder, not a temp folder
            string saveFolder = Path.Combine(_saveFolderPath, $"MP_{saveName}");
            
            // Clean up any existing MP save folder
            if (Directory.Exists(saveFolder))
            {
                try
                {
                    Directory.Delete(saveFolder, true);
                }
                catch (Exception ex)
                {
                    DebugLogger.Warning($"Failed to clean up existing save folder: {ex.Message}");
                }
            }
            
            // Create save folder
            Directory.CreateDirectory(saveFolder);

            // Write World.txt (the main save data)
            string worldFilePath = Path.Combine(saveFolder, "World.txt");
            File.WriteAllBytes(worldFilePath, saveData);
            DebugLogger.Info($"Wrote World.txt to: {worldFilePath}");
            
            // Create a valid JSON Summary.txt so the game recognizes this as a valid save
            // The game expects: Version, External, GameOptions, DateDay, DateTime
            string summaryPath = Path.Combine(saveFolder, "Summary.txt");
            string summaryJson = CreateSummaryJson(saveName);
            File.WriteAllText(summaryPath, summaryJson);
            DebugLogger.Info($"Wrote Summary.txt to: {summaryPath}");
            
            // Return the save folder path (not the World.txt path)
            return saveFolder;
        }

        /// <summary>
        /// Load the snapshot save file.
        /// </summary>
        private void LoadSnapshot(string saveFolder)
        {
            // saveFolder is like "C:\...\Saves\MP_SaveName"
            string saveName = Path.GetFileName(saveFolder);
            
            DebugLogger.Info($"Loading snapshot save: {saveName}");
            DebugLogger.Info($"Save folder path: {saveFolder}");

            try
            {
                // First try via SaveLoadPatch which has better method detection
                if (SaveLoadPatch.TriggerLoad(saveName))
                {
                    DebugLogger.Info($"Load triggered via SaveLoadPatch (save name: {saveName})");
                    return;
                }
                
                // Try with full folder path
                if (SaveLoadPatch.TriggerLoad(saveFolder))
                {
                    DebugLogger.Info($"Load triggered via SaveLoadPatch (folder path)");
                    return;
                }
                
                // Try via our discovered _loadMethod
                if (_loadMethod != null)
                {
                    DebugLogger.Info("Trying direct reflection load...");
                    
                    // Try save name first (most games use just the name)
                    try
                    {
                        if (_loadMethod.IsStatic)
                        {
                            _loadMethod.Invoke(null, new object[] { saveName });
                        }
                        else if (_saveLoadManagerInstance != null)
                        {
                            _loadMethod.Invoke(_saveLoadManagerInstance, new object[] { saveName });
                        }
                        DebugLogger.Info($"Triggered game load with save name: {saveName}");
                        return;
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Warning($"Load with save name failed: {ex.Message}");
                    }
                }

                DebugLogger.Warning($"Could not trigger automatic load - save folder created at: {saveFolder}");
                DebugLogger.Warning("You may need to manually load the save from the game menu.");
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Failed to load snapshot: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Reassemble chunks into complete data.
        /// </summary>
        private byte[] ReassembleChunks(byte[][] chunks)
        {
            // Calculate total size
            int totalSize = 0;
            foreach (var chunk in chunks)
            {
                if (chunk != null)
                {
                    totalSize += chunk.Length;
                }
            }
            
            byte[] result = new byte[totalSize];
            
            int offset = 0;
            foreach (var chunk in chunks)
            {
                if (chunk != null)
                {
                    Array.Copy(chunk, 0, result, offset, chunk.Length);
                    offset += chunk.Length;
                }
            }
            
            return result;
        }

        /// <summary>
        /// Simple compression using length prefix (no actual compression for max compatibility).
        /// Format: [OriginalLength:4][Data]
        /// </summary>
        private byte[] SimpleCompress(byte[] data)
        {
            // For maximum compatibility, just add a length prefix
            // This allows future compression algorithms to be added
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // Write original length
                writer.Write(data.Length);
                // Write data as-is
                writer.Write(data);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Simple decompression - reads length prefix and data.
        /// </summary>
        private byte[] SimpleDecompress(byte[] compressedData)
        {
            using (var ms = new MemoryStream(compressedData))
            using (var reader = new BinaryReader(ms))
            {
                // Read original length
                int originalLength = reader.ReadInt32();
                // Read data
                return reader.ReadBytes(originalLength);
            }
        }

        /// <summary>
        /// Calculate CRC32 checksum.
        /// </summary>
        private uint CalculateCRC32(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            
            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    crc = (crc >> 1) ^ (0xEDB88320 & ~((crc & 1) - 1));
                }
            }
            
            return ~crc;
        }

        /// <summary>
        /// Set state and fire event.
        /// </summary>
        private void SetState(SnapshotState newState)
        {
            if (State != newState)
            {
                State = newState;
                DebugLogger.Info($"Snapshot state changed: {newState}");
                OnStateChanged?.Invoke(newState);
            }
        }

        /// <summary>
        /// Check if currently receiving a snapshot.
        /// </summary>
        public bool IsReceiving => State == SnapshotState.Receiving || State == SnapshotState.ReceivingStart;

        /// <summary>
        /// Check if currently loading a snapshot.
        /// </summary>
        public bool IsLoading => State == SnapshotState.Loading;

        /// <summary>
        /// Check if transfer is in progress.
        /// </summary>
        public bool IsTransferring => State == SnapshotState.Sending || IsReceiving;
    }

    /// <summary>
    /// Host-side state for sending snapshot to a client.
    /// </summary>
    internal class SnapshotSendState
    {
        public int ClientId;
        public byte[] CompressedData;
        public uint Checksum;
        public int TotalChunks;
        public int CurrentChunk;
        public string SaveName;
    }

    /// <summary>
    /// Client-side state for receiving snapshot.
    /// </summary>
    internal class SnapshotReceiveState
    {
        public long TotalSize;
        public int TotalChunks;
        public string SaveName;
        public byte[][] ReceivedChunks;
        public int ChunksReceived;
    }
}
