using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;
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


        internal void CmdQueueEnqueue(CmdMsg cmdMsg)
        {
            cmdQueue.Enqueue(cmdMsg);
        }

        private void CommandHandler(CmdMsg cmdMsg)
        {
            long playerId = cmdMsg.Player?.IdentityId ?? 0;
            List<string> args = cmdMsg.Packet.Args;
            if (args.Count < 2)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Help:");
                sb.AppendLine("/ZoneSpawner Status");
                sb.AppendLine("   Lists the current status of the Spawner.");

                sb.AppendLine("/ZoneSpawner AddSpawn");
                sb.AppendLine("   Request a random Anomaly spawn.");

                sb.AppendLine("/ZoneSpawner AddSpawn \"prefabName\"");
                sb.AppendLine("   Request an Anomaly spawn of the configured prefab.");

                sb.AppendLine("/ZoneSpawner RemoveAllSpawns");
                sb.AppendLine("   Removes all current spawns and Anomalies.");

                sb.AppendLine("/ZoneSpawner PrefabList");
                sb.AppendLine("   Lists all the configured prefabs.");

                sb.AppendLine("/ZoneSpawner PrefabSpawn \"Subtype\" [Loot]");
                sb.AppendLine("   Spawns any prefab by its Subtype, it does not have to be in configuration. This is not an Anomaly and will not be removed!");
                sb.AppendLine("   The optional Loot parameter triggers the random loot generation.");

                sb.AppendLine("/ZoneSpawner SetSpawnCounter");
                sb.AppendLine("   Can only be run when the spawner is disabled in config.");

                sb.AppendLine("/ZoneControl");
                sb.AppendLine("   Commands for ZoneControl");

                Log.Msg(sb.ToString(), playerId);
                return;
            }

            Log.Msg($"Player {cmdMsg.Player?.DisplayName ?? "Local"} [{cmdMsg.Player?.IdentityId}] ran command {cmdMsg.Packet.Msg}");
            switch (args[1])
            {
                case "Debug":
                    {
                        Log.Debug = !Log.Debug;
                        Log.Msg($"Log Debug={Log.Debug}", playerId);
                        break;
                    }
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
                        if (args.Count != 3 || !int.TryParse(args[2], out value))
                        {
                            Log.Msg($"Error in command '{cmdMsg.Packet.Msg}'", playerId);
                            break;
                        }
                        if (value < 0)
                        {
                            Log.Msg("Error value < 0", playerId);
                            break;
                        }

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
                        if (args.Count < 3)
                        {
                            AddSpawn(true);
                            Log.Msg("Spawning random prefab requested", playerId);
                        }
                        else
                        {
                            string prefabName = args[2].Trim(new char[] { ' ', '"' });
                            if (AddSpawn(true, prefabName))
                                Log.Msg($"Spawning prefab '{prefabName}' requested", playerId);
                            else
                                Log.Msg($"Failed to submit '{prefabName}' see log", playerId);
                        }

                        break;
                    }

                case "Status":
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("Status:");
                        sb.AppendLine($"Enabled: {config.Enabled}");
                        sb.AppendLine($"Spawns: {currentSpawns.Spawns.Count} of {config.MaxSpawns}");
                        sb.AppendLine($"Date Now: {DateTime.Now.ToString("dd/MMM HH:mm")}");

                        int i = 0;
                        foreach (var spawn in currentSpawns.Spawns)
                        {
                            if (sb.Length == 0)
                                sb.AppendLine();
                            sb.AppendLine($"#{spawn.AnomalyId} '{spawn.PrefabName}'  {new DateTime(spawn.RemoveAt).ToString("dd/MMM HH:mm")}");
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

                case "PrefabList":
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("PrefabList:");
                        int i = 0;
                        foreach (var prefab in prefabs)
                        {
                            if (i == 0)
                                sb.AppendLine("PrefabName WeightNorm Sector");
                            sb.AppendLine($"\"{prefab.Subtype}\" {prefab.WeightNorm:0.000} \"{prefab.SectorInfo.UniqueName}\"");
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

                case "PrefabSpawn":
                    {
                        if (args.Count < 3)
                        {
                            Log.Msg("Prefab Subtype must be given.", playerId);
                            break;
                        }

                        if (args[2].Length == 0)
                        {
                            Log.Msg("Prefab Subtype must be given.", playerId);
                        }

                        Vector3D spawnPosition = cmdMsg.Player?.GetPosition() ?? MyAPIGateway.Session.Player.GetPosition();
                        var character = cmdMsg.Player?.Character ?? MyAPIGateway.Session.Player.Character;
                        spawnPosition = spawnPosition + character.LocalMatrix.Forward * 100; //new Vector3D(200, 0, 0);
                        if (MyAPIGateway.GravityProviderSystem.IsPositionInNaturalGravity(spawnPosition, 1000))
                        {
                            Log.Msg("Spawning in gravity disabled", playerId);
                            break;
                        }

                        var freePosition = MyAPIGateway.Entities.FindFreePlace(spawnPosition, 50);
                        if (!freePosition.HasValue)
                        {
                            Log.Msg("A spawn position was not found", playerId);
                            break;
                        }
                        SpawningOptions spawnOptions = SpawningOptions.UseOnlyWorldMatrix;
                        if (args.Count >= 4 && args[3] == "Loot")
                        {
                            spawnOptions |= SpawningOptions.SpawnRandomCargo;
                            Log.Msg("Adding random Loot to prefab", playerId);
                        }
                        MyVisualScriptLogicProvider.SpawnPrefab(args[2], freePosition.Value, Vector3D.Forward, Vector3D.Up, playerId, spawningOptions: spawnOptions);
                        Log.Msg($"Requested spawn of Subtype '{args[2]}'", playerId);
                        Log.Msg($"This is not an Anomaly and will not be removed!, REMEMBER TO REMOVE IT!", playerId);

                        break;
                    }

                default:
                    {
                        Log.Msg($"Error unknown command '{cmdMsg.Packet.Msg}'", playerId);
                        break;
                    }
            }
        }

        public override void LoadData()
        {
            Instance = this;
            Log.Msg("Notification LoadData...........");

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

                spawnSummary = new SpawnSummary(currentSpawns);
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
            }
            catch (Exception e)
            {
                Log.Msg($"Error in UnloadData\n{e.ToString()}");
            }
            finally
            {
                Instance = null;
            }
        }

    }

}
