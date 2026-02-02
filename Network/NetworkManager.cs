using System;
using System.Collections.Generic;
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

        // Events
        public event Action<ConnectionState> OnStateChanged;
        public event Action<int, string> OnPlayerConnected;    // clientId, info
        public event Action<int> OnPlayerDisconnected;         // clientId
        public event Action<int, byte[]> OnDataReceived;       // clientId, data
        public event Action<string> OnError;

        // Properties
        public ConnectionState State => _state;
        public bool IsHost => _state == ConnectionState.Hosting;
        public bool IsClient => _state == ConnectionState.Connected;
        public bool IsConnected => _state == ConnectionState.Connected || _state == ConnectionState.Hosting;
        public int ConnectedPlayerCount => _connectedClients.Count;

        private NetworkManager()
        {
            // Initialize Telepathy server and client with max message size
            _server = new Server();
            _client = new Client();

            DebugLogger.Info("NetworkManager initialized with Telepathy");
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
                if (_server.Start(port))
                {
                    SetState(ConnectionState.Hosting);
                    DebugLogger.Info($"Server started on port {port}");
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
                _server.Stop();
                _connectedClients.Clear();
            }
            else if (_state == ConnectionState.Connected || _state == ConnectionState.Connecting)
            {
                _client.Disconnect();
            }

            SetState(ConnectionState.Disconnected);
            DebugLogger.Info("Disconnected");
        }

        /// <summary>
        /// Must be called every frame to process network events.
        /// </summary>
        public void Update()
        {
            // Process server messages
            if (_state == ConnectionState.Hosting)
            {
                ProcessServerMessages();
            }

            // Process client messages
            if (_state == ConnectionState.Connecting || _state == ConnectionState.Connected)
            {
                ProcessClientMessages();
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
                        if (!_connectedClients.Contains(msg.connectionId))
                            _connectedClients.Add(msg.connectionId);
                        OnPlayerConnected?.Invoke(msg.connectionId, $"Client {msg.connectionId}");
                        break;

                    case EventType.Data:
                        OnDataReceived?.Invoke(msg.connectionId, msg.data);
                        break;

                    case EventType.Disconnected:
                        DebugLogger.Info($"Client {msg.connectionId} disconnected");
                        _connectedClients.Remove(msg.connectionId);
                        OnPlayerDisconnected?.Invoke(msg.connectionId);
                        break;
                }
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
                        break;

                    case EventType.Data:
                        OnDataReceived?.Invoke(0, msg.data); // 0 = server
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

        /// <summary>
        /// Send data to a specific client (server only).
        /// </summary>
        public void SendTo(int clientId, byte[] data)
        {
            if (_state == ConnectionState.Hosting)
            {
                _server.Send(clientId, data);
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
