using System;
using System.Collections.Generic;
using System.Text;
using Telepathy;
using AutonautsMP.Core;

namespace AutonautsMP.Network
{
    /// <summary>
    /// Connection state for the network manager.
    /// </summary>
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Hosting
    }
    
    /// <summary>
    /// Internal message types for the networking protocol.
    /// </summary>
    internal enum NetMessageType : byte
    {
        PlayerInfo = 1,      // Player sends their name
        Ping = 2,            // Ping request
        Pong = 3,            // Ping response
        PlayerList = 4,      // Server sends list of all players
    }

    /// <summary>
    /// Core networking service using Telepathy (Unity-compatible TCP).
    /// Handles both hosting and joining game sessions.
    /// </summary>
    internal class NetworkManager : IDisposable
    {
        // Singleton instance
        private static NetworkManager _instance;
        public static NetworkManager Instance => _instance ?? (_instance = new NetworkManager());

        // Telepathy server and client
        private Server _server;
        private Client _client;

        // State
        private ConnectionState _state = ConnectionState.Disconnected;
        private readonly List<int> _connectedClients = new List<int>();
        
        // Player info tracking
        private readonly Dictionary<int, PlayerInfo> _players = new Dictionary<int, PlayerInfo>();
        private PlayerInfo _localPlayer;
        private string _localPlayerName;
        
        // Ping tracking
        private readonly Dictionary<int, DateTime> _pendingPings = new Dictionary<int, DateTime>();
        private DateTime _lastPingTime = DateTime.MinValue;
        private const float PING_INTERVAL = 2f; // Send ping every 2 seconds
        private int _clientPing = 0; // Client's ping to server

        // Packet statistics tracking
        private int _packetsSentThisSecond = 0;
        private int _packetsReceivedThisSecond = 0;
        private float _lastStatResetTime = 0f;
        private int _packetsSentPerSec = 0;
        private int _packetsReceivedPerSec = 0;

        // Events
        public event Action<ConnectionState> OnStateChanged;
        public event Action<int, string> OnPlayerConnected;    // clientId, info
        public event Action<int> OnPlayerDisconnected;         // clientId
        public event Action<int, byte[]> OnDataReceived;       // clientId, data
        public event Action<string> OnError;
        public event Action OnPlayerListUpdated;               // Called when player list changes

        // Properties
        public ConnectionState State => _state;
        public bool IsHost => _state == ConnectionState.Hosting;
        public bool IsClient => _state == ConnectionState.Connected;
        public bool IsConnected => _state == ConnectionState.Connected || _state == ConnectionState.Hosting;
        public int ConnectedPlayerCount => IsHost ? _players.Count + 1 : _players.Count;
        public IReadOnlyList<int> ConnectedClientIds => _connectedClients.AsReadOnly();
        public IReadOnlyDictionary<int, PlayerInfo> Players => _players;
        public PlayerInfo LocalPlayer => _localPlayer;
        public string LocalPlayerName => _localPlayerName;
        public int ClientPing => _clientPing;
        public int PacketsSentPerSec => _packetsSentPerSec;
        public int PacketsReceivedPerSec => _packetsReceivedPerSec;

        private NetworkManager()
        {
            // Initialize Telepathy server and client with max message size
            _server = new Server();
            _client = new Client();
            
            // Get local player name from Steam or fallback
            _localPlayerName = SteamHelper.GetLocalPlayerName();

            DebugLogger.Info($"NetworkManager initialized - Player name: {_localPlayerName}");
        }

        /// <summary>
        /// Start hosting a game on the specified port.
        /// </summary>
        public bool StartHost(int port)
        {
            if (_state != ConnectionState.Disconnected)
            {
                DebugLogger.Warning("Cannot start host: already connected/hosting");
                return false;
            }

            try
            {
                // Retry getting Steam name in case it wasn't ready during initialization
                RefreshPlayerName();
                
                if (_server.Start(port))
                {
                    // Create local player info for host
                    _localPlayer = new PlayerInfo(-1, _localPlayerName, true);
                    _players.Clear();
                    
                    SetState(ConnectionState.Hosting);
                    DebugLogger.Info($"Server started on port {port}");
                    
                    // Open debug console for host (if dev feature enabled)
                    if (DevSettings.IsFeatureEnabled(DevFeature.ConsoleOnHost))
                    {
                        DebugConsole.Show();
                        DebugConsole.LogInfo($"Server started on port {port}");
                        DebugConsole.LogInfo($"Local player: {_localPlayerName}");
                        DebugConsole.LogNetwork("Waiting for clients to connect...");
                    }
                    
                    return true;
                }
                else
                {
                    DebugLogger.Error($"Failed to start server on port {port}");
                    OnError?.Invoke($"Failed to start server on port {port}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Exception starting host: {ex.Message}");
                OnError?.Invoke($"Error: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Refresh the local player name from Steam.
        /// Called when connecting to retry in case Steam wasn't ready initially.
        /// </summary>
        private void RefreshPlayerName()
        {
            // If we already have a Steam name (not a fallback), don't retry
            if (SteamHelper.IsSteamAvailable)
            {
                return;
            }
            
            // Try to reinitialize Steam - it might be ready now
            SteamHelper.Reinitialize();
            string newName = SteamHelper.GetLocalPlayerName();
            
            if (newName != _localPlayerName)
            {
                DebugLogger.Info($"Updated player name from '{_localPlayerName}' to '{newName}'");
                _localPlayerName = newName;
            }
        }

        /// <summary>
        /// Join a game at the specified IP and port.
        /// </summary>
        public bool JoinGame(string ip, int port)
        {
            if (_state != ConnectionState.Disconnected)
            {
                DebugLogger.Warning("Cannot join: already connected/hosting");
                return false;
            }

            try
            {
                // Retry getting Steam name in case it wasn't ready during initialization
                RefreshPlayerName();
                
                SetState(ConnectionState.Connecting);
                _client.Connect(ip, port);
                DebugLogger.Info($"Connecting to {ip}:{port}...");
                return true;
            }
            catch (Exception ex)
            {
                DebugLogger.Error($"Exception joining game: {ex.Message}");
                OnError?.Invoke($"Error: {ex.Message}");
                SetState(ConnectionState.Disconnected);
                return false;
            }
        }

        /// <summary>
        /// Disconnect from the current session.
        /// </summary>
        public void Disconnect()
        {
            if (_state == ConnectionState.Hosting)
            {
                DebugConsole.LogInfo("Server shutting down...");
                _server.Stop();
                _connectedClients.Clear();
                // Close the debug console when stopping host
                DebugConsole.Hide();
            }
            else if (_state == ConnectionState.Connected || _state == ConnectionState.Connecting)
            {
                _client.Disconnect();
            }

            _players.Clear();
            _localPlayer = null;
            _pendingPings.Clear();
            _clientPing = 0;
            
            SetState(ConnectionState.Disconnected);
            DebugLogger.Info("Disconnected");
        }

        /// <summary>
        /// Must be called every frame to process network events.
        /// </summary>
        public void Update()
        {
            // Update packet statistics every second
            UpdatePacketStats();

            // Process server messages
            if (_state == ConnectionState.Hosting)
            {
                ProcessServerMessages();
                UpdateServerPing();
            }

            // Process client messages
            if (_state == ConnectionState.Connecting || _state == ConnectionState.Connected)
            {
                ProcessClientMessages();
                UpdateClientPing();
            }
        }

        /// <summary>
        /// Updates packet statistics - resets counters every second.
        /// </summary>
        private void UpdatePacketStats()
        {
            float currentTime = UnityEngine.Time.realtimeSinceStartup;
            if (currentTime - _lastStatResetTime >= 1f)
            {
                _packetsSentPerSec = _packetsSentThisSecond;
                _packetsReceivedPerSec = _packetsReceivedThisSecond;
                _packetsSentThisSecond = 0;
                _packetsReceivedThisSecond = 0;
                _lastStatResetTime = currentTime;
            }
        }
        
        private void UpdateServerPing()
        {
            // Send ping to all clients periodically
            if ((DateTime.UtcNow - _lastPingTime).TotalSeconds >= PING_INTERVAL)
            {
                _lastPingTime = DateTime.UtcNow;
                
                foreach (var clientId in _connectedClients)
                {
                    _pendingPings[clientId] = DateTime.UtcNow;
                    SendTo(clientId, new byte[] { (byte)NetMessageType.Ping });
                }
            }
        }
        
        private void UpdateClientPing()
        {
            // Client sends ping to server periodically
            if (_state == ConnectionState.Connected && (DateTime.UtcNow - _lastPingTime).TotalSeconds >= PING_INTERVAL)
            {
                _lastPingTime = DateTime.UtcNow;
                _pendingPings[0] = DateTime.UtcNow;
                SendToServer(new byte[] { (byte)NetMessageType.Ping });
            }
        }

        private void ProcessServerMessages()
        {
            Message msg;
            while (_server.GetNextMessage(out msg))
            {
                switch (msg.eventType)
                {
                    case EventType.Connected:
                        DebugLogger.Info($"Client {msg.connectionId} connected");
                        DebugConsole.LogNetwork($"Client {msg.connectionId} connected");
                        if (!_connectedClients.Contains(msg.connectionId))
                        {
                            _connectedClients.Add(msg.connectionId);
                            // Create placeholder player info until we get their name
                            _players[msg.connectionId] = new PlayerInfo(msg.connectionId, $"Player {msg.connectionId}");
                        }
                        OnPlayerConnected?.Invoke(msg.connectionId, $"Client {msg.connectionId}");
                        OnPlayerListUpdated?.Invoke();
                        break;

                    case EventType.Data:
                        HandleServerData(msg.connectionId, msg.data);
                        break;

                    case EventType.Disconnected:
                        string disconnectedName = _players.ContainsKey(msg.connectionId) ? _players[msg.connectionId].Name : $"Client {msg.connectionId}";
                        DebugLogger.Info($"Client {msg.connectionId} disconnected");
                        DebugConsole.LogNetwork($"{disconnectedName} disconnected");
                        _connectedClients.Remove(msg.connectionId);
                        _players.Remove(msg.connectionId);
                        _pendingPings.Remove(msg.connectionId);
                        OnPlayerDisconnected?.Invoke(msg.connectionId);
                        OnPlayerListUpdated?.Invoke();
                        break;
                }
            }
        }
        
        private void HandleServerData(int clientId, byte[] data)
        {
            if (data == null || data.Length == 0) return;
            
            _packetsReceivedThisSecond++;
            var msgType = (NetMessageType)data[0];
            
            switch (msgType)
            {
                case NetMessageType.PlayerInfo:
                    // Client sent their player name
                    if (data.Length > 1)
                    {
                        string playerName = Encoding.UTF8.GetString(data, 1, data.Length - 1);
                        if (_players.ContainsKey(clientId))
                        {
                            _players[clientId].Name = playerName;
                            DebugLogger.Info($"Player {clientId} name: {playerName}");
                            DebugConsole.LogInfo($"{playerName} has joined");
                            OnPlayerListUpdated?.Invoke();
                        }
                    }
                    break;
                    
                case NetMessageType.Ping:
                    // Client is pinging us, send pong back
                    SendTo(clientId, new byte[] { (byte)NetMessageType.Pong });
                    break;
                    
                case NetMessageType.Pong:
                    // Got pong response from client
                    if (_pendingPings.TryGetValue(clientId, out DateTime pingTime))
                    {
                        int ping = (int)(DateTime.UtcNow - pingTime).TotalMilliseconds;
                        if (_players.ContainsKey(clientId))
                        {
                            _players[clientId].Ping = ping;
                            _players[clientId].LastPingTime = DateTime.UtcNow;
                        }
                        _pendingPings.Remove(clientId);
                    }
                    break;
                    
                default:
                    // Other data - pass to event handlers
                    OnDataReceived?.Invoke(clientId, data);
                    break;
            }
        }

        private void ProcessClientMessages()
        {
            Message msg;
            while (_client.GetNextMessage(out msg))
            {
                switch (msg.eventType)
                {
                    case EventType.Connected:
                        DebugLogger.Info("Connected to server!");
                        SetState(ConnectionState.Connected);
                        
                        // Create local player info
                        _localPlayer = new PlayerInfo(0, _localPlayerName, false);
                        
                        // Send our player name to the server
                        SendPlayerInfo();
                        break;

                    case EventType.Data:
                        HandleClientData(msg.data);
                        break;

                    case EventType.Disconnected:
                        DebugLogger.Info("Disconnected from server");
                        SetState(ConnectionState.Disconnected);
                        OnError?.Invoke("Disconnected from server");
                        break;
                }
            }

            // Check if connection failed
            if (_state == ConnectionState.Connecting && !_client.Connecting && !_client.Connected)
            {
                SetState(ConnectionState.Disconnected);
                OnError?.Invoke("Connection failed");
            }
        }
        
        private void HandleClientData(byte[] data)
        {
            if (data == null || data.Length == 0) return;
            
            _packetsReceivedThisSecond++;
            var msgType = (NetMessageType)data[0];
            
            switch (msgType)
            {
                case NetMessageType.Ping:
                    // Server is pinging us, send pong back
                    SendToServer(new byte[] { (byte)NetMessageType.Pong });
                    break;
                    
                case NetMessageType.Pong:
                    // Got pong response from server
                    if (_pendingPings.TryGetValue(0, out DateTime pingTime))
                    {
                        _clientPing = (int)(DateTime.UtcNow - pingTime).TotalMilliseconds;
                        if (_localPlayer != null)
                        {
                            _localPlayer.Ping = _clientPing;
                        }
                        _pendingPings.Remove(0);
                    }
                    break;
                    
                case NetMessageType.PlayerList:
                    // Server sent updated player list (future feature)
                    break;
                    
                default:
                    // Other data - pass to event handlers
                    OnDataReceived?.Invoke(0, data);
                    break;
            }
        }
        
        /// <summary>
        /// Send local player info to the server.
        /// </summary>
        private void SendPlayerInfo()
        {
            if (_state != ConnectionState.Connected) return;
            
            byte[] nameBytes = Encoding.UTF8.GetBytes(_localPlayerName);
            byte[] data = new byte[1 + nameBytes.Length];
            data[0] = (byte)NetMessageType.PlayerInfo;
            Array.Copy(nameBytes, 0, data, 1, nameBytes.Length);
            
            SendToServer(data);
            DebugLogger.Info($"Sent player info: {_localPlayerName}");
        }
        
        /// <summary>
        /// Get all players including the local player (for display purposes).
        /// </summary>
        public List<PlayerInfo> GetAllPlayers()
        {
            var allPlayers = new List<PlayerInfo>();
            
            if (_localPlayer != null)
            {
                allPlayers.Add(_localPlayer);
            }
            
            foreach (var player in _players.Values)
            {
                allPlayers.Add(player);
            }
            
            return allPlayers;
        }

        /// <summary>
        /// Send data to a specific client (server only).
        /// </summary>
        public void SendTo(int clientId, byte[] data)
        {
            if (_state == ConnectionState.Hosting)
            {
                _server.Send(clientId, data);
                _packetsSentThisSecond++;
            }
        }

        /// <summary>
        /// Send data to the server (client only).
        /// </summary>
        public void SendToServer(byte[] data)
        {
            if (_state == ConnectionState.Connected)
            {
                _client.Send(data);
                _packetsSentThisSecond++;
            }
        }

        /// <summary>
        /// Broadcast data to all connected clients (server only).
        /// </summary>
        public void Broadcast(byte[] data)
        {
            if (_state == ConnectionState.Hosting)
            {
                foreach (var clientId in _connectedClients)
                {
                    _server.Send(clientId, data);
                    _packetsSentThisSecond++;
                }
            }
        }

        private void SetState(ConnectionState newState)
        {
            if (_state != newState)
            {
                _state = newState;
                DebugLogger.Info($"Connection state changed: {newState}");
                OnStateChanged?.Invoke(newState);
            }
        }

        public void Dispose()
        {
            Disconnect();
            _instance = null;
        }
    }
}
