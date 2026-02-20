using Digi.NetworkLib;
using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using ZoneControl.Spawner;

namespace ZoneControl.NetworkLayer
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    internal class Networking : MySessionComponentBase
    {
        public const ushort NetworkId = (ushort)(3490215737 % ushort.MaxValue);

        public static Networking Instance;
        public Network Net;

        private CmdMsgPacket cmdMsgPacket;
        private GPSbuttonPacket gpsBtnPacket;

        public override void LoadData()
        {
            Log.Msg("LoadData");
            Instance = this;

            Net = new Network(NetworkId, ModContext.ModName);

            // If you want errors to use your logger then you can do:
            //Net.ExceptionHandler = (e) => Log.Error(e);
            //Net.ErrorHandler = (msg) => Log.Error(msg);

            // To test if serialization works in singleplayer when using SendToServer().
            Net.SerializeTest = false;

            cmdMsgPacket = new CmdMsgPacket();
            gpsBtnPacket = new GPSbuttonPacket();

            CmdMsgPacket.OnReceive += CmdMsgPacket_OnReceive;
            GPSbuttonPacket.OnReceive += GPSbuttonPacket_OnReceive;

            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                Log.Msg("LoadData Client");
                MyAPIGateway.Utilities.MessageEntered += Utilities_MessageEntered;
            }
        }

        public void SendGPSbuttonPressed(int button, long identityId)
        {
            if (Log.Debug) Log.Msg($"SendGPSbuttonPressed button={button}, identityId={identityId}");
            gpsBtnPacket.Setup(button, identityId);
            Net.SendToServer(gpsBtnPacket);
        }

        private void GPSbuttonPacket_OnReceive(GPSbuttonPacket packet, ref PacketInfo packetInfo, ulong senderSteamId)
        {
            if (Log.Debug) Log.Msg($"GPSbuttonPacket_OnReceive button={packet.Button} identityId={packet.IdentityId}");
            SpawnerSession.Instance.AddPlayerGPS(packet.Button, packet.IdentityId);
        }

        private void Utilities_MessageEntered(string messageText, ref bool sendToOthers)
        {

            if (Log.Debug) Log.Msg($"MessageEntered local msg={messageText}");

            if (messageText.StartsWith("/ZoneControl"))
            {
                cmdMsgPacket.Setup(CmdMsgType.ZoneControl, messageText);
                Net.SendToServer(cmdMsgPacket);
                sendToOthers = false;
                return;
            }
            else if (messageText.StartsWith("/ZoneSpawner"))
            {
                cmdMsgPacket.Setup(CmdMsgType.ZoneSpawner, messageText);
                Net.SendToServer(cmdMsgPacket);
                sendToOthers = false;
                return;
            }
            /*            else if (messageText.StartsWith("/Test"))
                        {
                            cmdMsgPacket.Setup(CmdMsgType.None, messageText);
                            Net.SendToServer(cmdMsgPacket);
                            return;
                        }*/

            return;
        }

        private void CmdMsgPacket_OnReceive(CmdMsgPacket packet, ref PacketInfo packetInfo, ulong senderSteamId)
        {
            if (Log.Debug) Log.Msg($"CmdMsgPacket_OnReceive steamId={senderSteamId} type={packet.MsgType} args.Count={packet.Args.Count} msg={packet.Msg}");

            IMyPlayer player = null;
            if (senderSteamId != 0)
            {
                player = MyAPIGateway.Players.TryGetIdentityId(MyAPIGateway.Players.TryGetIdentityId(senderSteamId));
                if (player == null) //belt and braces
                    return;
            }
            MyVisualScriptLogicProvider.SendChatMessageColored(packet.Msg, Color.Green, player.DisplayName, player.IdentityId);

            switch (packet.MsgType)
            {
                case CmdMsgType.None:
                    {
                        Log.Msg($"MsgType={packet.MsgType} args.Count={packet.Args.Count}");
                        break;
                    }
                case CmdMsgType.ZoneControl:
                    {
                        ZonesSession.Instance.CmdQueueEnqueue(new ZonesSession.CmdMsg() { Player = player, Packet = packet });
                        break;
                    }
                case CmdMsgType.ZoneSpawner:
                    {
                        SpawnerSession.Instance.CmdQueueEnqueue(new ZonesSession.CmdMsg() { Player = player, Packet = packet });
                        break;
                    }
            }
        }

        protected override void UnloadData()
        {
            try
            {
                Log.Msg("UnloadData");

                Net?.Dispose();
                Net = null;

                if (!MyAPIGateway.Utilities.IsDedicated)
                    MyAPIGateway.Utilities.MessageEntered -= Utilities_MessageEntered;
            }
            catch (Exception e)
            {
                MyLog.Default.Error(e.ToString());
            }
            finally
            {
                Instance = null;
            }
        }

    }
}
