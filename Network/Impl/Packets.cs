using System;
using LiteNetLib.Utils;

namespace AutonautsMP.Network
{
    /// <summary>
    /// Packet type identifiers
    /// </summary>
    public enum PacketType : byte
    {
        PlayerInfo = 1,
        PlayerPosition = 2,
        PlayerList = 3,
        WorldStateChunk = 4,
        ObjectAction = 5,
        PlayerLeft = 6
    }

    /// <summary>
    /// Player information packet - sent on connect
    /// </summary>
    public struct PlayerInfo : INetSerializable
    {
        public int PlayerId;
        public string PlayerName;
        public ulong SteamId; // 0 if not using Steam

        public void Serialize(NetDataWriter writer)
        {
            writer.Put((byte)PacketType.PlayerInfo);
            writer.Put(PlayerId);
            writer.Put(PlayerName ?? "Player");
            writer.Put(SteamId);
        }

        public void Deserialize(NetDataReader reader)
        {
            PlayerId = reader.GetInt();
            PlayerName = reader.GetString();
            SteamId = reader.GetULong();
        }
    }

    /// <summary>
    /// Player position packet - sent frequently for real-time sync
    /// </summary>
    public struct PlayerPosition : INetSerializable
    {
        public int PlayerId;
        public float X;
        public float Y;
        public float Z;
        public float Rotation;
        public byte State; // Walking, idle, etc.

        public void Serialize(NetDataWriter writer)
        {
            writer.Put((byte)PacketType.PlayerPosition);
            writer.Put(PlayerId);
            writer.Put(X);
            writer.Put(Y);
            writer.Put(Z);
            writer.Put(Rotation);
            writer.Put(State);
        }

        public void Deserialize(NetDataReader reader)
        {
            PlayerId = reader.GetInt();
            X = reader.GetFloat();
            Y = reader.GetFloat();
            Z = reader.GetFloat();
            Rotation = reader.GetFloat();
            State = reader.GetByte();
        }
    }

    /// <summary>
    /// Player list packet - sent to new players
    /// </summary>
    public struct PlayerList : INetSerializable
    {
        public PlayerInfo[] Players;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put((byte)PacketType.PlayerList);
            writer.Put((byte)(Players?.Length ?? 0));
            if (Players != null)
            {
                foreach (var p in Players)
                {
                    writer.Put(p.PlayerId);
                    writer.Put(p.PlayerName ?? "Player");
                    writer.Put(p.SteamId);
                }
            }
        }

        public void Deserialize(NetDataReader reader)
        {
            int count = reader.GetByte();
            Players = new PlayerInfo[count];
            for (int i = 0; i < count; i++)
            {
                Players[i] = new PlayerInfo
                {
                    PlayerId = reader.GetInt(),
                    PlayerName = reader.GetString(),
                    SteamId = reader.GetULong()
                };
            }
        }
    }

    /// <summary>
    /// World state chunk - for initial sync
    /// </summary>
    public struct WorldStateChunk : INetSerializable
    {
        public int ChunkIndex;
        public int TotalChunks;
        public byte[] Data;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put((byte)PacketType.WorldStateChunk);
            writer.Put(ChunkIndex);
            writer.Put(TotalChunks);
            writer.PutBytesWithLength(Data ?? Array.Empty<byte>());
        }

        public void Deserialize(NetDataReader reader)
        {
            ChunkIndex = reader.GetInt();
            TotalChunks = reader.GetInt();
            Data = reader.GetBytesWithLength();
        }
    }

    /// <summary>
    /// Object action packet - for delta sync
    /// </summary>
    public struct ObjectAction : INetSerializable
    {
        public byte ActionType; // Create, Destroy, Move, etc.
        public int ObjectUID;
        public string ObjectType;
        public float X;
        public float Y;
        public string ExtraData; // JSON for complex data

        public void Serialize(NetDataWriter writer)
        {
            writer.Put((byte)PacketType.ObjectAction);
            writer.Put(ActionType);
            writer.Put(ObjectUID);
            writer.Put(ObjectType ?? "");
            writer.Put(X);
            writer.Put(Y);
            writer.Put(ExtraData ?? "");
        }

        public void Deserialize(NetDataReader reader)
        {
            ActionType = reader.GetByte();
            ObjectUID = reader.GetInt();
            ObjectType = reader.GetString();
            X = reader.GetFloat();
            Y = reader.GetFloat();
            ExtraData = reader.GetString();
        }
    }

    /// <summary>
    /// Player left notification
    /// </summary>
    public struct PlayerLeft : INetSerializable
    {
        public int PlayerId;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put((byte)PacketType.PlayerLeft);
            writer.Put(PlayerId);
        }

        public void Deserialize(NetDataReader reader)
        {
            PlayerId = reader.GetInt();
        }
    }
}
