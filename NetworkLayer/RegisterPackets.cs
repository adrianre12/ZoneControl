using ProtoBuf;
using ZoneControl.NetworkLayer;

namespace Digi.NetworkLib
{
    [ProtoInclude(10, typeof(CmdMsgPacket))]
    [ProtoInclude(11, typeof(GPSbuttonPacket))]
    //[ProtoInclude(12, typeof(Etc...))]
    public abstract partial class PacketBase
    {
    }
}