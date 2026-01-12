using ProtoBuf;
using ZoneControl.NotificationBlock;

namespace Digi.NetworkLib
{
    [ProtoInclude(10, typeof(NotificationPacket))]
    //[ProtoInclude(11, typeof(SomeOtherPacketClass))]
    //[ProtoInclude(12, typeof(Etc...))]
    public abstract partial class PacketBase
    {
    }
}
