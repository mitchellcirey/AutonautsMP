using System;
using System.Collections.Generic;
using LiteNetLib;
using LiteNetLib.Utils;

namespace AutonautsMP.Network
{
    /// <summary>
    /// Represents a connected player
    /// </summary>
    public class ConnectedPlayer
    {
        public int Id;
        public string Name = "Player";
        public ulong SteamId;
        public NetPeer? Peer;
        public float X, Y, Z;
        public float Rotation;
        public byte State;
        public DateTime LastUpdate = DateTime.UtcNow;
    }

    /// <summary>
    /// Bridge class that wraps all LiteNetLib functionality.
    /// Loaded dynamically by the main assembly - never referenced directly.
    /// </summary>
    public class NetworkBridge : INetEventListener
    {
        private NetManager? _netManager;
        private bool _isHost;
        private string _statusMessage = "Not connected";
        private readonly NetDataWriter _writer = new NetDataWriter();

        // Player tracking
        private int _localPlayerId;
        private string _localPlayerName = "Player";
        private readonly Dictionary<int, ConnectedPlayer> _players = new Dictionary<int, ConnectedPlayer>();
        private readonly Dictionary<NetPeer, int> _peerToPlayerId = new Dictionary<NetPeer, int>();
        private int _nextPlayerId = 1;

        // Position sync
        private float _lastPositionSendTime;
        private const float PositionSendInterval = 0.1f; // 10Hz

        // Callbacks for game integration
        public Action<ConnectedPlayer>? OnPlayerJoined;
        public Action<int>? OnPlayerLeft;
        public Action<int, float, float, float, float, byte>? OnPlayerPositionReceived;
        public Action<byte[]>? OnWorldStateReceived;

        /// <summary>
        /// Set the local player's name before connecting
        /// </summary>
        public void SetPlayerName(string name)
        {
            _localPlayerName = name ?? "Player";
        }

        /// <summary>
        /// Get the local player's name
        /// </summary>
        public string GetPlayerName() => _localPlayerName;

        /// <summary>
        /// Get all connected players (including self)
        /// </summary>
        public ConnectedPlayer[] GetPlayers()
        {
            var list = new List<ConnectedPlayer>(_players.Values);
            return list.ToArray();
        }

        /// <summary>
        /// Get player count
        /// </summary>
        public int GetPlayerCount() => _players.Count;

        /// <summary>
        /// Check if we are the host
        /// </summary>
        public bool IsHost() => _isHost;

        /// <summary>
        /// Check if connected
        /// </summary>
        public bool IsConnected() => _netManager != null && (_isHost || _players.Count > 0);

        /// <summary>
        /// Get local player ID
        /// </summary>
        public int GetLocalPlayerId() => _localPlayerId;

        public void StartHost(int port)
        {
            Stop();
            _netManager = new NetManager(this) 
            { 
                AutoRecycle = true,
                DisconnectTimeout = 10000
            };
            _netManager.Start(port);
            _isHost = true;
            _statusMessage = "Hosting on port " + port;

            // Add self as player
            _localPlayerId = _nextPlayerId++;
            var self = new ConnectedPlayer
            {
                Id = _localPlayerId,
                Name = _localPlayerName,
                Peer = null // Host has no peer for self
            };
            _players[_localPlayerId] = self;
        }

        public void Connect(string ip, int port)
        {
            Stop();
            _netManager = new NetManager(this) 
            { 
                AutoRecycle = true,
                DisconnectTimeout = 10000
            };
            _netManager.Start();
            _netManager.Connect(ip, port, "AutonautsMP");
            _isHost = false;
            _statusMessage = "Connecting to " + ip + ":" + port;
        }

        public void Disconnect()
        {
            Stop();
            _statusMessage = "Disconnected";
        }

        public void Update()
        {
            _netManager?.PollEvents();
        }

        /// <summary>
        /// Send local player position to all peers
        /// </summary>
        public void SendPosition(float x, float y, float z, float rotation, byte state)
        {
            if (_netManager == null) return;

            // Update local player
            if (_players.TryGetValue(_localPlayerId, out var self))
            {
                self.X = x;
                self.Y = y;
                self.Z = z;
                self.Rotation = rotation;
                self.State = state;
            }

            // Send to all peers
            _writer.Reset();
            var packet = new PlayerPosition
            {
                PlayerId = _localPlayerId,
                X = x,
                Y = y,
                Z = z,
                Rotation = rotation,
                State = state
            };
            packet.Serialize(_writer);

            foreach (var peer in _netManager.ConnectedPeerList)
            {
                peer.Send(_writer, DeliveryMethod.Sequenced);
            }
        }

        /// <summary>
        /// Send world state to a specific peer (host only)
        /// </summary>
        public void SendWorldState(NetPeer peer, byte[] worldData)
        {
            if (!_isHost || worldData == null) return;

            const int chunkSize = 32000; // Keep under MTU limits
            int totalChunks = (worldData.Length + chunkSize - 1) / chunkSize;

            for (int i = 0; i < totalChunks; i++)
            {
                int offset = i * chunkSize;
                int length = Math.Min(chunkSize, worldData.Length - offset);
                byte[] chunkData = new byte[length];
                Array.Copy(worldData, offset, chunkData, 0, length);

                _writer.Reset();
                var chunk = new WorldStateChunk
                {
                    ChunkIndex = i,
                    TotalChunks = totalChunks,
                    Data = chunkData
                };
                chunk.Serialize(_writer);
                peer.Send(_writer, DeliveryMethod.ReliableOrdered);
            }
        }

        public string GetStatus()
        {
            if (_netManager == null) return _statusMessage;

            int peerCount = _netManager.ConnectedPeersCount;
            if (_isHost)
            {
                return $"Hosting | {_players.Count} player(s)";
            }
            else if (peerCount > 0)
            {
                return $"Connected | {_players.Count} player(s)";
            }
            return _statusMessage;
        }

        private void Stop()
        {
            _players.Clear();
            _peerToPlayerId.Clear();
            _nextPlayerId = 1;
            _localPlayerId = 0;
            _netManager?.Stop();
            _netManager = null;
        }

        // Send player info to a peer
        private void SendPlayerInfo(NetPeer peer, PlayerInfo info)
        {
            _writer.Reset();
            info.Serialize(_writer);
            peer.Send(_writer, DeliveryMethod.ReliableOrdered);
        }

        // Send player list to a peer
        private void SendPlayerList(NetPeer peer)
        {
            _writer.Reset();
            var list = new PlayerList { Players = new PlayerInfo[_players.Count] };
            int i = 0;
            foreach (var p in _players.Values)
            {
                list.Players[i++] = new PlayerInfo
                {
                    PlayerId = p.Id,
                    PlayerName = p.Name,
                    SteamId = p.SteamId
                };
            }
            list.Serialize(_writer);
            peer.Send(_writer, DeliveryMethod.ReliableOrdered);
        }

        // Broadcast player left to all peers
        private void BroadcastPlayerLeft(int playerId)
        {
            if (_netManager == null) return;

            _writer.Reset();
            var packet = new PlayerLeft { PlayerId = playerId };
            packet.Serialize(_writer);

            foreach (var peer in _netManager.ConnectedPeerList)
            {
                peer.Send(_writer, DeliveryMethod.ReliableOrdered);
            }
        }

        // INetEventListener implementation
        public void OnPeerConnected(NetPeer peer)
        {
            if (_isHost)
            {
                // Host: assign player ID and wait for their info
                int playerId = _nextPlayerId++;
                _peerToPlayerId[peer] = playerId;

                // Create placeholder player
                var player = new ConnectedPlayer
                {
                    Id = playerId,
                    Name = "Player " + playerId,
                    Peer = peer
                };
                _players[playerId] = player;

                // Send them their assigned ID and the current player list
                SendPlayerInfo(peer, new PlayerInfo
                {
                    PlayerId = playerId,
                    PlayerName = _localPlayerName, // Host's name
                    SteamId = 0
                });
                SendPlayerList(peer);

                _statusMessage = "Client connected (ID: " + playerId + ")";
                OnPlayerJoined?.Invoke(player);
            }
            else
            {
                // Client: we're connected to host
                _statusMessage = "Connected to host!";

                // Send our info
                SendPlayerInfo(peer, new PlayerInfo
                {
                    PlayerId = 0, // Will be assigned by host
                    PlayerName = _localPlayerName,
                    SteamId = 0
                });
            }
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            if (_peerToPlayerId.TryGetValue(peer, out int playerId))
            {
                _players.Remove(playerId);
                _peerToPlayerId.Remove(peer);

                if (_isHost)
                {
                    BroadcastPlayerLeft(playerId);
                }

                OnPlayerLeft?.Invoke(playerId);
            }

            if (!_isHost)
            {
                _players.Clear();
                _peerToPlayerId.Clear();
            }

            _statusMessage = "Disconnected: " + disconnectInfo.Reason;
        }

        public void OnNetworkError(System.Net.IPEndPoint endPoint, System.Net.Sockets.SocketError socketError)
        {
            _statusMessage = "Network error: " + socketError;
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            try
            {
                if (reader.AvailableBytes < 1) return;

                PacketType type = (PacketType)reader.GetByte();

                switch (type)
                {
                    case PacketType.PlayerInfo:
                        HandlePlayerInfo(peer, reader);
                        break;

                    case PacketType.PlayerPosition:
                        HandlePlayerPosition(reader);
                        break;

                    case PacketType.PlayerList:
                        HandlePlayerList(reader);
                        break;

                    case PacketType.WorldStateChunk:
                        HandleWorldStateChunk(reader);
                        break;

                    case PacketType.PlayerLeft:
                        HandlePlayerLeft(reader);
                        break;
                }
            }
            catch (Exception ex)
            {
                _statusMessage = "Packet error: " + ex.Message;
            }
            finally
            {
                reader.Recycle();
            }
        }

        private void HandlePlayerInfo(NetPeer peer, NetPacketReader reader)
        {
            var info = new PlayerInfo();
            info.Deserialize(reader);

            if (_isHost)
            {
                // Host received client's info - update their name
                if (_peerToPlayerId.TryGetValue(peer, out int playerId))
                {
                    if (_players.TryGetValue(playerId, out var player))
                    {
                        player.Name = info.PlayerName;
                        player.SteamId = info.SteamId;

                        // Broadcast updated player list to all
                        foreach (var p in _netManager!.ConnectedPeerList)
                        {
                            SendPlayerList(p);
                        }
                    }
                }
            }
            else
            {
                // Client received host's info - this includes our assigned ID
                _localPlayerId = info.PlayerId;

                // Add self
                var self = new ConnectedPlayer
                {
                    Id = _localPlayerId,
                    Name = _localPlayerName,
                    Peer = null
                };
                _players[_localPlayerId] = self;

                // Add host
                var host = new ConnectedPlayer
                {
                    Id = 1, // Host is always ID 1
                    Name = info.PlayerName,
                    Peer = peer
                };
                _players[1] = host;
            }
        }

        private void HandlePlayerPosition(NetPacketReader reader)
        {
            var pos = new PlayerPosition();
            pos.Deserialize(reader);

            if (_players.TryGetValue(pos.PlayerId, out var player))
            {
                player.X = pos.X;
                player.Y = pos.Y;
                player.Z = pos.Z;
                player.Rotation = pos.Rotation;
                player.State = pos.State;
                player.LastUpdate = DateTime.UtcNow;

                OnPlayerPositionReceived?.Invoke(pos.PlayerId, pos.X, pos.Y, pos.Z, pos.Rotation, pos.State);
            }

            // Host forwards to other clients
            if (_isHost && _netManager != null)
            {
                _writer.Reset();
                pos.Serialize(_writer);
                foreach (var peer in _netManager.ConnectedPeerList)
                {
                    if (!_peerToPlayerId.TryGetValue(peer, out int peerId) || peerId != pos.PlayerId)
                    {
                        peer.Send(_writer, DeliveryMethod.Sequenced);
                    }
                }
            }
        }

        private void HandlePlayerList(NetPacketReader reader)
        {
            var list = new PlayerList();
            list.Deserialize(reader);

            // Client: update player list from host
            if (!_isHost && list.Players != null)
            {
                foreach (var info in list.Players)
                {
                    if (!_players.ContainsKey(info.PlayerId))
                    {
                        var player = new ConnectedPlayer
                        {
                            Id = info.PlayerId,
                            Name = info.PlayerName,
                            SteamId = info.SteamId
                        };
                        _players[info.PlayerId] = player;
                        OnPlayerJoined?.Invoke(player);
                    }
                    else
                    {
                        _players[info.PlayerId].Name = info.PlayerName;
                        _players[info.PlayerId].SteamId = info.SteamId;
                    }
                }
            }
        }

        private readonly List<byte[]> _worldStateChunks = new List<byte[]>();
        private int _expectedWorldChunks = 0;

        private void HandleWorldStateChunk(NetPacketReader reader)
        {
            var chunk = new WorldStateChunk();
            chunk.Deserialize(reader);

            // Initialize chunk storage
            if (chunk.ChunkIndex == 0)
            {
                _worldStateChunks.Clear();
                _expectedWorldChunks = chunk.TotalChunks;
                for (int i = 0; i < chunk.TotalChunks; i++)
                    _worldStateChunks.Add(Array.Empty<byte>());
            }

            // Store chunk
            if (chunk.ChunkIndex < _worldStateChunks.Count)
            {
                _worldStateChunks[chunk.ChunkIndex] = chunk.Data;
            }

            // Check if complete
            bool complete = _worldStateChunks.Count == _expectedWorldChunks;
            for (int i = 0; i < _worldStateChunks.Count && complete; i++)
            {
                if (_worldStateChunks[i].Length == 0) complete = false;
            }

            if (complete)
            {
                // Reassemble world state
                int totalSize = 0;
                foreach (var c in _worldStateChunks) totalSize += c.Length;

                byte[] worldData = new byte[totalSize];
                int offset = 0;
                foreach (var c in _worldStateChunks)
                {
                    Array.Copy(c, 0, worldData, offset, c.Length);
                    offset += c.Length;
                }

                _worldStateChunks.Clear();
                OnWorldStateReceived?.Invoke(worldData);
            }
        }

        private void HandlePlayerLeft(NetPacketReader reader)
        {
            var packet = new PlayerLeft();
            packet.Deserialize(reader);

            _players.Remove(packet.PlayerId);
            OnPlayerLeft?.Invoke(packet.PlayerId);
        }

        public void OnNetworkReceiveUnconnected(System.Net.IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            reader.Recycle();
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            if (_isHost)
            {
                if (_netManager!.ConnectedPeersCount < 8) // Max 8 players
                    request.Accept();
                else
                    request.Reject();
            }
            else
            {
                request.Reject();
            }
        }
    }
}
