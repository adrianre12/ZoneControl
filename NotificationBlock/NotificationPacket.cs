using Digi.NetworkLib;
using ProtoBuf;
using static ZoneControl.ZoneSpawner;

namespace ZoneControl.NotificationBlock
{
    [ProtoContract]
    public class NotificationPacket : PacketBase
    {
        [ProtoMember(1)]
        public CurrentSpawns CurrentSpawns;

        public void Setup(CurrentSpawns currentSpawns)
        {
            CurrentSpawns = currentSpawns;
        }


        public static event ReceiveDelegate<NotificationPacket> OnReceive;

        public override void Received(ref PacketInfo packetInfo, ulong senderSteamId)
        {
            OnReceive?.Invoke(this, ref packetInfo, senderSteamId);
        }
    }

}
