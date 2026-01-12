using Digi.NetworkLib;
using Sandbox.ModAPI;
using System;
using VRage.Game.Components;

namespace ZoneControl.NotificationBlock
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    internal class NotificationSession : MySessionComponentBase
    {
        public const long DefaultRefreshPeriod = 30 * 60;

        public static NotificationSession Instance;

        public const ushort NetworkId = (ushort)(3616581249 % ushort.MaxValue); //Steam number
        public Network Net;

        private long nextFrame;
        private NotificationPacket notificationPacket;

        public override void LoadData()
        {
            Instance = this;
            Log.Msg("Notification LoadData...........");

            Net = new Network(NetworkId, ModContext.ModName);

            Net.ExceptionHandler = (e) => Log.Msg(e.ToString());
            Net.ErrorHandler = (msg) => Log.Msg(msg);

            Net.SerializeTest = true;
            notificationPacket = new NotificationPacket();

            NotificationPacket.OnReceive += NotificationPacket_OnReceive
                ;
            /*            if (MyAPIGateway.Session.IsServer)
                            LoadDataOnHost();
                        else
                            LoadDataOnClient();*/
        }

        private void NotificationPacket_OnReceive(NotificationPacket packet, ref PacketInfo packetInfo, ulong senderSteamId)
        {

            string msg = $"[Example] Received {packet.GetType().Name}: text={packet.CurrentSpawns?.SpawnCounter})";
            Log.Msg(msg);

            if (MyAPIGateway.Session.Player != null)
            {
                MyAPIGateway.Utilities.ShowNotification(msg, 5000);
            }


            // to see how this works in practice, try it in both singleplayer (you're the server) and as a MP client in a dedicated server (you can start one from steam tools).
            packetInfo.Relay = RelayMode.ToEveryone;
        }

        protected override void UnloadData()
        {
            try
            {

                Net?.Dispose();
                Net = null;

                NotificationPacket.OnReceive -= NotificationPacket_OnReceive;
                Instance = null;

            }
            catch (Exception e)
            {
                Log.Msg($"Error in UnloadData\n{e.ToString()}");
            }
        }


        public override void BeforeStart()
        {
            //Log.Msg("BeforeStart");
            base.BeforeStart();
            if (MyAPIGateway.Session.IsServer)
            {
                Log.Msg("Notification Before Start Server");
            }

        }

        public override void UpdateAfterSimulation()
        {
            var currentFrame = MyAPIGateway.Session.GameplayFrameCounter;
            if (nextFrame > currentFrame)
                return;
            nextFrame = currentFrame + DefaultRefreshPeriod;

            if (MyAPIGateway.Session.IsServer)
                UpdateAfterSimulationHost();
            if (!MyAPIGateway.Utilities.IsDedicated)
                UpdateAfterSimulationClient();
        }


        private void UpdateAfterSimulationHost()
        {
            Log.Msg("Tick Host");
            ;
            //notificationPacket.Setup();
            //Net.SendToServer(notificationPacket);
        }

        private void UpdateAfterSimulationClient()
        {
            Log.Msg("Tick Client");
        }

    }
}
