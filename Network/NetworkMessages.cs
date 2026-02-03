using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AutonautsMP.Network
{
    /// <summary>
    /// Message types for network communication.
    /// </summary>
    public enum MessageType : byte
    {
        // Connection messages
        PlayerJoin = 1,
        PlayerLeave = 2,
        PlayerList = 3,

        // Chat messages
        ChatMessage = 10,

        // Game state messages
        GameStateSync = 20,
        GameStateUpdate = 21,

        // Entity messages
        EntitySpawn = 30,
        EntityDespawn = 31,
        EntityUpdate = 32,

        // Bot/Worker messages
        BotCommand = 40,
        BotStateSync = 41,

        // World messages
        TileUpdate = 50,
        ObjectPlaced = 51,
        ObjectRemoved = 52,

        // Player sync messages
        PlayerTransform = 60,   // Position/rotation update

        // Ping/Pong
        Ping = 100,
        Pong = 101,

        // Test sync messages (for network debugging)
        TestIncrement = 200,    // Client → Host: request counter increment
        TestBroadcast = 201     // Host → All: broadcast new counter value
    }

    /// <summary>
    /// Helper class for building and reading network messages.
    /// Uses simple binary format: [MessageType:1byte][Data...]
    /// </summary>
    public static class NetworkMessages
    {
        /// <summary>
        /// Build a player join message.
        /// </summary>
        public static byte[] BuildPlayerJoin(string playerName)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)MessageType.PlayerJoin);
                writer.Write(playerName);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Build a chat message.
        /// </summary>
        public static byte[] BuildChatMessage(string sender, string message)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)MessageType.ChatMessage);
                writer.Write(sender);
                writer.Write(message);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Build a ping message.
        /// </summary>
        public static byte[] BuildPing(long timestamp)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)MessageType.Ping);
                writer.Write(timestamp);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Build a pong response.
        /// </summary>
        public static byte[] BuildPong(long originalTimestamp)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)MessageType.Pong);
                writer.Write(originalTimestamp);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Build a test increment request (client to host).
        /// </summary>
        public static byte[] BuildTestIncrement()
        {
            return new byte[] { (byte)MessageType.TestIncrement };
        }

        /// <summary>
        /// Build a test broadcast message (host to all clients).
        /// </summary>
        public static byte[] BuildTestBroadcast(int counterValue)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)MessageType.TestBroadcast);
                writer.Write(counterValue);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Build a player transform update message.
        /// </summary>
        public static byte[] BuildPlayerTransform(int playerId, float x, float y, float z, float rotY)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)MessageType.PlayerTransform);
                writer.Write(playerId);
                writer.Write(x);
                writer.Write(y);
                writer.Write(z);
                writer.Write(rotY);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Build a player list message (host to clients).
        /// Format: [PlayerList:1][Count:4][Player1...PlayerN]
        /// Each player: [ConnectionId:4][IsHost:1][Name:string][Ping:4]
        /// </summary>
        public static byte[] BuildPlayerList(List<PlayerListEntry> players)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)MessageType.PlayerList);
                writer.Write(players.Count);
                
                foreach (var player in players)
                {
                    writer.Write(player.ConnectionId);
                    writer.Write(player.IsHost);
                    writer.Write(player.Name);
                    writer.Write(player.Ping);
                }
                
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Read a player list message.
        /// </summary>
        public static List<PlayerListEntry> ReadPlayerList(byte[] data)
        {
            var players = new List<PlayerListEntry>();
            
            using (var reader = CreateReader(data))
            {
                int count = reader.ReadInt32();
                
                for (int i = 0; i < count; i++)
                {
                    players.Add(new PlayerListEntry
                    {
                        ConnectionId = reader.ReadInt32(),
                        IsHost = reader.ReadBoolean(),
                        Name = reader.ReadString(),
                        Ping = reader.ReadInt32()
                    });
                }
            }
            
            return players;
        }

        /// <summary>
        /// Read message type from packet.
        /// </summary>
        public static MessageType ReadType(byte[] data)
        {
            if (data == null || data.Length < 1)
                return 0;
            return (MessageType)data[0];
        }

        /// <summary>
        /// Create a BinaryReader for reading message data (skips type byte).
        /// </summary>
        public static BinaryReader CreateReader(byte[] data)
        {
            var ms = new MemoryStream(data);
            var reader = new BinaryReader(ms);
            reader.ReadByte(); // Skip message type
            return reader;
        }
    }

    /// <summary>
    /// Parsed player join data.
    /// </summary>
    public struct PlayerJoinData
    {
        public string PlayerName;

        public static PlayerJoinData Read(byte[] data)
        {
            using (var reader = NetworkMessages.CreateReader(data))
            {
                return new PlayerJoinData
                {
                    PlayerName = reader.ReadString()
                };
            }
        }
    }

    /// <summary>
    /// Parsed chat message data.
    /// </summary>
    public struct ChatMessageData
    {
        public string Sender;
        public string Message;

        public static ChatMessageData Read(byte[] data)
        {
            using (var reader = NetworkMessages.CreateReader(data))
            {
                return new ChatMessageData
                {
                    Sender = reader.ReadString(),
                    Message = reader.ReadString()
                };
            }
        }
    }

    /// <summary>
    /// Parsed player transform data.
    /// </summary>
    public struct PlayerTransformData
    {
        public int PlayerId;
        public float X;
        public float Y;
        public float Z;
        public float RotY;

        public static PlayerTransformData Read(byte[] data)
        {
            using (var reader = NetworkMessages.CreateReader(data))
            {
                return new PlayerTransformData
                {
                    PlayerId = reader.ReadInt32(),
                    X = reader.ReadSingle(),
                    Y = reader.ReadSingle(),
                    Z = reader.ReadSingle(),
                    RotY = reader.ReadSingle()
                };
            }
        }
    }

    /// <summary>
    /// Entry in the player list message.
    /// </summary>
    public struct PlayerListEntry
    {
        public int ConnectionId;
        public bool IsHost;
        public string Name;
        public int Ping;
    }
}
