using ProtoBuf;
using ZoneControl.Networking;

namespace Digi.NetworkLib
{
    [ProtoInclude(10, typeof(CmdMsgPacket))]
    //[ProtoInclude(11, typeof(SomeOtherPacketClass))]
    //[ProtoInclude(12, typeof(Etc...))]
    public abstract partial class PacketBase
    {
    }
}