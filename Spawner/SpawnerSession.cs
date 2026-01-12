using Digi.NetworkLib;
using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using static ZoneControl.Utils;

namespace ZoneControl.Spawner
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    internal partial class SpawnerSession : MySessionComponentBase
    {
        public const long DefaultRefreshPeriod = 30 * 60;
        public const ushort NetworkId = (ushort)(3616581249 % ushort.MaxValue); //Steam number
        const string VariableId = nameof(ZoneSpawner);
        const long DateTimeTicksPerHour = 36000000000L;
        const long DateTimeTicksPerMin = 600000000L;

        public static SpawnerSession Instance;

        public Network Net;

        internal CurrentSpawnsPacket CurrentSpawns = new CurrentSpawnsPacket();
        internal SpawnerConfig Config;


        private int updatePeriodMins;
        private int urgentMsgPeriodMins;
        private int warnMsgPeriodMins;
        private long dateTimeTicksUrgentMsgPeriod;
        private long dateTimeTicksWarnMsgPeriod;
        private int defaultRefreshPeriodTicks;

        private long nextFrame;

        private int updateRndMultiplier = 0;
        private int nextRefreshFrame = 1800; // 30s, frame counter should be 0 at startup
        private List<PrefabInfoInternal> prefabs = new List<PrefabInfoInternal>(); //all prefabs with weighting.

        private long factionOwnerId;

        public override void LoadData()
        {
            Instance = this;
            Log.Msg("Notification LoadData...........");

            Net = new Network(NetworkId, ModContext.ModName);

            Net.ExceptionHandler = (e) => Log.Msg(e.ToString());
            Net.ErrorHandler = (msg) => Log.Msg(msg);

            Net.SerializeTest = true;

            CurrentSpawnsPacket.OnReceive += NotificationPacket_OnReceive;

            if (MyAPIGateway.Session.IsServer)
                LoadDataHost();
            if (!MyAPIGateway.Utilities.IsDedicated)
                LoadDataClient();
        }

        private void LoadDataHost()
        {
            Config = SpawnerConfig.LoadConfig();
            if (Config.UpdatePeriodMins != null && int.TryParse(Config.UpdatePeriodMins, out updatePeriodMins))
                updatePeriodMins = Math.Max(updatePeriodMins, 1);
            else
                updatePeriodMins = 5;
            Log.Msg($"Spawner UpdatePeriodMins={updatePeriodMins}");

            urgentMsgPeriodMins = 2 * updatePeriodMins;
            warnMsgPeriodMins = 30;
            dateTimeTicksUrgentMsgPeriod = urgentMsgPeriodMins * DateTimeTicksPerMin;
            dateTimeTicksWarnMsgPeriod = warnMsgPeriodMins * DateTimeTicksPerMin;
            defaultRefreshPeriodTicks = 60 * 60 * updatePeriodMins;

            updateRndMultiplier = 60 / (updatePeriodMins * Math.Max(Math.Min(Config.SpawnRateMultiplier, 60 / updatePeriodMins), 0));
            double totalWeighting = 0;

            Log.Msg($"Spawner Enabled={Config.Enabled}");

            MyVisualScriptLogicProvider.PrefabSpawnedDetailed += PrefabSpawnedDetailed;
        }

        private void LoadDataClient()
        {
            //throw new NotImplementedException();
        }


        public override void BeforeStart()
        {
            //Log.Msg("BeforeStart");
            base.BeforeStart();
            if (MyAPIGateway.Session.IsServer)
                BeforeStartHost();

            if (!MyAPIGateway.Utilities.IsDedicated)
                BeforeStartClient();

        }

        private void BeforeStartHost()
        {
            Log.Msg("Spawner Before Start Host");
            factionOwnerId = FindFactionId(Config.FactionTag);

            double totalWeighting = 0;
            foreach (var sector in Config.Sectors)
            {
                SectorInfoInternal sectorInfo = new SectorInfoInternal(sector);
                foreach (var prefab in sector.Prefabs)
                {
                    PrefabInfoInternal prefabInfo = new PrefabInfoInternal(prefab, sectorInfo);
                    prefabs.Add(prefabInfo);
                    totalWeighting += prefabInfo.Weighting;
                }
            }

            foreach (PrefabInfoInternal pi in prefabs)
            {
                pi.WeightNorm = pi.Weighting / totalWeighting;
                Log.Msg($"Prefab loaded {pi.Subtype} Sector={pi.SectorInfo.UniqueName} WeightNorm={pi.WeightNorm}");
            }

            string variableStr;
            if (MyAPIGateway.Utilities.GetVariable<string>(VariableId, out variableStr))
            {
                try
                {
                    CurrentSpawns = MyAPIGateway.Utilities.SerializeFromBinary<CurrentSpawnsPacket>(Convert.FromBase64String(variableStr));
                }
                catch (Exception ex)
                {
                    Log.Msg($"Error: Failed to deseralize currentSpawns\n{ex.ToString()}");
                    CurrentSpawns = new CurrentSpawnsPacket();
                }

                for (int i = CurrentSpawns.Spawns.Count - 1; i >= 0; --i)
                {
                    var spawn = CurrentSpawns.Spawns[i];
                    if (spawn.EntityId < 0)
                    {
                        Log.Msg($"currentSpawn EntityId not set, removing '{spawn.Name}'");
                        CurrentSpawns.Spawns.Remove(spawn);
                        continue;
                    }
                    spawn.ZoneId = -1;
                    Log.Msg($"currentSpawn loaded '{spawn.Name}'");
                }
            }

        }

        private void BeforeStartClient()
        {
            //throw new NotImplementedException();
        }

        protected override void UnloadData()
        {
            try
            {

                Net?.Dispose();
                Net = null;

                CurrentSpawnsPacket.OnReceive -= NotificationPacket_OnReceive;

                if (MyAPIGateway.Session.IsServer)
                {
                    MyVisualScriptLogicProvider.PrefabSpawnedDetailed -= PrefabSpawnedDetailed;

                }

                //if (!MyAPIGateway.Utilities.IsDedicated)
                //{
                //}



                Instance = null;

            }
            catch (Exception e)
            {
                Log.Msg($"Error in UnloadData\n{e.ToString()}");
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

        private void NotificationPacket_OnReceive(CurrentSpawnsPacket packet, ref PacketInfo packetInfo, ulong senderSteamId)
        {

            string msg = $"[Example] Received {packet.GetType().Name}: text={packet.SpawnCounter})";
            Log.Msg(msg);

            if (MyAPIGateway.Session.Player != null)
            {
                MyAPIGateway.Utilities.ShowNotification(msg, 5000);
            }


            // to see how this works in practice, try it in both singleplayer (you're the server) and as a MP client in a dedicated server (you can start one from steam tools).
            packetInfo.Relay = RelayMode.ToEveryone;
        }

    }

}
