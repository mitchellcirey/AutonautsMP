using System;
using LiteNetLib;
using LiteNetLib.Utils;

namespace AutonautsMP.Network
{
    /// <summary>
    /// Bridge class that wraps all LiteNetLib functionality.
    /// Loaded dynamically by the main assembly - never referenced directly.
    /// </summary>
    public class NetworkBridge : INetEventListener
    {
        private NetManager? _netManager;
        private NetPeer? _remotePeer;
        private bool _isHost;
        private string _statusMessage = "Not connected";
        private readonly NetDataWriter _writer = new NetDataWriter();

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

        public string GetStatus()
        {
            if (_netManager == null) return _statusMessage;

            int peerCount = _netManager.ConnectedPeersCount;
            if (_isHost)
            {
                return $"Hosting | {peerCount} connected";
            }
            else if (peerCount > 0)
            {
                return "Connected to host!";
            }
            return _statusMessage;
        }

        private void Stop()
        {
            _remotePeer = null;
            _netManager?.Stop();
            _netManager = null;
        }

        // INetEventListener implementation
        public void OnPeerConnected(NetPeer peer)
        {
            _remotePeer = peer;
            if (_isHost)
            {
                _statusMessage = "Client connected!";
            }
            else
            {
                _statusMessage = "Connected to host!";
            }
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            _remotePeer = null;
            _statusMessage = "Disconnected: " + disconnectInfo.Reason;
        }

        public void OnNetworkError(System.Net.IPEndPoint endPoint, System.Net.Sockets.SocketError socketError)
        {
            _statusMessage = "Network error: " + socketError;
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            // Handle incoming packets here
            reader.Recycle();
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
