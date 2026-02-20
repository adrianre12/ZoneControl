using Digi.NetworkLib;
using ProtoBuf;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ZoneControl.NetworkLayer
{

    public enum CmdMsgType
    {
        None = 0,
        ZoneControl,
        ZoneSpawner
    }

    [ProtoContract]
    public class CmdMsgPacket : PacketBase
    {
        const string regxArgs = " (?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))";

        public CmdMsgPacket() { } // Empty constructor required for deserialization

        [ProtoMember(1)]
        public CmdMsgType MsgType;
        [ProtoMember(2)]
        public string Msg;
        [ProtoMember(3)]
        public List<string> Args;

        public void Setup(CmdMsgType msgType, string msg)
        {
            MsgType = msgType;
            Msg = msg;
            Args = GetArgs(msg);
        }

        // Alternative way of handling the data elsewhere.
        // Or you can handle it in the Received() method below and remove this event, up to you.
        public static event ReceiveDelegate<CmdMsgPacket> OnReceive;

        public override void Received(ref PacketInfo packetInfo, ulong senderSteamId)
        {
            OnReceive?.Invoke(this, ref packetInfo, senderSteamId);
        }

        internal static List<string> GetArgs(string msg)
        {
            var parts = Regex.Split(msg, regxArgs);
            List<string> args = new List<string>();
            foreach (var part in parts)
            {
                string arg = part.Trim(new char[] { ' ', '"' });
                if (arg.Length == 0)
                    continue;

                args.Add(arg);
            }
            return args;
        }
    }
}