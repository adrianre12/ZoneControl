using Digi.NetworkLib;
using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace ZoneControl.Networking
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    internal class Networking : MySessionComponentBase
    {
        public const ushort NetworkId = (ushort)(3490215737 % ushort.MaxValue);

        public static Networking Instance;
        public Network Net;

        private CmdMsgPacket cmdMsgPacket;

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

            CmdMsgPacket.OnReceive += CmdMsgPacket_OnReceive;

            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                Log.Msg("LoadData Client");
                MyAPIGateway.Utilities.MessageEntered += Utilities_MessageEntered;
            }
            //if (MyAPIGateway.Session.IsServer)
            //    MyAPIGateway.Utilities.MessageRecieved += Utilities_MessageRecieved;

        }

        public override void BeforeStart()
        {
            Log.Msg("BeforeStart");
        }

        private void Utilities_MessageEntered(string messageText, ref bool sendToOthers)
        {
            sendToOthers = false;
            Log.Msg($"MessageEntered local msg={messageText}");

            if (messageText.StartsWith("/ZoneControl"))
            {
                cmdMsgPacket.Setup(CmdMsgType.ZoneControl, messageText);
                Net.SendToServer(cmdMsgPacket);
                return;
            }
            else if (messageText.StartsWith("/ZoneSpawner"))
            {
                cmdMsgPacket.Setup(CmdMsgType.ZoneSpawner, messageText);
                Net.SendToServer(cmdMsgPacket);
                return;
            }
            else if (messageText.StartsWith("/Test"))
            {
                cmdMsgPacket.Setup(CmdMsgType.None, messageText);
                Net.SendToServer(cmdMsgPacket);
                return;
            }

            return;
        }

        private void CmdMsgPacket_OnReceive(CmdMsgPacket packet, ref PacketInfo packetInfo, ulong senderSteamId)
        {
            Log.Msg($"CmdMsgPacket_OnReceive steamId={senderSteamId} type={packet.MsgType} args.Count={packet.Args.Count} msg={packet.Msg}");
            long IdentityId = MyAPIGateway.Players.TryGetIdentityId(senderSteamId);

            IMyPlayer player = null;
            if (senderSteamId != 0)
            {
                player = MyAPIGateway.Players.TryGetIdentityId(MyAPIGateway.Players.TryGetIdentityId(senderSteamId));
                if (player == null) //belt and braces
                    return;


                if (player.PromoteLevel < MyPromoteLevel.Admin)
                {
                    Log.Msg($"Non Admin player {player.DisplayName} tried to run command {packet.Msg}", player.IdentityId);
                    return;
                }
            }

            MyVisualScriptLogicProvider.SendChatMessageColored(packet.Msg, Color.Yellow, player.DisplayName, IdentityId);

            switch (packet.MsgType)
            {
                case CmdMsgType.None:
                    {
                        Log.Msg($"MsgType={packet.MsgType} args.Count={packet.Args.Count}");
                        break;
                    }
                case CmdMsgType.ZoneControl:
                    {
                        // cmdQueue.Enqueue(new CmdMsg() { Player = player, Msg = msg });

                        break;
                    }
                case CmdMsgType.ZoneSpawner:
                    {
                        //cmdQueue.Enqueue(new CmdMsg() { Player = player, Msg = msg });

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
                //if (MyAPIGateway.Session.IsServer)
                //    MyAPIGateway.Utilities.MessageRecieved -= Utilities_MessageRecieved;
            }
            catch (Exception e)
            {
                MyLog.Default.Error(e.ToString());
            }
            finally
            {
                Instance = null; // important for avoiding this instance and all its references to remain allocated in memory
            }
        }

        public override void UpdateBeforeSimulation()
        {
            // executed every tick, 60 times a second, before physics simulation and only if game is not paused.
        }

    }
}
