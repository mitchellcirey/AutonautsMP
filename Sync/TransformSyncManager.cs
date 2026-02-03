using System;
using System.Collections.Generic;
using UnityEngine;
using AutonautsMP.Core;
using AutonautsMP.Network;

namespace AutonautsMP.Sync
{
    /// <summary>
    /// Manages player transform synchronization between host and clients.
    /// Uses host-authoritative model - host broadcasts all player positions.
    /// </summary>
    public class TransformSyncManager
    {
        // Singleton instance
        private static TransformSyncManager _instance;
        public static TransformSyncManager Instance => _instance ?? (_instance = new TransformSyncManager());

        // Remote player representations
        private readonly Dictionary<int, RemotePlayer> _remotePlayers = new Dictionary<int, RemotePlayer>();

        // Sync timing
        private const float SYNC_INTERVAL = 0.05f; // 20 updates per second
        private float _lastSyncTime = 0f;

        // Local player tracking
        private Transform _localPlayerTransform;
        private Vector3 _lastSentPosition;
        private float _lastSentRotation;
        private const float POSITION_THRESHOLD = 0.01f; // Only send if moved more than this
        private const float ROTATION_THRESHOLD = 1f; // Only send if rotated more than this (degrees)

        // Statistics
        public int TransformPacketsSent { get; private set; }
        public int TransformPacketsReceived { get; private set; }

        private TransformSyncManager()
        {
            // Subscribe to network events
            NetworkManager.Instance.OnDataReceived += HandleDataReceived;
            NetworkManager.Instance.OnPlayerConnected += HandlePlayerConnected;
            NetworkManager.Instance.OnPlayerDisconnected += HandlePlayerDisconnected;
            NetworkManager.Instance.OnStateChanged += HandleStateChanged;

            DebugLogger.Info("TransformSyncManager initialized");
        }

        /// <summary>
        /// Initialize the TransformSyncManager (call once at startup).
        /// </summary>
        public static void Initialize()
        {
            var _ = Instance;
        }

        /// <summary>
        /// Set the local player's transform to track.
        /// Must be called when the player is spawned.
        /// </summary>
        public void SetLocalPlayer(Transform playerTransform)
        {
            _localPlayerTransform = playerTransform;
            if (playerTransform != null)
            {
                _lastSentPosition = playerTransform.position;
                _lastSentRotation = playerTransform.eulerAngles.y;
                DebugLogger.Info($"Local player transform set: {playerTransform.name}");
            }
            else
            {
                DebugLogger.Info("Local player transform cleared");
            }
        }

        /// <summary>
        /// Try to automatically find and set the local player.
        /// Searches for common player object names.
        /// </summary>
        public bool TryFindLocalPlayer()
        {
            // Common player/farmer object names
            var playerNames = new[] { "Farmer", "Player", "LocalPlayer", "FarmerPlayer" };

            foreach (var name in playerNames)
            {
                var obj = GameObject.Find(name);
                if (obj != null)
                {
                    SetLocalPlayer(obj.transform);
                    return true;
                }
            }

            // Try to find by tag
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null)
            {
                SetLocalPlayer(tagged.transform);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Must be called every frame to update sync.
        /// </summary>
        public void Update()
        {
            if (!NetworkManager.Instance.IsConnected)
                return;

            // Try to find local player if not set
            if (_localPlayerTransform == null)
            {
                TryFindLocalPlayer();
            }

            // Update remote players (interpolation)
            foreach (var remote in _remotePlayers.Values)
            {
                remote.Update(Time.deltaTime);
            }

            // Send local transform at interval
            float currentTime = Time.realtimeSinceStartup;
            if (currentTime - _lastSyncTime >= SYNC_INTERVAL)
            {
                _lastSyncTime = currentTime;
                SendLocalTransform();
            }
        }

        /// <summary>
        /// Send local player transform to network.
        /// </summary>
        private void SendLocalTransform()
        {
            if (_localPlayerTransform == null)
                return;

            Vector3 pos = _localPlayerTransform.position;
            float rotY = _localPlayerTransform.eulerAngles.y;

            // Check if we've moved enough to warrant sending
            float posDelta = Vector3.Distance(pos, _lastSentPosition);
            float rotDelta = Mathf.Abs(Mathf.DeltaAngle(rotY, _lastSentRotation));

            if (posDelta < POSITION_THRESHOLD && rotDelta < ROTATION_THRESHOLD)
                return;

            _lastSentPosition = pos;
            _lastSentRotation = rotY;

            // Get our player ID
            int playerId = NetworkManager.Instance.IsHost ? -1 : 0;

            var packet = NetworkMessages.BuildPlayerTransform(playerId, pos.x, pos.y, pos.z, rotY);

            if (NetworkManager.Instance.IsHost)
            {
                // Host broadcasts to all clients
                NetworkManager.Instance.Broadcast(packet);
            }
            else
            {
                // Client sends to host
                NetworkManager.Instance.SendToServer(packet);
            }

            TransformPacketsSent++;
        }

        /// <summary>
        /// Handle incoming network data.
        /// </summary>
        private void HandleDataReceived(int senderId, byte[] data)
        {
            if (data == null || data.Length < 1)
                return;

            var msgType = NetworkMessages.ReadType(data);
            if (msgType != MessageType.PlayerTransform)
                return;

            TransformPacketsReceived++;

            try
            {
                var transformData = PlayerTransformData.Read(data);
                
                if (NetworkManager.Instance.IsHost)
                {
                    // Host received transform from client - update and rebroadcast
                    HandleClientTransform(senderId, transformData);
                }
                else
                {
                    // Client received transform - could be host or another player
                    HandleRemoteTransform(transformData);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Failed to parse PlayerTransform: {ex.Message}");
            }
        }

        /// <summary>
        /// Host handles transform update from a client.
        /// </summary>
        private void HandleClientTransform(int clientId, PlayerTransformData data)
        {
            // Update the remote player representation
            UpdateRemotePlayer(clientId, data.X, data.Y, data.Z, data.RotY);

            // Rebroadcast to all other clients (with correct player ID)
            var packet = NetworkMessages.BuildPlayerTransform(clientId, data.X, data.Y, data.Z, data.RotY);
            
            foreach (var otherId in NetworkManager.Instance.ConnectedClientIds)
            {
                if (otherId != clientId)
                {
                    NetworkManager.Instance.SendTo(otherId, packet);
                    TransformPacketsSent++;
                }
            }
        }

        /// <summary>
        /// Client handles transform update from host.
        /// </summary>
        private void HandleRemoteTransform(PlayerTransformData data)
        {
            // Don't update ourselves
            int localId = 0; // Client's own ID from their perspective
            if (data.PlayerId == localId)
                return;

            UpdateRemotePlayer(data.PlayerId, data.X, data.Y, data.Z, data.RotY);
        }

        /// <summary>
        /// Update or create a remote player representation.
        /// </summary>
        private void UpdateRemotePlayer(int playerId, float x, float y, float z, float rotY)
        {
            if (!_remotePlayers.TryGetValue(playerId, out var remote))
            {
                // Create new remote player
                string playerName = GetPlayerName(playerId);
                remote = new RemotePlayer(playerId, playerName);
                _remotePlayers[playerId] = remote;
                DebugLogger.Info($"Created remote player representation for {playerName} (ID: {playerId})");
            }

            remote.SetTargetPosition(new Vector3(x, y, z), rotY);
        }

        /// <summary>
        /// Handle player connected event.
        /// </summary>
        private void HandlePlayerConnected(int clientId, string info)
        {
            DebugLogger.Info($"TransformSync: Player {clientId} connected");
        }

        /// <summary>
        /// Handle player disconnected event.
        /// </summary>
        private void HandlePlayerDisconnected(int clientId)
        {
            if (_remotePlayers.TryGetValue(clientId, out var remote))
            {
                remote.Destroy();
                _remotePlayers.Remove(clientId);
                DebugLogger.Info($"Removed remote player {clientId}");
            }
        }

        /// <summary>
        /// Handle connection state changed.
        /// </summary>
        private void HandleStateChanged(ConnectionState state)
        {
            if (state == ConnectionState.Disconnected)
            {
                // Clean up all remote players
                foreach (var remote in _remotePlayers.Values)
                {
                    remote.Destroy();
                }
                _remotePlayers.Clear();
                _localPlayerTransform = null;
                TransformPacketsSent = 0;
                TransformPacketsReceived = 0;
                DebugLogger.Info("TransformSync: Cleared all remote players");
            }
        }

        /// <summary>
        /// Get player name by ID.
        /// </summary>
        private string GetPlayerName(int playerId)
        {
            if (playerId == -1)
            {
                return NetworkManager.Instance.LocalPlayerName + " (Host)";
            }

            var players = NetworkManager.Instance.Players;
            if (players.TryGetValue(playerId, out var player))
            {
                return player.Name;
            }

            return $"Player {playerId}";
        }

        /// <summary>
        /// Get all remote players (for debugging/display).
        /// </summary>
        public IReadOnlyDictionary<int, RemotePlayer> RemotePlayers => _remotePlayers;
    }
}
