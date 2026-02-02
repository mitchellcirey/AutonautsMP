using System;
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

        // Ping/Pong
        Ping = 100,
        Pong = 101
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
}
