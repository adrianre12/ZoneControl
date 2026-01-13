using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using static ZoneControl.Utils;
using static ZoneControl.ZonesSession;

namespace ZoneControl.Spawner
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    internal partial class SpawnerSession : MySessionComponentBase
    {
        const string VariableId = nameof(SpawnerSession);
        const long DateTimeTicksPerHour = 36000000000L;
        const long DateTimeTicksPerMin = 600000000L;

        public static SpawnerSession Instance;

        private SpawnerConfig config;

        private int updatePeriodMins;
        private int urgentMsgPeriodMins;
        private int warnMsgPeriodMins;
        private long dateTimeTicksUrgentMsgPeriod;
        private long dateTimeTicksWarnMsgPeriod;
        private int defaultRefreshPeriodTicks;

        private int updateRndMultiplier = 0;
        private List<PrefabInfoInternal> prefabs = new List<PrefabInfoInternal>(); //all prefabs with weighting.

        private long factionOwnerId;
        private Queue<CmdMsg> cmdQueue = new Queue<CmdMsg>();

        private void Utilities_MessageEntered(string msg, ref bool sendToOthers)
        {
            //Log.Msg($"Recieved local msg={msg}");
            Utilities_MessageRecieved(0, msg);
        }

        private void Utilities_MessageRecieved(ulong steamId, string msg)
        {
            //Log.Msg($"Recieved steamId={steamId} msg={msg}");

            if (!msg.StartsWith("/ZoneSpawner"))
                return;

            IMyPlayer player = null;
            if (steamId != 0)
            {
                player = MyAPIGateway.Players.TryGetIdentityId(MyAPIGateway.Players.TryGetIdentityId(steamId));
                if (player == null) //belt and braces
                    return;


                if (player.PromoteLevel < MyPromoteLevel.Admin)
                {
                    Log.Msg($"Non Admin player {player.DisplayName} tried to run command {msg}", player.IdentityId);
                    return;
                }
            }

            cmdQueue.Enqueue(new CmdMsg() { Player = player, Msg = msg });
        }

        private void CommandHandler(CmdMsg cmdMsg)
        {
            long playerId = cmdMsg.Player?.IdentityId ?? 0;
            var args = cmdMsg.Msg.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (args.Length < 2)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Help:");
                sb.AppendLine("/ZoneSpawner Status");
                sb.AppendLine("   Lists the current status of the Spawner.");

                sb.AppendLine("/ZoneSpawner AddSpawn");
                sb.AppendLine("   Request an Anomaly spawn.");

                sb.AppendLine("/ZoneSpawner RemoveAllSpawns");
                sb.AppendLine("   Removes all current spawns and Anomalies.");

                sb.AppendLine("/ZoneSpawner SetSpawnCounter");
                sb.AppendLine("   Can only be run when the spawner is disabled in config.");


                sb.AppendLine("/ZoneControl");
                sb.AppendLine("   Commands for ZoneControl");



                Log.Msg(sb.ToString(), playerId);
                return;
            }

            Log.Msg($"Player {cmdMsg.Player?.DisplayName ?? "Local"} [{cmdMsg.Player?.IdentityId}] ran command {cmdMsg.Msg}");
            switch (args[1])
            {
                case "RemoveAllSpawns":
                    {
                        if (currentSpawns.Spawns.Count > 0)
                            RemoveAllSpawns();
                        Log.Msg($"All spawns removed.", playerId);
                        break;
                    }

                case "SetSpawnCounter":
                    {
                        if (config.Enabled)
                        {
                            Log.Msg("Spwaner must be disabled to run SetSpawnCounter", playerId);
                            break;
                        }
                        int value = -1;
                        if (args.Length != 3 || !int.TryParse(args[2], out value))
                        {
                            Log.Msg($"Error in command '{cmdMsg.Msg}'", playerId);
                            break;
                        }
                        if (value < 0)
                        {
                            Log.Msg("Error value < 0", playerId);
                            break;
                        }
                        /*                        if (currentSpawns.Spawns.Count != 0 && value < currentSpawns.SpawnCounter)
                                                {
                                                    Log.Msg("Error value < current value, run RemoveAllSpawns", playerId);
                                                    break;
                                                }*/
                        Log.Msg($"SpawnCounter set to {value}", playerId);
                        currentSpawns.SpawnCounter = value;
                        SaveCurrentSpawns();
                        break;
                    }

                case "AddSpawn":
                    {
                        if (!config.Enabled)
                        {
                            Log.Msg("Spwaner must be enabled to run AddSpawn", playerId);
                            break;
                        }

                        if (currentSpawns.Spawns.Count >= config.MaxSpawns)
                        {
                            Log.Msg("Already at MaxSpawns", playerId);
                            break;
                        }
                        AddSpawn(true);
                        Log.Msg("Spawning requested", playerId);
                        break;
                    }
                case "Status":
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("Status:");
                        sb.AppendLine($"Enabled: {config.Enabled}");
                        sb.AppendLine($"Spawns: {currentSpawns.Spawns.Count} of {config.MaxSpawns}");
                        int i = 2;
                        foreach (var spawn in currentSpawns.Spawns)
                        {
                            if (sb.Length == 0)
                                sb.AppendLine();
                            sb.AppendLine($"{spawn.Name}  {new DateTime(spawn.RemoveAt)}");
                            ++i;
                            if (i == 10)
                            {
                                Log.Msg(sb.ToString(), playerId);
                                sb.Clear();
                                i = 0;
                            }
                        }

                        if (sb.Length > 0)
                            Log.Msg(sb.ToString(), playerId);
                        break;
                    }
                default:
                    {
                        Log.Msg($"Error unknown command '{cmdMsg.Msg}'", playerId);
                        break;
                    }
            }
        }

        public override void LoadData()
        {
            Instance = this;
            Log.Msg("Notification LoadData...........");

            if (MyAPIGateway.Utilities.IsDedicated)
                MyAPIGateway.Utilities.MessageRecieved += Utilities_MessageRecieved;
            else
                MyAPIGateway.Utilities.MessageEntered += Utilities_MessageEntered;

            if (MyAPIGateway.Session.IsServer)
                LoadDataHost();
        }

        private void LoadDataHost()
        {
            config = SpawnerConfig.LoadConfig();
            if (config.UpdatePeriodMins != null && int.TryParse(config.UpdatePeriodMins, out updatePeriodMins))
                updatePeriodMins = Math.Max(updatePeriodMins, 1);
            else
                updatePeriodMins = 5;
            Log.Msg($"Spawner UpdatePeriodMins={updatePeriodMins}");

            urgentMsgPeriodMins = 2 * updatePeriodMins;
            warnMsgPeriodMins = 30;
            dateTimeTicksUrgentMsgPeriod = urgentMsgPeriodMins * DateTimeTicksPerMin;
            dateTimeTicksWarnMsgPeriod = warnMsgPeriodMins * DateTimeTicksPerMin;
            defaultRefreshPeriodTicks = 60 * 60 * updatePeriodMins;

            updateRndMultiplier = 60 / (updatePeriodMins * Math.Max(Math.Min(config.SpawnRateMultiplier, 60 / updatePeriodMins), 0));

            Log.Msg($"Spawner Enabled={config.Enabled}");

            MyVisualScriptLogicProvider.PrefabSpawnedDetailed += PrefabSpawnedDetailed;
        }


        public override void BeforeStart()
        {
            //Log.Msg("BeforeStart");
            base.BeforeStart();
            if (MyAPIGateway.Session.IsServer)
                BeforeStartHost();
        }

        private void BeforeStartHost()
        {
            Log.Msg("Spawner Before Start Host");
            factionOwnerId = FindFactionId(config.FactionTag);

            double totalWeighting = 0;
            foreach (var sector in config.Sectors)
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
                    currentSpawns = MyAPIGateway.Utilities.SerializeFromBinary<CurrentSpawnsData>(Convert.FromBase64String(variableStr));
                }
                catch (Exception ex)
                {
                    Log.Msg($"Error: Failed to deseralize currentSpawns\n{ex.ToString()}");
                    currentSpawns = new CurrentSpawnsData();
                }

                for (int i = currentSpawns.Spawns.Count - 1; i >= 0; --i)
                {
                    var spawn = currentSpawns.Spawns[i];
                    if (spawn.EntityId < 0)
                    {
                        Log.Msg($"currentSpawn EntityId not set, removing '{spawn.Name}'");
                        currentSpawns.Spawns.Remove(spawn);
                        continue;
                    }
                    spawn.ZoneId = -1;
                    Log.Msg($"currentSpawn loaded '{spawn.Name}'");
                }
            }
        }

        protected override void UnloadData()
        {
            try
            {
                if (MyAPIGateway.Session.IsServer)
                {
                    MyVisualScriptLogicProvider.PrefabSpawnedDetailed -= PrefabSpawnedDetailed;

                }

                if (MyAPIGateway.Utilities.IsDedicated)
                    MyAPIGateway.Utilities.MessageRecieved -= Utilities_MessageRecieved;
                else
                    MyAPIGateway.Utilities.MessageEntered -= Utilities_MessageEntered;

                Instance = null;
            }
            catch (Exception e)
            {
                Log.Msg($"Error in UnloadData\n{e.ToString()}");
            }
        }

    }

}
