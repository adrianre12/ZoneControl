using ProtoBuf;
using ZoneControl.Spawner;

namespace Digi.NetworkLib
{
    [ProtoInclude(10, typeof(CurrentSpawnsPacket))]
    //[ProtoInclude(11, typeof(SomeOtherPacketClass))]
    //[ProtoInclude(12, typeof(Etc...))]
    public abstract partial class PacketBase
    {
    }
}
