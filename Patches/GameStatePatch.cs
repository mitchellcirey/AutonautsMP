using System;
using System.Reflection;
using HarmonyLib;
using AutonautsMP.Core;

namespace AutonautsMP.Patches
{
    /// <summary>
    /// Harmony patches to detect game state changes in Autonauts.
    /// Patches game loading/unloading methods to track when player is in-game.
    /// </summary>
    [HarmonyPatch]
    internal static class GameStatePatch
    {
        private static bool _patchAttempted = false;
        private static bool _patchSucceeded = false;

        /// <summary>
        /// Attempts to apply game-specific patches at runtime.
        /// Called after Harmony is initialized.
        /// </summary>
        public static void TryApplyPatches(Harmony harmony)
        {
            if (_patchAttempted) return;
            _patchAttempted = true;

            DebugLogger.Info("Attempting to apply game state patches...");

            // Try to find and patch GameStateManager (common pattern)
            _patchSucceeded = TryPatchGameStateManager(harmony);

            if (!_patchSucceeded)
            {
                // Try SaveLoadManager as fallback
                _patchSucceeded = TryPatchSaveLoadManager(harmony);
            }

            if (!_patchSucceeded)
            {
                DebugLogger.Warning("Could not find game-specific classes to patch. Using scene-based detection.");
            }
            else
            {
                DebugLogger.Info("Game state patches applied successfully!");
            }
        }

        private static bool TryPatchGameStateManager(Harmony harmony)
        {
            try
            {
                // Look for GameStateManager in game assembly
                var gameStateManagerType = FindType("GameStateManager");
                if (gameStateManagerType == null)
                {
                    DebugLogger.Debug("GameStateManager type not found");
                    return false;
                }

                DebugLogger.Info($"Found GameStateManager: {gameStateManagerType.FullName}");

                // Try to patch SetState method
                var setStateMethod = gameStateManagerType.GetMethod("SetState", 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                
                if (setStateMethod != null)
                {
                    var postfix = typeof(GameStatePatch).GetMethod(nameof(GameStateManager_SetState_Postfix),
                        BindingFlags.NonPublic | BindingFlags.Static);
                    harmony.Patch(setStateMethod, postfix: new HarmonyMethod(postfix));
                    DebugLogger.Info("Patched GameStateManager.SetState");
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                DebugLogger.Debug($"Failed to patch GameStateManager: {ex.Message}");
                return false;
            }
        }

        private static bool TryPatchSaveLoadManager(Harmony harmony)
        {
            try
            {
                // Look for SaveLoadManager
                var saveLoadType = FindType("SaveLoadManager");
                if (saveLoadType == null)
                {
                    DebugLogger.Debug("SaveLoadManager type not found");
                    return false;
                }

                DebugLogger.Info($"Found SaveLoadManager: {saveLoadType.FullName}");
                bool patched = false;

                // Try to patch Load method
                var loadMethod = saveLoadType.GetMethod("Load",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                if (loadMethod != null)
                {
                    var postfix = typeof(GameStatePatch).GetMethod(nameof(SaveLoad_Load_Postfix),
                        BindingFlags.NonPublic | BindingFlags.Static);
                    harmony.Patch(loadMethod, postfix: new HarmonyMethod(postfix));
                    DebugLogger.Info("Patched SaveLoadManager.Load");
                    patched = true;
                }

                return patched;
            }
            catch (Exception ex)
            {
                DebugLogger.Debug($"Failed to patch SaveLoadManager: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Find a type by name across all loaded assemblies.
        /// </summary>
        private static Type FindType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    // Skip system assemblies
                    if (assembly.FullName.StartsWith("System") || 
                        assembly.FullName.StartsWith("mscorlib") ||
                        assembly.FullName.StartsWith("Unity"))
                        continue;

                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.Name == typeName)
                        {
                            return type;
                        }
                    }
                }
                catch
                {
                    // Ignore assemblies that can't be inspected
                }
            }
            return null;
        }

        // Postfix handlers for dynamic patches
        private static void GameStateManager_SetState_Postfix(object __instance)
        {
            try
            {
                // Try to get the current state from the instance
                var stateField = __instance.GetType().GetField("m_State", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var stateProperty = __instance.GetType().GetProperty("State", 
                    BindingFlags.Public | BindingFlags.Instance);

                object state = stateField?.GetValue(__instance) ?? stateProperty?.GetValue(__instance);
                
                if (state != null)
                {
                    string stateName = state.ToString();
                    DebugLogger.Debug($"GameStateManager state changed: {stateName}");
                    
                    // Common state names that indicate being in-game
                    bool inGame = stateName.Contains("Play") || 
                                  stateName.Contains("Game") || 
                                  stateName.Contains("Normal") ||
                                  stateName.Contains("Running");
                    
                    GameStateDetector.SetInGame(inGame);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Debug($"Error reading game state: {ex.Message}");
            }
        }

        private static void SaveLoad_Load_Postfix()
        {
            DebugLogger.Info("Save file loaded - setting in-game state");
            GameStateDetector.SetInGame(true);
        }
    }
}
