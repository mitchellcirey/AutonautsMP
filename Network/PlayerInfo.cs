using System;

namespace AutonautsMP.Network
{
    /// <summary>
    /// Information about a connected player.
    /// </summary>
    public class PlayerInfo
    {
        public int ConnectionId { get; set; }
        public string Name { get; set; }
        public int Ping { get; set; }
        public DateTime LastPingTime { get; set; }
        public bool IsHost { get; set; }
        
        public PlayerInfo(int connectionId, string name, bool isHost = false)
        {
            ConnectionId = connectionId;
            Name = name;
            Ping = 0;
            LastPingTime = DateTime.UtcNow;
            IsHost = isHost;
        }
    }
}
