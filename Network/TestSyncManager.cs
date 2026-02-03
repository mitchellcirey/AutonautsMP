using System;
using System.IO;
using AutonautsMP.Core;
using static AutonautsMP.Core.DevFeature;

namespace AutonautsMP.Network
{
    /// <summary>
    /// Manages a shared counter for testing network synchronization.
    /// Follows proper authority model: host is authoritative, clients only request changes.
    /// </summary>
    public class TestSyncManager
    {
        // Singleton instance
        private static TestSyncManager _instance;
        public static TestSyncManager Instance => _instance ?? (_instance = new TestSyncManager());

        /// <summary>
        /// The shared counter value (authoritative on host, replicated on clients).
        /// </summary>
        public int SharedCounter { get; private set; } = 0;

        /// <summary>
        /// Event fired when the counter value changes.
        /// </summary>
        public event Action<int> OnCounterChanged;

        private TestSyncManager()
        {
            // Subscribe to network data events
            NetworkManager.Instance.OnDataReceived += HandleDataReceived;
            NetworkManager.Instance.OnStateChanged += HandleStateChanged;
            
            DebugLogger.Info("TestSyncManager initialized");
        }

        /// <summary>
        /// Initializes the TestSyncManager (call once at startup).
        /// </summary>
        public static void Initialize()
        {
            // Just access the instance to trigger construction
            var _ = Instance;
        }

        /// <summary>
        /// Request to increment the shared counter.
        /// - If host: increments locally and broadcasts to all clients
        /// - If client: sends request to host (does NOT modify local state)
        /// </summary>
        public void RequestIncrement()
        {
            if (NetworkManager.Instance.IsHost)
            {
                // Host: increment locally and broadcast
                SharedCounter++;
                
                DebugLogger.Info($"[HOST] Counter incremented to {SharedCounter}");
                if (DevSettings.IsFeatureEnabled(PacketLogging))
                {
                    DebugConsole.LogPacket("OUT", "TestBroadcast", $"Counter = {SharedCounter}");
                }
                
                // Broadcast to all clients
                BroadcastCounter();
                
                // Also notify local UI
                OnCounterChanged?.Invoke(SharedCounter);
            }
            else if (NetworkManager.Instance.IsClient)
            {
                // Client: only send request, do NOT modify local state
                // Wait for host to broadcast the new value
                DebugLogger.Info("[CLIENT] Sending increment request to host");
                
                var packet = NetworkMessages.BuildTestIncrement();
                NetworkManager.Instance.SendToServer(packet);
            }
            else
            {
                DebugLogger.Warning("Cannot increment: not connected");
            }
        }

        /// <summary>
        /// Broadcasts the current counter value to all connected clients.
        /// Only callable by the host.
        /// </summary>
        private void BroadcastCounter()
        {
            if (!NetworkManager.Instance.IsHost) return;

            var packet = NetworkMessages.BuildTestBroadcast(SharedCounter);
            NetworkManager.Instance.Broadcast(packet);
        }

        /// <summary>
        /// Handles incoming network data.
        /// </summary>
        private void HandleDataReceived(int clientId, byte[] data)
        {
            if (data == null || data.Length < 1) return;

            var msgType = NetworkMessages.ReadType(data);

            switch (msgType)
            {
                case MessageType.TestIncrement:
                    HandleTestIncrement(clientId);
                    break;

                case MessageType.TestBroadcast:
                    HandleTestBroadcast(data);
                    break;
            }
        }

        /// <summary>
        /// Host receives increment request from a client.
        /// </summary>
        private void HandleTestIncrement(int clientId)
        {
            if (!NetworkManager.Instance.IsHost)
            {
                // Only host should receive increment requests
                DebugLogger.Warning("Client received TestIncrement - ignoring (authority violation)");
                return;
            }

            // Log receipt
            var playerName = GetPlayerName(clientId);
            DebugLogger.Info($"[HOST] Received increment request from {playerName} (client {clientId})");
            if (DevSettings.IsFeatureEnabled(PacketLogging))
            {
                DebugConsole.LogPacket("IN", "TestIncrement", $"From: {playerName}");
            }

            // Increment the counter (host is authoritative)
            SharedCounter++;
            
            DebugLogger.Info($"[HOST] Counter incremented to {SharedCounter}");
            if (DevSettings.IsFeatureEnabled(PacketLogging))
            {
                DebugConsole.LogNetwork($"Counter is now {SharedCounter}");
                DebugConsole.LogPacket("OUT", "TestBroadcast", $"Counter = {SharedCounter} (to all clients)");
            }
            BroadcastCounter();

            // Notify local UI
            OnCounterChanged?.Invoke(SharedCounter);
        }

        /// <summary>
        /// Client receives broadcasted counter value from host.
        /// </summary>
        private void HandleTestBroadcast(byte[] data)
        {
            if (NetworkManager.Instance.IsHost)
            {
                // Host doesn't need to process its own broadcasts
                return;
            }

            try
            {
                using (var reader = NetworkMessages.CreateReader(data))
                {
                    int newValue = reader.ReadInt32();
                    
                    DebugLogger.Info($"[CLIENT] Received counter broadcast: {newValue}");
                    
                    // Update local counter (client just accepts host's authority)
                    SharedCounter = newValue;
                    
                    // Notify UI
                    OnCounterChanged?.Invoke(SharedCounter);
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Failed to parse TestBroadcast: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles connection state changes (reset counter on disconnect).
        /// </summary>
        private void HandleStateChanged(ConnectionState state)
        {
            if (state == ConnectionState.Disconnected)
            {
                // Reset counter when disconnected
                SharedCounter = 0;
                OnCounterChanged?.Invoke(SharedCounter);
            }
        }

        /// <summary>
        /// Gets a player's name by client ID.
        /// </summary>
        private string GetPlayerName(int clientId)
        {
            var players = NetworkManager.Instance.Players;
            if (players.TryGetValue(clientId, out var player))
            {
                return player.Name;
            }
            return $"Player {clientId}";
        }
    }
}
