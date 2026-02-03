using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using AutonautsMP.Core;
using AutonautsMP.Sync;

namespace AutonautsMP.Patches
{
    /// <summary>
    /// Harmony patches for Autonauts' save/load system.
    /// Used to intercept saves for snapshot transfer and trigger loads for received snapshots.
    /// </summary>
    [HarmonyPatch]
    internal static class SaveLoadPatch
    {
        private static bool _patchAttempted = false;
        private static bool _patchSucceeded = false;

        // Flag to indicate we're doing a snapshot load
        private static bool _isSnapshotLoad = false;
        private static string _snapshotLoadPath = null;

        // Cached references for invoking save/load
        private static Type _saveLoadManagerType;
        private static object _saveLoadManagerInstance;
        private static MethodInfo _loadMethod;
        private static MethodInfo _saveMethod;
        private static MethodInfo _getCurrentSavePathMethod;

        /// <summary>
        /// Whether patches were successfully applied.
        /// </summary>
        public static bool PatchSucceeded => _patchSucceeded;

        /// <summary>
        /// The current save file path (if discovered).
        /// </summary>
        public static string CurrentSavePath { get; private set; }

        /// <summary>
        /// Event fired when a save completes.
        /// </summary>
        public static event Action<string> OnSaveCompleted;

        /// <summary>
        /// Event fired when a load completes.
        /// </summary>
        public static event Action<string> OnLoadCompleted;

        /// <summary>
        /// Attempts to apply save/load patches at runtime.
        /// Called after Harmony is initialized.
        /// </summary>
        public static void TryApplyPatches(Harmony harmony)
        {
            if (_patchAttempted) return;
            _patchAttempted = true;

            DebugLogger.Info("Attempting to apply save/load patches...");

            // Try to find SaveLoadManager
            _patchSucceeded = TryPatchSaveLoadManager(harmony);

            if (!_patchSucceeded)
            {
                // Try alternate names
                _patchSucceeded = TryPatchSaveManager(harmony);
            }

            if (!_patchSucceeded)
            {
                DebugLogger.Warning("Could not find save/load classes to patch. Snapshot loading will use file-based fallback.");
            }
            else
            {
                DebugLogger.Info("Save/load patches applied successfully!");
            }
        }

        private static bool TryPatchSaveLoadManager(Harmony harmony)
        {
            try
            {
                _saveLoadManagerType = FindType("SaveLoadManager");
                if (_saveLoadManagerType == null)
                {
                    DebugLogger.Debug("SaveLoadManager type not found");
                    return false;
                }

                DebugLogger.Info($"Found SaveLoadManager: {_saveLoadManagerType.FullName}");

                // Get singleton instance
                var instanceProp = _saveLoadManagerType.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                if (instanceProp != null)
                {
                    _saveLoadManagerInstance = instanceProp.GetValue(null);
                    DebugLogger.Info("Got SaveLoadManager instance");
                }

                bool patched = false;

                // Patch Save method
                _saveMethod = FindMethod(_saveLoadManagerType, "Save", "DoSave", "SaveGame", "SaveWorld");
                if (_saveMethod != null)
                {
                    var postfix = typeof(SaveLoadPatch).GetMethod(nameof(Save_Postfix),
                        BindingFlags.NonPublic | BindingFlags.Static);
                    harmony.Patch(_saveMethod, postfix: new HarmonyMethod(postfix));
                    DebugLogger.Info($"Patched {_saveMethod.Name}");
                    patched = true;
                }

                // Patch Load method
                _loadMethod = FindMethod(_saveLoadManagerType, "Load", "DoLoad", "LoadGame", "LoadWorld", "LoadSave");
                if (_loadMethod != null)
                {
                    var prefix = typeof(SaveLoadPatch).GetMethod(nameof(Load_Prefix),
                        BindingFlags.NonPublic | BindingFlags.Static);
                    var postfix = typeof(SaveLoadPatch).GetMethod(nameof(Load_Postfix),
                        BindingFlags.NonPublic | BindingFlags.Static);
                    harmony.Patch(_loadMethod, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
                    DebugLogger.Info($"Patched {_loadMethod.Name}");
                    patched = true;
                }

                // Try to find method to get current save path
                _getCurrentSavePathMethod = FindMethod(_saveLoadManagerType, 
                    "GetCurrentSavePath", "GetSavePath", "CurrentSavePath", "GetSaveFilePath");

                return patched;
            }
            catch (Exception ex)
            {
                DebugLogger.Debug($"Failed to patch SaveLoadManager: {ex.Message}");
                return false;
            }
        }

        private static bool TryPatchSaveManager(Harmony harmony)
        {
            try
            {
                // Try alternate class names
                var alternateNames = new[] { "SaveManager", "GameSaveManager", "WorldSaveManager", "SaveSystem" };
                
                foreach (var name in alternateNames)
                {
                    _saveLoadManagerType = FindType(name);
                    if (_saveLoadManagerType != null)
                    {
                        DebugLogger.Info($"Found alternate save manager: {_saveLoadManagerType.FullName}");
                        
                        bool patched = false;
                        
                        _saveMethod = FindMethod(_saveLoadManagerType, "Save", "DoSave", "SaveGame");
                        if (_saveMethod != null)
                        {
                            var postfix = typeof(SaveLoadPatch).GetMethod(nameof(Save_Postfix),
                                BindingFlags.NonPublic | BindingFlags.Static);
                            harmony.Patch(_saveMethod, postfix: new HarmonyMethod(postfix));
                            patched = true;
                        }

                        _loadMethod = FindMethod(_saveLoadManagerType, "Load", "DoLoad", "LoadGame");
                        if (_loadMethod != null)
                        {
                            var prefix = typeof(SaveLoadPatch).GetMethod(nameof(Load_Prefix),
                                BindingFlags.NonPublic | BindingFlags.Static);
                            var postfix = typeof(SaveLoadPatch).GetMethod(nameof(Load_Postfix),
                                BindingFlags.NonPublic | BindingFlags.Static);
                            harmony.Patch(_loadMethod, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
                            patched = true;
                        }

                        if (patched) return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                DebugLogger.Debug($"Failed to patch alternate save manager: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Find a type by name across loaded assemblies.
        /// </summary>
        private static Type FindType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    string asmName = assembly.GetName().Name;
                    if (asmName.StartsWith("System") || asmName.StartsWith("mscorlib") ||
                        asmName.StartsWith("Unity") || asmName.StartsWith("Mono"))
                        continue;

                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.Name == typeName)
                        {
                            return type;
                        }
                    }
                }
                catch { }
            }
            return null;
        }

        /// <summary>
        /// Find a method by trying multiple possible names.
        /// </summary>
        private static MethodInfo FindMethod(Type type, params string[] possibleNames)
        {
            foreach (var name in possibleNames)
            {
                var method = type.GetMethod(name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (method != null)
                {
                    return method;
                }
            }
            return null;
        }

        /// <summary>
        /// Trigger a save via the game's save system.
        /// </summary>
        public static bool TriggerSave()
        {
            if (_saveMethod == null)
            {
                DebugLogger.Warning("Cannot trigger save - method not found");
                return false;
            }

            try
            {
                if (_saveMethod.IsStatic)
                {
                    _saveMethod.Invoke(null, null);
                }
                else if (_saveLoadManagerInstance != null)
                {
                    _saveMethod.Invoke(_saveLoadManagerInstance, null);
                }
                else
                {
                    DebugLogger.Warning("Cannot trigger save - no instance available");
                    return false;
                }

                DebugLogger.Info("Triggered game save");
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Failed to trigger save: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Trigger loading a save file via the game's load system.
        /// </summary>
        public static bool TriggerLoad(string savePath)
        {
            if (_loadMethod == null)
            {
                DebugLogger.Warning("Cannot trigger load - method not found");
                return false;
            }

            try
            {
                _isSnapshotLoad = true;
                _snapshotLoadPath = savePath;

                var parameters = _loadMethod.GetParameters();
                object[] args = null;

                // Try to determine what arguments the load method expects
                if (parameters.Length > 0)
                {
                    args = new object[parameters.Length];
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var param = parameters[i];
                        if (param.ParameterType == typeof(string))
                        {
                            args[i] = savePath;
                        }
                        else if (param.ParameterType == typeof(int))
                        {
                            args[i] = 0; // Default slot
                        }
                        else if (param.HasDefaultValue)
                        {
                            args[i] = param.DefaultValue;
                        }
                        else
                        {
                            args[i] = null;
                        }
                    }
                }

                if (_loadMethod.IsStatic)
                {
                    _loadMethod.Invoke(null, args);
                }
                else if (_saveLoadManagerInstance != null)
                {
                    _loadMethod.Invoke(_saveLoadManagerInstance, args);
                }
                else
                {
                    DebugLogger.Warning("Cannot trigger load - no instance available");
                    return false;
                }

                DebugLogger.Info($"Triggered game load: {savePath}");
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Failed to trigger load: {ex.Message}");
                return false;
            }
            finally
            {
                _isSnapshotLoad = false;
                _snapshotLoadPath = null;
            }
        }

        /// <summary>
        /// Get the current save file path.
        /// </summary>
        public static string GetCurrentSavePath()
        {
            if (CurrentSavePath != null)
            {
                return CurrentSavePath;
            }

            try
            {
                if (_getCurrentSavePathMethod != null)
                {
                    if (_getCurrentSavePathMethod.IsStatic)
                    {
                        return _getCurrentSavePathMethod.Invoke(null, null) as string;
                    }
                    else if (_saveLoadManagerInstance != null)
                    {
                        return _getCurrentSavePathMethod.Invoke(_saveLoadManagerInstance, null) as string;
                    }
                }

                // Fallback: find most recent save file
                string savesFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "AppData", "LocalLow", "Denki Ltd", "Autonauts", "Saves"
                );

                if (Directory.Exists(savesFolder))
                {
                    var saveFiles = new List<string>();
                    foreach (var f in Directory.GetFiles(savesFolder, "*.*"))
                    {
                        if (f.EndsWith(".txt") || f.EndsWith(".sav") || f.EndsWith(".json"))
                        {
                            saveFiles.Add(f);
                        }
                    }
                    
                    // Sort by modification time, newest first
                    saveFiles.Sort((a, b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));

                    if (saveFiles.Count > 0)
                    {
                        return saveFiles[0];
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Debug($"Failed to get current save path: {ex.Message}");
            }

            return null;
        }

        #region Harmony Patch Methods

        private static void Save_Postfix()
        {
            try
            {
                string savePath = GetCurrentSavePath();
                CurrentSavePath = savePath;
                DebugLogger.Info($"Save completed: {savePath}");
                OnSaveCompleted?.Invoke(savePath);
            }
            catch (Exception ex)
            {
                DebugLogger.Debug($"Error in Save_Postfix: {ex.Message}");
            }
        }

        private static void Load_Prefix()
        {
            try
            {
                if (_isSnapshotLoad)
                {
                    DebugLogger.Info($"Loading multiplayer snapshot: {_snapshotLoadPath}");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Debug($"Error in Load_Prefix: {ex.Message}");
            }
        }

        private static void Load_Postfix()
        {
            try
            {
                string savePath = _isSnapshotLoad ? _snapshotLoadPath : GetCurrentSavePath();
                CurrentSavePath = savePath;
                DebugLogger.Info($"Load completed: {savePath}");
                OnLoadCompleted?.Invoke(savePath);

                // Update game state detector
                GameStateDetector.SetInGame(true);
            }
            catch (Exception ex)
            {
                DebugLogger.Debug($"Error in Load_Postfix: {ex.Message}");
            }
        }

        #endregion

    }
}
