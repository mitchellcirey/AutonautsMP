using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using AutonautsMP.Core;
using AutonautsMP.Network;

namespace AutonautsMP.Sync
{
    /// <summary>
    /// Represents a synced object in the game world.
    /// </summary>
    public class SyncedObject
    {
        public int UID { get; set; }
        public string ObjectType { get; set; }
        public int TileX { get; set; }
        public int TileY { get; set; }
        public int Rotation { get; set; }
        public bool IsHeld { get; set; }
        public int HeldByPlayer { get; set; }
        public float LastUpdateTime { get; set; }
    }

    /// <summary>
    /// Manages object synchronization between host and clients.
    /// Uses host-authoritative model - host broadcasts object changes to all clients.
    /// </summary>
    public class ObjectSyncManager
    {
        // Singleton instance
        private static ObjectSyncManager _instance;
        public static ObjectSyncManager Instance => _instance ?? (_instance = new ObjectSyncManager());

        // Object tracking
        private readonly Dictionary<int, SyncedObject> _syncedObjects = new Dictionary<int, SyncedObject>();
        
        // Pending changes list (for batching)
        private readonly List<ObjectChange> _pendingChanges = new List<ObjectChange>();
        private float _lastSyncTime = 0f;
        private const float SYNC_INTERVAL = 0.05f; // 20 updates per second
        
        // Game reflection - Autonauts specific
        private Type _objectTypeListType;
        private object _objectTypeListInstance;
        private MethodInfo _getObjectFromUIDMethod;
        private Type _tileCoordType;
        private ConstructorInfo _tileCoordCtor;
        
        // Local player UID (to filter out our own events)
        private int _localPlayerUID = -1;
        
        // Statistics
        public int ObjectsSynced => _syncedObjects.Count;
        public int PendingChanges => _pendingChanges.Count;

        /// <summary>
        /// Types of object changes that can occur.
        /// </summary>
        public enum ChangeType
        {
            Created,
            Destroyed,
            Moved,
            StateChanged,
            PickedUp,
            Dropped
        }

        /// <summary>
        /// Represents a pending object change to sync.
        /// </summary>
        public class ObjectChange
        {
            public ChangeType Type { get; set; }
            public int UID { get; set; }
            public string ObjectType { get; set; }
            public int TileX { get; set; }
            public int TileY { get; set; }
            public int Rotation { get; set; }
            public int PlayerId { get; set; }
            public byte[] StateData { get; set; }
        }

        private ObjectSyncManager()
        {
            // Subscribe to network events
            NetworkManager.Instance.OnDataReceived += HandleDataReceived;
            NetworkManager.Instance.OnStateChanged += HandleStateChanged;

            // Discover game's object management system
            DiscoverAutonautsTypes();

            DebugLogger.Info("ObjectSyncManager initialized");
        }

        /// <summary>
        /// Initialize the ObjectSyncManager (call once at startup).
        /// </summary>
        public static void Initialize()
        {
            var _ = Instance;
        }

        /// <summary>
        /// Set the local player's UID (to filter out our own events from sync).
        /// </summary>
        public void SetLocalPlayerUID(int uid)
        {
            _localPlayerUID = uid;
            DebugLogger.Info($"ObjectSync: Local player UID set to {uid}");
        }

        /// <summary>
        /// Discover Autonauts-specific types via reflection.
        /// </summary>
        private void DiscoverAutonautsTypes()
        {
            try
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
                            if (type.Name == "ObjectTypeList")
                            {
                                _objectTypeListType = type;
                                
                                // Get singleton instance
                                var instanceProp = type.GetProperty("Instance", 
                                    BindingFlags.Public | BindingFlags.Static);
                                if (instanceProp != null)
                                {
                                    _objectTypeListInstance = instanceProp.GetValue(null);
                                }
                                
                                // Get GetObjectFromUniqueID method
                                _getObjectFromUIDMethod = type.GetMethod("GetObjectFromUniqueID",
                                    BindingFlags.Public | BindingFlags.Instance);
                                    
                                if (_getObjectFromUIDMethod != null)
                                {
                                    DebugLogger.Info("Found ObjectTypeList.GetObjectFromUniqueID");
                                }
                            }
                            else if (type.Name == "TileCoord")
                            {
                                _tileCoordType = type;
                                _tileCoordCtor = type.GetConstructor(new Type[] { typeof(int), typeof(int) });
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Error discovering Autonauts types: {ex.Message}");
            }
        }

        /// <summary>
        /// Get a game object by its unique ID using Autonauts' ObjectTypeList.
        /// </summary>
        private object GetGameObjectByUID(int uid)
        {
            try
            {
                // Always get fresh instance - it may change after save/load
                if (_objectTypeListType == null)
                {
                    DebugLogger.Warning($"[ObjectSync] ObjectTypeList type not found");
                    return null;
                }
                
                var instanceProp = _objectTypeListType.GetProperty("Instance", 
                    BindingFlags.Public | BindingFlags.Static);
                if (instanceProp == null)
                {
                    DebugLogger.Warning($"[ObjectSync] ObjectTypeList.Instance property not found");
                    return null;
                }
                
                var instance = instanceProp.GetValue(null);
                if (instance == null)
                {
                    DebugLogger.Warning($"[ObjectSync] ObjectTypeList.Instance is null");
                    return null;
                }
                
                if (_getObjectFromUIDMethod == null)
                {
                    _getObjectFromUIDMethod = _objectTypeListType.GetMethod("GetObjectFromUniqueID",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (_getObjectFromUIDMethod == null)
                    {
                        DebugLogger.Warning($"[ObjectSync] GetObjectFromUniqueID method not found");
                        return null;
                    }
                }
                
                // Call GetObjectFromUniqueID(uid, false) - false = don't error if not found
                var result = _getObjectFromUIDMethod.Invoke(instance, new object[] { uid, false });
                
                if (result == null)
                {
                    DebugLogger.Debug($"[ObjectSync] ObjectTypeList returned null for UID:{uid}");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[ObjectSync] Error getting object by UID: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Update - call every frame.
        /// </summary>
        public void Update()
        {
            if (!NetworkManager.Instance.IsConnected)
                return;

            float currentTime = Time.realtimeSinceStartup;
            
            // Process pending changes at interval
            if (currentTime - _lastSyncTime >= SYNC_INTERVAL)
            {
                _lastSyncTime = currentTime;
                
                if (NetworkManager.Instance.IsHost)
                {
                    ProcessPendingChanges();
                }
            }
        }

        /// <summary>
        /// Queue an object change for syncing (called when object changes are detected).
        /// </summary>
        public void QueueObjectChange(ChangeType type, int uid, string objectType, int tileX, int tileY, int rotation = 0, byte[] stateData = null)
        {
            if (!NetworkManager.Instance.IsConnected)
                return;

            var change = new ObjectChange
            {
                Type = type,
                UID = uid,
                ObjectType = objectType,
                TileX = tileX,
                TileY = tileY,
                Rotation = rotation,
                PlayerId = NetworkManager.Instance.IsHost ? -1 : 0,
                StateData = stateData
            };

            DebugLogger.Info($"[ObjectSync] Queuing {type}: {objectType} (UID:{uid}) at ({tileX},{tileY})");

            if (NetworkManager.Instance.IsHost)
            {
                // Host queues for broadcast
                _pendingChanges.Add(change);
                DebugLogger.Debug($"[ObjectSync] Host queued change, pending count: {_pendingChanges.Count}");
            }
            else
            {
                // Client sends to host for validation
                DebugLogger.Debug($"[ObjectSync] Client sending change to host...");
                SendObjectChangeToHost(change);
            }
        }

        /// <summary>
        /// Process queued changes and broadcast to clients (host only).
        /// </summary>
        private void ProcessPendingChanges()
        {
            if (_pendingChanges.Count == 0)
                return;

            const int MAX_CHANGES_PER_FRAME = 10;
            int toProcess = Math.Min(_pendingChanges.Count, MAX_CHANGES_PER_FRAME);

            DebugLogger.Debug($"[ObjectSync] Processing {toProcess} pending changes...");

            for (int i = 0; i < toProcess; i++)
            {
                var change = _pendingChanges[0];
                _pendingChanges.RemoveAt(0);
                BroadcastObjectChange(change);
            }
        }

        /// <summary>
        /// Send object change to host (client only).
        /// </summary>
        private void SendObjectChangeToHost(ObjectChange change)
        {
            byte[] packet = BuildObjectChangePacket(change);
            DebugLogger.Debug($"[ObjectSync] Sending {packet.Length} byte packet to host");
            NetworkManager.Instance.SendToServer(packet);
        }

        /// <summary>
        /// Broadcast object change to all clients (host only).
        /// </summary>
        private void BroadcastObjectChange(ObjectChange change)
        {
            byte[] packet = BuildObjectChangePacket(change);
            DebugLogger.Debug($"[ObjectSync] Broadcasting {packet.Length} byte packet to all clients");
            NetworkManager.Instance.Broadcast(packet);

            // Update local tracking
            UpdateTrackedObject(change);
        }

        /// <summary>
        /// Build network packet for object change.
        /// </summary>
        private byte[] BuildObjectChangePacket(ObjectChange change)
        {
            using (var ms = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(ms))
            {
                // Determine message type based on change type
                MessageType msgType;
                switch (change.Type)
                {
                    case ChangeType.Created:
                    case ChangeType.Dropped:
                        msgType = MessageType.ObjectPlaced;
                        break;
                    case ChangeType.Destroyed:
                    case ChangeType.PickedUp:
                        msgType = MessageType.ObjectRemoved;
                        break;
                    default:
                        msgType = MessageType.EntityUpdate;
                        break;
                }

                writer.Write((byte)msgType);
                writer.Write(change.UID);
                writer.Write(change.ObjectType ?? "");
                writer.Write(change.TileX);
                writer.Write(change.TileY);
                writer.Write(change.Rotation);
                writer.Write(change.PlayerId);
                writer.Write((byte)change.Type);
                
                // Write state data if present
                if (change.StateData != null && change.StateData.Length > 0)
                {
                    writer.Write(change.StateData.Length);
                    writer.Write(change.StateData);
                }
                else
                {
                    writer.Write(0);
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Parse object change from network packet.
        /// </summary>
        private ObjectChange ParseObjectChangePacket(byte[] data)
        {
            using (var ms = new System.IO.MemoryStream(data))
            using (var reader = new System.IO.BinaryReader(ms))
            {
                reader.ReadByte(); // Skip message type
                
                var change = new ObjectChange
                {
                    UID = reader.ReadInt32(),
                    ObjectType = reader.ReadString(),
                    TileX = reader.ReadInt32(),
                    TileY = reader.ReadInt32(),
                    Rotation = reader.ReadInt32(),
                    PlayerId = reader.ReadInt32(),
                    Type = (ChangeType)reader.ReadByte()
                };

                int stateLength = reader.ReadInt32();
                if (stateLength > 0)
                {
                    change.StateData = reader.ReadBytes(stateLength);
                }

                return change;
            }
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
                case MessageType.ObjectPlaced:
                case MessageType.ObjectRemoved:
                case MessageType.EntityUpdate:
                    DebugLogger.Info($"[ObjectSync] Received {msgType} message from {senderId}, {data.Length} bytes");
                    HandleObjectChangeMessage(senderId, data);
                    break;
            }
        }

        /// <summary>
        /// Handle object change message.
        /// </summary>
        private void HandleObjectChangeMessage(int senderId, byte[] data)
        {
            try
            {
                var change = ParseObjectChangePacket(data);
                DebugLogger.Info($"[ObjectSync] Received {change.Type}: {change.ObjectType} (UID:{change.UID}) at ({change.TileX},{change.TileY})");

                if (NetworkManager.Instance.IsHost)
                {
                    // Host validates and rebroadcasts
                    if (ValidateObjectChange(change))
                    {
                        // Apply locally and broadcast to other clients
                        ApplyObjectChange(change);
                        
                        // Rebroadcast to other clients
                        int rebroadcastCount = 0;
                        foreach (var clientId in NetworkManager.Instance.ConnectedClientIds)
                        {
                            if (clientId != senderId)
                            {
                                NetworkManager.Instance.SendTo(clientId, data);
                                rebroadcastCount++;
                            }
                        }
                        DebugLogger.Debug($"[ObjectSync] Host rebroadcasted to {rebroadcastCount} clients");
                    }
                    else
                    {
                        DebugLogger.Warning($"Rejected object change from client {senderId}: UID={change.UID}");
                    }
                }
                else
                {
                    // Client applies changes from host
                    ApplyObjectChange(change);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Error handling object change: {ex.Message}");
            }
        }

        /// <summary>
        /// Validate an object change request from a client (host only).
        /// </summary>
        private bool ValidateObjectChange(ObjectChange change)
        {
            // Basic validation - accept all changes for now
            return true;
        }

        /// <summary>
        /// Apply an object change to the local game state.
        /// </summary>
        private void ApplyObjectChange(ObjectChange change)
        {
            try
            {
                DebugLogger.Info($"[ObjectSync] Applying {change.Type} for {change.ObjectType} (UID:{change.UID})");
                
                switch (change.Type)
                {
                    case ChangeType.Created:
                    case ChangeType.Dropped:
                        ApplyObjectDrop(change);
                        break;
                        
                    case ChangeType.Destroyed:
                    case ChangeType.PickedUp:
                        ApplyObjectPickup(change);
                        break;
                        
                    case ChangeType.Moved:
                    case ChangeType.StateChanged:
                        ApplyObjectUpdate(change);
                        break;
                }

                // Update tracking
                UpdateTrackedObject(change);
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Error applying object change: {ex.Message}");
            }
        }

        /// <summary>
        /// Debug method to check if UID list exists and has entries.
        /// </summary>
        private void DiagnoseUIDLookup(int targetUID, string objectType)
        {
            try
            {
                if (_objectTypeListType == null)
                    return;
                    
                // Try to access the static m_UniqueIDList field
                var uidListField = _objectTypeListType.GetField("m_UniqueIDList",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    
                if (uidListField != null)
                {
                    var uidList = uidListField.GetValue(null);
                    if (uidList != null)
                    {
                        // It's a Dictionary<int, BaseClass>
                        var countProp = uidList.GetType().GetProperty("Count");
                        if (countProp != null)
                        {
                            int count = (int)countProp.GetValue(uidList);
                            DebugLogger.Info($"[ObjectSync] m_UniqueIDList has {count} entries");
                            
                            // Try to check if our target UID exists
                            var containsKeyMethod = uidList.GetType().GetMethod("ContainsKey");
                            if (containsKeyMethod != null)
                            {
                                bool hasKey = (bool)containsKeyMethod.Invoke(uidList, new object[] { targetUID });
                                DebugLogger.Info($"[ObjectSync] UID {targetUID} exists in list: {hasKey}");
                            }
                            
                            // List some nearby UIDs for debugging
                            var keysProperty = uidList.GetType().GetProperty("Keys");
                            if (keysProperty != null)
                            {
                                var keys = keysProperty.GetValue(uidList) as System.Collections.IEnumerable;
                                if (keys != null)
                                {
                                    var nearbyUIDs = new List<int>();
                                    foreach (var key in keys)
                                    {
                                        int uid = (int)key;
                                        // Find UIDs close to target
                                        if (Math.Abs(uid - targetUID) < 100)
                                        {
                                            nearbyUIDs.Add(uid);
                                            if (nearbyUIDs.Count >= 10) break;
                                        }
                                    }
                                    if (nearbyUIDs.Count > 0)
                                    {
                                        DebugLogger.Info($"[ObjectSync] Nearby UIDs: {string.Join(", ", nearbyUIDs)}");
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        DebugLogger.Warning($"[ObjectSync] m_UniqueIDList is null");
                    }
                }
                else
                {
                    DebugLogger.Warning($"[ObjectSync] m_UniqueIDList field not found");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Debug($"[ObjectSync] Diagnostic error: {ex.Message}");
            }
        }

        /// <summary>
        /// Apply object pickup - hide the object (remote player picked it up).
        /// </summary>
        private void ApplyObjectPickup(ObjectChange change)
        {
            var baseClass = GetGameObjectByUID(change.UID);
            if (baseClass == null)
            {
                DebugLogger.Warning($"[ObjectSync] Cannot find object UID:{change.UID} for pickup");
                DiagnoseUIDLookup(change.UID, change.ObjectType);
                return;
            }

            try
            {
                DebugLogger.Info($"[ObjectSync] Found object {change.UID}, type: {baseClass.GetType().Name}");
                
                // BaseClass extends MonoBehaviour, so we can cast to Component to get gameObject
                var component = baseClass as Component;
                if (component == null)
                {
                    DebugLogger.Warning($"[ObjectSync] Object {change.UID} is not a Component");
                    return;
                }
                
                GameObject unityObj = component.gameObject;
                DebugLogger.Info($"[ObjectSync] Got Unity GameObject: {unityObj.name}");
                
                // Hide the object by disabling renderers
                var renderers = unityObj.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers)
                {
                    if (renderer != null)
                    {
                        renderer.enabled = false;
                    }
                }
                DebugLogger.Info($"[ObjectSync] Disabled {renderers.Length} renderers on object {change.UID}");
                
                // Also set m_BeingHeld if available
                var objType = baseClass.GetType();
                var beingHeldField = objType.GetField("m_BeingHeld", 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (beingHeldField != null)
                {
                    beingHeldField.SetValue(baseClass, true);
                    DebugLogger.Debug($"[ObjectSync] Set m_BeingHeld = true");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[ObjectSync] Pickup apply error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Apply object drop - show the object and set its position (remote player dropped it).
        /// </summary>
        private void ApplyObjectDrop(ObjectChange change)
        {
            var baseClass = GetGameObjectByUID(change.UID);
            if (baseClass == null)
            {
                DebugLogger.Warning($"[ObjectSync] Cannot find object UID:{change.UID} for drop");
                DiagnoseUIDLookup(change.UID, change.ObjectType);
                return;
            }

            try
            {
                DebugLogger.Info($"[ObjectSync] Found object {change.UID} for drop, type: {baseClass.GetType().Name}");
                
                // Get the Unity GameObject
                var component = baseClass as Component;
                if (component == null)
                {
                    DebugLogger.Warning($"[ObjectSync] Object {change.UID} is not a Component");
                    return;
                }
                
                GameObject unityObj = component.gameObject;
                
                // Show the object by enabling renderers
                var renderers = unityObj.GetComponentsInChildren<Renderer>(true);
                foreach (var renderer in renderers)
                {
                    if (renderer != null)
                    {
                        renderer.enabled = true;
                    }
                }
                DebugLogger.Info($"[ObjectSync] Enabled {renderers.Length} renderers on object {change.UID}");
                
                // Set m_BeingHeld = false
                var objType = baseClass.GetType();
                var beingHeldField = objType.GetField("m_BeingHeld", 
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (beingHeldField != null)
                {
                    beingHeldField.SetValue(baseClass, false);
                    DebugLogger.Debug($"[ObjectSync] Set m_BeingHeld = false");
                }
                
                // Update the object's position
                // First try to use SetPosition method with world coordinates
                if (_tileCoordType != null && _tileCoordCtor != null)
                {
                    var coord = _tileCoordCtor.Invoke(new object[] { change.TileX, change.TileY });
                    
                    // Try ToWorldPositionTileCentered
                    var toWorldMethod = _tileCoordType.GetMethod("ToWorldPositionTileCentered",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (toWorldMethod != null)
                    {
                        var worldPos = (Vector3)toWorldMethod.Invoke(coord, null);
                        unityObj.transform.position = worldPos;
                        DebugLogger.Info($"[ObjectSync] Moved object to world position {worldPos}");
                    }
                    
                    // Also update m_TileCoord field
                    var tileCoordField = objType.GetField("m_TileCoord", 
                        BindingFlags.Public | BindingFlags.Instance);
                    if (tileCoordField != null)
                    {
                        tileCoordField.SetValue(baseClass, coord);
                        DebugLogger.Debug($"[ObjectSync] Updated m_TileCoord to ({change.TileX},{change.TileY})");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"[ObjectSync] Drop apply error: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Apply object update to game state.
        /// </summary>
        private void ApplyObjectUpdate(ObjectChange change)
        {
            DebugLogger.Debug($"[ObjectSync] Object update: UID={change.UID}, Type={change.Type}");
        }

        /// <summary>
        /// Update tracked object state.
        /// </summary>
        private void UpdateTrackedObject(ObjectChange change)
        {
            if (change.Type == ChangeType.Destroyed)
            {
                _syncedObjects.Remove(change.UID);
            }
            else
            {
                if (!_syncedObjects.TryGetValue(change.UID, out var obj))
                {
                    obj = new SyncedObject { UID = change.UID };
                    _syncedObjects[change.UID] = obj;
                }

                obj.ObjectType = change.ObjectType;
                obj.TileX = change.TileX;
                obj.TileY = change.TileY;
                obj.Rotation = change.Rotation;
                obj.IsHeld = (change.Type == ChangeType.PickedUp);
                obj.HeldByPlayer = change.PlayerId;
                obj.LastUpdateTime = Time.realtimeSinceStartup;
            }
        }

        /// <summary>
        /// Handle connection state changed.
        /// </summary>
        private void HandleStateChanged(ConnectionState state)
        {
            if (state == ConnectionState.Disconnected)
            {
                _syncedObjects.Clear();
                _pendingChanges.Clear();
                _localPlayerUID = -1;
                DebugLogger.Info("ObjectSync: Cleared all synced objects");
            }
        }

        /// <summary>
        /// Get all tracked objects (for debugging).
        /// </summary>
        public IReadOnlyDictionary<int, SyncedObject> TrackedObjects => _syncedObjects;
    }
}
