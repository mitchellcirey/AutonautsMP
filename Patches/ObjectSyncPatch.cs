using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using AutonautsMP.Core;
using AutonautsMP.Network;
using AutonautsMP.Sync;

namespace AutonautsMP.Patches
{
    /// <summary>
    /// Harmony patches for detecting object changes in the game.
    /// Hooks into ModManager.CheckCustomCallback to intercept pickup/drop events.
    /// </summary>
    internal static class ObjectSyncPatch
    {
        // Discovered types
        private static Type _modManagerType;
        private static Type _callbackTypesEnum;
        
        // Callback type values
        private static object _holdablePickedUp;
        private static object _holdableDroppedOnGround;
        private static object _converterCreateItem;
        private static object _addedToConverter;

        /// <summary>
        /// Try to apply patches to object-related methods.
        /// </summary>
        public static bool TryApplyPatches(Harmony harmony)
        {
            bool patched = false;
            
            try
            {
                DebugLogger.Info("Attempting to apply object sync patches...");
                
                // Find ModManager class
                _modManagerType = FindType("ModManager");
                if (_modManagerType != null)
                {
                    DebugLogger.Info($"Found ModManager: {_modManagerType.FullName}");
                    
                    // Find the CallbackTypes enum
                    _callbackTypesEnum = _modManagerType.GetNestedType("CallbackTypes");
                    if (_callbackTypesEnum != null)
                    {
                        DebugLogger.Info($"Found CallbackTypes enum");
                        
                        // Get the enum values we care about
                        try
                        {
                            _holdablePickedUp = Enum.Parse(_callbackTypesEnum, "HoldablePickedUp");
                            _holdableDroppedOnGround = Enum.Parse(_callbackTypesEnum, "HoldableDroppedOnGround");
                            _converterCreateItem = Enum.Parse(_callbackTypesEnum, "ConverterCreateItem");
                            _addedToConverter = Enum.Parse(_callbackTypesEnum, "AddedToConverter");
                            
                            DebugLogger.Info($"Parsed callback types: PickedUp={_holdablePickedUp}, Dropped={_holdableDroppedOnGround}");
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.Warning($"Failed to parse callback types: {ex.Message}");
                        }
                    }
                    
                    // Patch CheckCustomCallback method
                    patched = PatchCheckCustomCallback(harmony, _modManagerType);
                }
                else
                {
                    DebugLogger.Warning("ModManager not found - trying alternative approach");
                    patched = TryAlternativePatches(harmony);
                }
                
                if (patched)
                {
                    DebugLogger.Info("Object sync patches applied successfully!");
                }
                else
                {
                    DebugLogger.Warning("No object sync patches could be applied - object sync may be limited");
                }
                
                return patched;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Failed to apply object sync patches: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Patch ModManager.CheckCustomCallback to intercept object events.
        /// </summary>
        private static bool PatchCheckCustomCallback(Harmony harmony, Type modManagerType)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            
            try
            {
                // Find CheckCustomCallback method
                var method = modManagerType.GetMethod("CheckCustomCallback", flags);
                if (method != null)
                {
                    var prefix = typeof(ObjectSyncPatch).GetMethod(nameof(CheckCustomCallback_Prefix),
                        BindingFlags.NonPublic | BindingFlags.Static);
                    harmony.Patch(method, prefix: new HarmonyMethod(prefix));
                    DebugLogger.Info($"Patched ModManager.CheckCustomCallback");
                    return true;
                }
                else
                {
                    DebugLogger.Warning("CheckCustomCallback method not found");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Error patching CheckCustomCallback: {ex.Message}");
            }
            
            return false;
        }

        /// <summary>
        /// Try alternative patches if ModManager approach fails.
        /// </summary>
        private static bool TryAlternativePatches(Harmony harmony)
        {
            bool patched = false;
            
            // Try to patch FarmerCarry.AddCarry directly
            var farmerCarryType = FindType("FarmerCarry");
            if (farmerCarryType != null)
            {
                patched |= PatchFarmerCarry(harmony, farmerCarryType);
            }
            
            // Try to patch FarmerStateDropping
            var farmerStateDroppingType = FindType("FarmerStateDropping");
            if (farmerStateDroppingType != null)
            {
                patched |= PatchFarmerStateDropping(harmony, farmerStateDroppingType);
            }
            
            return patched;
        }

        /// <summary>
        /// Patch FarmerCarry.AddCarry for pickup detection.
        /// </summary>
        private static bool PatchFarmerCarry(Harmony harmony, Type farmerCarryType)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            
            try
            {
                var method = farmerCarryType.GetMethod("AddCarry", flags, null, 
                    new Type[] { FindType("Holdable") }, null);
                    
                if (method != null)
                {
                    var postfix = typeof(ObjectSyncPatch).GetMethod(nameof(AddCarry_Postfix),
                        BindingFlags.NonPublic | BindingFlags.Static);
                    harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                    DebugLogger.Info("Patched FarmerCarry.AddCarry");
                    return true;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Debug($"Error patching FarmerCarry: {ex.Message}");
            }
            
            return false;
        }

        /// <summary>
        /// Patch FarmerStateDropping for drop detection.
        /// </summary>
        private static bool PatchFarmerStateDropping(Harmony harmony, Type stateType)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            
            try
            {
                // Look for DoAction or similar method
                var methods = new[] { "DoAction", "EndState", "DoAnimationAction" };
                foreach (var methodName in methods)
                {
                    var method = stateType.GetMethod(methodName, flags);
                    if (method != null)
                    {
                        var postfix = typeof(ObjectSyncPatch).GetMethod(nameof(StateDropping_Postfix),
                            BindingFlags.NonPublic | BindingFlags.Static);
                        harmony.Patch(method, postfix: new HarmonyMethod(postfix));
                        DebugLogger.Info($"Patched FarmerStateDropping.{methodName}");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Debug($"Error patching FarmerStateDropping: {ex.Message}");
            }
            
            return false;
        }

        /// <summary>
        /// Find a type by name across all assemblies.
        /// </summary>
        private static Type FindType(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.FullName.StartsWith("System") ||
                    assembly.FullName.StartsWith("Unity") ||
                    assembly.FullName.StartsWith("mscorlib") ||
                    assembly.FullName.StartsWith("Mono"))
                    continue;

                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.Name == typeName)
                            return type;
                    }
                }
                catch { }
            }
            return null;
        }

        #region Harmony Patches

        /// <summary>
        /// Prefix for ModManager.CheckCustomCallback - intercepts all mod callbacks.
        /// Parameters match: CheckCustomCallback(CallbackTypes Type, ObjectType Obj, TileCoord Location, int ObjectUniqueID, int PlayerUniqueID)
        /// </summary>
        private static void CheckCustomCallback_Prefix(
            object Type,           // CallbackTypes enum
            object Obj,            // ObjectType enum  
            object Location,       // TileCoord struct
            int ObjectUniqueID,
            int PlayerUniqueID)
        {
            try
            {
                if (!NetworkManager.Instance.IsConnected)
                    return;

                // Check if this is a pickup or drop event
                bool isPickup = _holdablePickedUp != null && Type.Equals(_holdablePickedUp);
                bool isDrop = _holdableDroppedOnGround != null && Type.Equals(_holdableDroppedOnGround);
                
                if (!isPickup && !isDrop)
                    return;

                // Extract position from TileCoord
                int tileX = 0, tileY = 0;
                if (Location != null)
                {
                    var locType = Location.GetType();
                    var xField = locType.GetField("x");
                    var yField = locType.GetField("y");
                    if (xField != null) tileX = (int)xField.GetValue(Location);
                    if (yField != null) tileY = (int)yField.GetValue(Location);
                }

                // Get object type name
                string objectTypeName = Obj?.ToString() ?? "Unknown";

                if (isPickup)
                {
                    DebugLogger.Info($"[ObjectSync] PICKUP: {objectTypeName} (UID:{ObjectUniqueID}) by Player {PlayerUniqueID} at ({tileX},{tileY})");
                    ObjectSyncManager.Instance.QueueObjectChange(
                        ObjectSyncManager.ChangeType.PickedUp,
                        ObjectUniqueID,
                        objectTypeName,
                        tileX,
                        tileY
                    );
                }
                else if (isDrop)
                {
                    DebugLogger.Info($"[ObjectSync] DROP: {objectTypeName} (UID:{ObjectUniqueID}) by Player {PlayerUniqueID} at ({tileX},{tileY})");
                    ObjectSyncManager.Instance.QueueObjectChange(
                        ObjectSyncManager.ChangeType.Dropped,
                        ObjectUniqueID,
                        objectTypeName,
                        tileX,
                        tileY
                    );
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Debug($"Error in CheckCustomCallback_Prefix: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix for FarmerCarry.AddCarry - fallback pickup detection.
        /// </summary>
        private static void AddCarry_Postfix(object __instance, object CarryObject)
        {
            try
            {
                if (!NetworkManager.Instance.IsConnected || CarryObject == null)
                    return;

                var objectInfo = ExtractObjectInfo(CarryObject);
                if (objectInfo != null)
                {
                    DebugLogger.Info($"[ObjectSync] PICKUP (AddCarry): {objectInfo.Item2} (UID:{objectInfo.Item1})");
                    ObjectSyncManager.Instance.QueueObjectChange(
                        ObjectSyncManager.ChangeType.PickedUp,
                        objectInfo.Item1,
                        objectInfo.Item2,
                        objectInfo.Item3,
                        objectInfo.Item4
                    );
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Debug($"Error in AddCarry_Postfix: {ex.Message}");
            }
        }

        /// <summary>
        /// Postfix for FarmerStateDropping - fallback drop detection.
        /// </summary>
        private static void StateDropping_Postfix(object __instance)
        {
            // This is a fallback - CheckCustomCallback should handle most cases
        }

        #endregion

        /// <summary>
        /// Extract object info (UID, type, tileX, tileY) from a game object.
        /// </summary>
        private static Tuple<int, string, int, int> ExtractObjectInfo(object obj)
        {
            if (obj == null)
                return null;

            try
            {
                var type = obj.GetType();
                var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                
                // Get UID
                int uid = 0;
                var uidField = type.GetField("m_UniqueID", flags);
                if (uidField != null)
                    uid = (int)uidField.GetValue(obj);
                
                // Get type name
                string typeName = "Unknown";
                var typeIdField = type.GetField("m_TypeIdentifier", flags);
                if (typeIdField != null)
                {
                    var typeVal = typeIdField.GetValue(obj);
                    if (typeVal != null)
                        typeName = typeVal.ToString();
                }
                
                // Get position
                int tileX = 0, tileY = 0;
                var tileCoordField = type.GetField("m_TileCoord", flags);
                if (tileCoordField != null)
                {
                    var coord = tileCoordField.GetValue(obj);
                    if (coord != null)
                    {
                        var coordType = coord.GetType();
                        var xField = coordType.GetField("x");
                        var yField = coordType.GetField("y");
                        if (xField != null) tileX = (int)xField.GetValue(coord);
                        if (yField != null) tileY = (int)yField.GetValue(coord);
                    }
                }
                
                return new Tuple<int, string, int, int>(uid, typeName, tileX, tileY);
            }
            catch (Exception ex)
            {
                DebugLogger.Debug($"Error extracting object info: {ex.Message}");
                return null;
            }
        }
    }
}
