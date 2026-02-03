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

        // World Snapshot messages (Phase 4)
        SnapshotStart = 70,     // Host -> Client: begin snapshot transfer
        SnapshotChunk = 71,     // Host -> Client: chunk of save data
        SnapshotComplete = 72,  // Host -> Client: transfer complete
        SnapshotAck = 73,       // Client -> Host: acknowledge receipt
        SnapshotError = 74,     // Either direction: transfer failed

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

        #region Snapshot Messages

        /// <summary>
        /// Build a snapshot start message (host to client).
        /// Format: [MessageType:1][TotalSize:8][ChunkCount:4][SaveName:string]
        /// </summary>
        public static byte[] BuildSnapshotStart(long totalSize, int chunkCount, string saveName)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)MessageType.SnapshotStart);
                writer.Write(totalSize);
                writer.Write(chunkCount);
                writer.Write(saveName ?? "");
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Build a snapshot chunk message (host to client).
        /// Format: [MessageType:1][ChunkIndex:4][Data:bytes]
        /// </summary>
        public static byte[] BuildSnapshotChunk(int chunkIndex, byte[] chunkData)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)MessageType.SnapshotChunk);
                writer.Write(chunkIndex);
                writer.Write(chunkData.Length);
                writer.Write(chunkData);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Build a snapshot complete message (host to client).
        /// Format: [MessageType:1][Checksum:4]
        /// </summary>
        public static byte[] BuildSnapshotComplete(uint checksum)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)MessageType.SnapshotComplete);
                writer.Write(checksum);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Build a snapshot acknowledgment message (client to host).
        /// Format: [MessageType:1][Success:1]
        /// </summary>
        public static byte[] BuildSnapshotAck(bool success)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)MessageType.SnapshotAck);
                writer.Write(success);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Build a snapshot error message (either direction).
        /// Format: [MessageType:1][ErrorMessage:string]
        /// </summary>
        public static byte[] BuildSnapshotError(string errorMessage)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write((byte)MessageType.SnapshotError);
                writer.Write(errorMessage ?? "Unknown error");
                return ms.ToArray();
            }
        }

        #endregion

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

    #region Snapshot Data Structures

    /// <summary>
    /// Parsed snapshot start data.
    /// </summary>
    public struct SnapshotStartData
    {
        public long TotalSize;
        public int ChunkCount;
        public string SaveName;

        public static SnapshotStartData Read(byte[] data)
        {
            using (var reader = NetworkMessages.CreateReader(data))
            {
                return new SnapshotStartData
                {
                    TotalSize = reader.ReadInt64(),
                    ChunkCount = reader.ReadInt32(),
                    SaveName = reader.ReadString()
                };
            }
        }
    }

    /// <summary>
    /// Parsed snapshot chunk data.
    /// </summary>
    public struct SnapshotChunkData
    {
        public int ChunkIndex;
        public byte[] Data;

        public static SnapshotChunkData Read(byte[] data)
        {
            using (var reader = NetworkMessages.CreateReader(data))
            {
                int chunkIndex = reader.ReadInt32();
                int length = reader.ReadInt32();
                byte[] chunkData = reader.ReadBytes(length);
                
                return new SnapshotChunkData
                {
                    ChunkIndex = chunkIndex,
                    Data = chunkData
                };
            }
        }
    }

    /// <summary>
    /// Parsed snapshot complete data.
    /// </summary>
    public struct SnapshotCompleteData
    {
        public uint Checksum;

        public static SnapshotCompleteData Read(byte[] data)
        {
            using (var reader = NetworkMessages.CreateReader(data))
            {
                return new SnapshotCompleteData
                {
                    Checksum = reader.ReadUInt32()
                };
            }
        }
    }

    /// <summary>
    /// Parsed snapshot acknowledgment data.
    /// </summary>
    public struct SnapshotAckData
    {
        public bool Success;

        public static SnapshotAckData Read(byte[] data)
        {
            using (var reader = NetworkMessages.CreateReader(data))
            {
                return new SnapshotAckData
                {
                    Success = reader.ReadBoolean()
                };
            }
        }
    }

    /// <summary>
    /// Parsed snapshot error data.
    /// </summary>
    public struct SnapshotErrorData
    {
        public string ErrorMessage;

        public static SnapshotErrorData Read(byte[] data)
        {
            using (var reader = NetworkMessages.CreateReader(data))
            {
                return new SnapshotErrorData
                {
                    ErrorMessage = reader.ReadString()
                };
            }
        }
    }

    #endregion
}
