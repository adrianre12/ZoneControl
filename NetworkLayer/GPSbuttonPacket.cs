using Digi.NetworkLib;
using ProtoBuf;

namespace ZoneControl.NetworkLayer
{
    [ProtoContract]
    public class GPSbuttonPacket : PacketBase
    {
        public GPSbuttonPacket() { }

        [ProtoMember(1)]
        public int Button;
        [ProtoMember(2)]
        public long IdentityId;

        public void Setup(int button, long identityId)
        {
            Button = button;
            IdentityId = identityId;
        }

        // Alternative way of handling the data elsewhere.
        // Or you can handle it in the Received() method below and remove this event, up to you.
        public static event ReceiveDelegate<GPSbuttonPacket> OnReceive;

        public override void Received(ref PacketInfo packetInfo, ulong senderSteamId)
        {
            OnReceive?.Invoke(this, ref packetInfo, senderSteamId);
        }
    }
}
