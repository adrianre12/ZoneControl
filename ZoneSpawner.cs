using ProtoBuf;
using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using static ZoneControl.ZonesSession;

namespace ZoneControl
{
    public class ZoneSpawner
    {
        [ProtoContract]
        public class SpawnInfo
        {
            [ProtoMember(1)]
            public string Name = "";
            [ProtoMember(2)]
            public Vector3D Position = Vector3D.MaxValue;
            [ProtoMember(3)]
            public long RemoveAt = 0; //system DateTime ticks
            [ProtoMember(4)]
            public Vector3D SubZonePosition = Vector3D.MaxValue;
            [ProtoMember(5)]
            public long EntityId = 0;
            [ProtoMember(6)]
            public int ZoneId = -1;
            [ProtoMember(7)]
            public long AnomalyId = 0;
            /*
            [ProtoMember(8)]
            public string DPname = "";
            [ProtoMember(9)]
            public string DPdata = "";*/


            public SpawnInfo() { }

            public SpawnInfo(SpawnInfo spawnInfo)
            {
                Name = spawnInfo.Name;
                Position = new Vector3D(spawnInfo.Position);
                RemoveAt = spawnInfo.RemoveAt;
                SubZonePosition = new Vector3D(spawnInfo.SubZonePosition);
                EntityId = spawnInfo.EntityId;
                ZoneId = spawnInfo.ZoneId;
                AnomalyId = spawnInfo.AnomalyId;
                //DPname = spawnInfo.DPname;
                //DPdata = spawnInfo.DPdata;

            }


        }

        [ProtoContract]
        public class CurrentSpawns
        {
            [ProtoMember(1)]
            public List<SpawnInfo> Spawns = new List<SpawnInfo>();
            [ProtoMember(2)]
            public int SpawnCounter = 0;

            public CurrentSpawns() { }
        }

        const string VariableId = nameof(ZoneSpawner);
        const long DateTimeTicksPerHour = 36000000000L;
        const long DateTimeTicksPerMin = 600000000L;

        private readonly int updatePeriodMins;
        private readonly int urgentMsgPeriodMins;
        private readonly int warnMsgPeriodMins;
        private readonly long dateTimeTicksUrgentMsgPeriod;
        private readonly long dateTimeTicksWarnMsgPeriod;
        private readonly int defaultRefreshPeriodTicks;

        //private readonly MyDefinitionId DatapadDefId = new MyDefinitionId(typeof(MyObjectBuilder_Datapad), "Datapad");

        private int updateRndMultiplier = 0;
        private int nextRefreshFrame = 1800; // 30s, frame counter should be 0 at startup
        private List<PrefabInfoInternal> prefabs; //all prefabs with weighting.
        private ZonesConfig.SpawnerInfo configSpawner;
        private bool updateSpawns;
        private CurrentSpawns currentSpawns = new CurrentSpawns();
        //private  List<SpawnInfo> randomSpawnList = new List<SpawnInfo>();
        private int nextSpawnIndex = -1;
        private Random rng = new Random();
        private long factionOwnerId;

        private Queue<CmdMsg> cmdQueue = new Queue<CmdMsg>();

        public ZoneSpawner(ZonesConfig config)
        {
            prefabs = new List<PrefabInfoInternal>();
            configSpawner = config.Spawner;
            configSpawner.Verify();

            if (configSpawner.UpdatePeriodMins != null && int.TryParse(configSpawner.UpdatePeriodMins, out updatePeriodMins))
                updatePeriodMins = Math.Max(updatePeriodMins, 1);
            else
                updatePeriodMins = 5;
            Log.Msg($"Spawner UpdatePeriodMins={updatePeriodMins}");

            urgentMsgPeriodMins = 2 * updatePeriodMins;
            warnMsgPeriodMins = 30;
            dateTimeTicksUrgentMsgPeriod = urgentMsgPeriodMins * DateTimeTicksPerMin;
            dateTimeTicksWarnMsgPeriod = warnMsgPeriodMins * DateTimeTicksPerMin;
            defaultRefreshPeriodTicks = 60 * 60 * updatePeriodMins;

            updateRndMultiplier = 60 / (updatePeriodMins * Math.Max(Math.Min(configSpawner.SpawnRateMultiplier, 60 / updatePeriodMins), 0));
            double totalWeighting = 0;

            Log.Msg($"Spawner Enabled={configSpawner.Enabled}");
            factionOwnerId = FindFactionId(configSpawner.FactionTag);

            foreach (var sector in configSpawner.Sectors)
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
                    currentSpawns = MyAPIGateway.Utilities.SerializeFromBinary<CurrentSpawns>(Convert.FromBase64String(variableStr));
                }
                catch (Exception ex)
                {
                    Log.Msg($"Error: Failed to deseralize currentSpawns\n{ex.ToString()}");
                    currentSpawns = new CurrentSpawns();
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

            MyVisualScriptLogicProvider.PrefabSpawnedDetailed += PrefabSpawnedDetailed;
        }


        /*        public MyObjectBuilder_Datapad GetRandomDatapad()
                {
                    if (currentSpawns.Spawns.Count == 0)
                        return null;

                    var dp = (MyObjectBuilder_Datapad)MyObjectBuilderSerializer.CreateNewObject(DatapadDefId);

                    var rndSpwan = currentSpawns.Spawns[rng.Next(currentSpawns.Spawns.Count)];
                    dp.Name = rndSpwan.DPname;
                    dp.Data = rndSpwan.DPdata;

                    return dp;
                }*/


        /*        private void CreateRandomSpawnList()
                {
                    randomSpawnList= GetActiveSpawns();
                    int n = randomSpawnList.Count;
                    while (n > 1)
                    {
                        n--;
                        int k = rng.Next(n + 1);
                        var value = randomSpawnList[k];
                        randomSpawnList[k] = randomSpawnList[n];
                        randomSpawnList[n] = value;
                    }
                }*/

        public List<SpawnInfo> GetActiveSpawns()
        {
            List<SpawnInfo> activeSpawns = new List<SpawnInfo>();
            foreach (SpawnInfo spawn in currentSpawns.Spawns)
            {
                if (spawn.EntityId < 0)
                    continue;
                activeSpawns.Add(new SpawnInfo(spawn));
            }
            return activeSpawns;
        }

        internal void Close()
        {
            MyVisualScriptLogicProvider.PrefabSpawnedDetailed -= PrefabSpawnedDetailed;
        }

        internal void Enqueue(CmdMsg cmdMsg)
        {
            cmdQueue.Enqueue(cmdMsg);
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

            Log.Msg($"Player {cmdMsg.Player?.DisplayName ?? "Local"} ran command {cmdMsg.Msg}");
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
                        if (configSpawner.Enabled)
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
                        if (!configSpawner.Enabled)
                        {
                            Log.Msg("Spwaner must be enabled to run AddSpawn", playerId);
                            break;
                        }

                        if (currentSpawns.Spawns.Count >= configSpawner.MaxSpawns)
                        {
                            Log.Msg("Already at MaxSpawns", playerId);
                            break;
                        }
                        AddSpawn(true);
                        Log.Msg("Spawning requested");
                        break;
                    }
                case "Status":
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("Status:");
                        sb.AppendLine($"Enabled: {configSpawner.Enabled}");
                        sb.AppendLine($"Spawns: {currentSpawns.Spawns.Count} of {configSpawner.MaxSpawns}");
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

        internal void Update(int currentFrame)
        {
            if (cmdQueue.Count > 0)
            {
                CommandHandler(cmdQueue.Dequeue());
                return;
            }

            if (!configSpawner.Enabled)
            {
                if (currentSpawns.Spawns.Count > 0)
                    RemoveAllSpawns();
                return;
            }

            if (updateSpawns)
            {
                //Log.Msg($"updateSpawns={updateSpawns} nextSpawnIndex={nextSpawnIndex}");

                //do the loop
                if (nextSpawnIndex >= 0)
                {// update spawns
                    SpawnInfo spawn = currentSpawns.Spawns[nextSpawnIndex];
                    if (Log.Debug) Log.Msg($"Updating spawn[{nextSpawnIndex}] '{spawn.Name}' ZoneId={spawn.ZoneId} RemoveAt={new DateTime(spawn.RemoveAt)}");

                    //remove if if too old
                    if (spawn.RemoveAt < DateTime.Now.Ticks)
                    {
                        RemoveSpawn(spawn);
                        --nextSpawnIndex;
                        return;
                    }
                    //Log.Msg($"WarnAt={new DateTime(spawn.RemoveAt - DateTimeTicksWarnMsgPeriod).ToString()}");
                    if (spawn.ZoneId > 0 && spawn.RemoveAt - dateTimeTicksWarnMsgPeriod < DateTime.Now.Ticks)
                    {
                        if (spawn.RemoveAt - dateTimeTicksUrgentMsgPeriod < DateTime.Now.Ticks)
                        {
                            //Log.Msg($"Adding Urgent Msg '{configSpawner.MessageUrgent}'");
                            ZonesSession.Instance.SubZoneTable.AddExtraMessage(spawn.ZoneId, configSpawner.MessageUrgent, configSpawner.MessageColour, true);
                        }
                        else
                        {
                            //Log.Msg($"Adding Warn Msg '{configSpawner.MessageWarn}'");
                            ZonesSession.Instance.SubZoneTable.AddExtraMessage(spawn.ZoneId, configSpawner.MessageWarn, configSpawner.MessageColour, false);
                        }
                    }
                    CheckSubZone(spawn);
                    --nextSpawnIndex;
                    return;
                }

                //All done
                if (configSpawner.Enabled && currentSpawns.Spawns.Count < configSpawner.MaxSpawns)
                {
                    AddSpawn();
                }

                //randomSpawnList.Clear();
                updateSpawns = false;
            }

            if (currentFrame < nextRefreshFrame)
                return;
            nextRefreshFrame = currentFrame + defaultRefreshPeriodTicks;
            updateSpawns = true;
            nextSpawnIndex = currentSpawns.Spawns.Count - 1;
        }

        private void AddSpawn(bool force = false)
        {
            double rnd = force ? 1 : updateRndMultiplier * rng.NextDouble(); //make >1 to get no spawn probability
            if (Log.Debug) Log.Msg($"AddSpawn rnd={rnd}");

            if (rnd > 1)
            {
                if (Log.Debug) Log.Msg($"No spawn this time rnd={rnd}");
                return;
            }

            SpawnInfo newSpawn = new SpawnInfo();
            //var gameTime = MyAPIGateway.Session.GameDateTime;
            //newSpawn.Name = $"Anomaly {gameTime.ToString("yyMMdd HH:mm")}";

            //newSpawn.Name = $"Anomaly {DateTime.Now.ToString("yyMMdd HH:mm")}"; //tmp name

            //find prefab
            double totalWeightNorm = 0;
            PrefabInfoInternal selectedPrefab = null;
            foreach (PrefabInfoInternal pi in prefabs)
            {
                totalWeightNorm += pi.WeightNorm;
                if (rnd <= totalWeightNorm)
                {
                    if (Log.Debug) Log.Msg($"Selected prefab '{pi.Subtype}'");
                    selectedPrefab = pi;
                    break;
                }
            }
            if (selectedPrefab == null)
            {
                Log.Msg($"Error: should have a prefab, rnd={rnd}");
                return;
            }

            //find free position
            int i = 20;
            Vector3D? spawnPosition = null;
            while (i > 0 && spawnPosition == null)
            {
                --i;
                spawnPosition = selectedPrefab.SectorInfo.Position + selectedPrefab.SectorInfo.Radius * MyUtils.GetRandomVector3Normalized();

                if (MyAPIGateway.GravityProviderSystem.IsPositionInNaturalGravity(spawnPosition.Value, 2000))  //more than 2Km outside grav
                    continue;

                spawnPosition = MyAPIGateway.Entities.FindFreePlace(spawnPosition.Value, 100);
            }

            if (spawnPosition == null)
            {
                Log.Msg($"Could not find free position");
                return;
            }

            newSpawn.Position = spawnPosition.Value;
            newSpawn.SubZonePosition = spawnPosition.Value + 0.8f * configSpawner.AlertRadius * (float)rng.NextDouble() * MyUtils.GetRandomVector3Normalized();
            newSpawn.RemoveAt = DateTime.Now.Ticks + (long)(DateTimeTicksPerHour * (selectedPrefab.LifetimeMin + ((selectedPrefab.LifetimeMax - selectedPrefab.LifetimeMin) * rng.NextDouble())));
            ++currentSpawns.SpawnCounter;
            newSpawn.AnomalyId = currentSpawns.SpawnCounter;
            newSpawn.Name = $"Anomaly#{currentSpawns.SpawnCounter}";
            //newSpawn.DPname = TextReplace(configSpawner.DataPadTitle, "[NAME]", newSpawn.Name);
            //newSpawn.DPdata = TextReplace(configSpawner.DataPadMessage, "[NAME]", newSpawn.Name, "[GPS]", ZonesConfigBase.VectorToGPS(newSpawn.Name, newSpawn.Position, configSpawner.GPScolourHex));

            MyVisualScriptLogicProvider.SpawnPrefab(selectedPrefab.Subtype, spawnPosition.Value, Vector3D.Forward, Vector3D.Up, factionOwnerId, spawningOptions: SpawningOptions.RotateFirstCockpitTowardsDirection | SpawningOptions.UseOnlyWorldMatrix);

            currentSpawns.Spawns.Add(newSpawn);
        }

        /// <summary>
        /// Called after the prefab has been spawned, only way to find the entityId
        /// </summary>
        public void PrefabSpawnedDetailed(long entityId, string prefabName)
        {
            if (Log.Debug) Log.Msg($"Prefab spawned id={entityId}, name={prefabName}");
            IMyEntity entity;
            if (!MyAPIGateway.Entities.TryGetEntityById(entityId, out entity))
            {
                Log.Msg("Spawner could not find Entity");
                return;
            }

            foreach (var spawn in currentSpawns.Spawns)
            {
                if (spawn.EntityId != 0)
                    continue;
                if (Vector3D.DistanceSquared(entity.GetPosition(), spawn.Position) < 0.0001)
                {
                    ((IMyCubeGrid)entity).IsStatic = true;
                    spawn.EntityId = entityId;
                    CheckSubZone(spawn);
                    if (Log.Debug) Log.Msg($"Spawned '{spawn.Name}' ZoneId={spawn.ZoneId} RemoveAt={new DateTime(spawn.RemoveAt)}");

                    SaveCurrentSpawns();
                    return;
                }
            }
            Log.Msg("Spawnwer could not find Entity in currentSpawns");
        }

        private void RemoveAllSpawns()
        {
            for (int i = currentSpawns.Spawns.Count - 1; i >= 0; --i)
            {
                var spawn = currentSpawns.Spawns[i];
                RemoveSpawn(spawn);
                if (Log.Debug) Log.Msg($"Removed spawn '{spawn.Name}'");

            }
            SaveCurrentSpawns();
        }

        private void RemoveSpawn(SpawnInfo spawn, bool save = true)
        {
            //remove anomally
            RemoveSubZone(spawn);

            //close grid
            var grid = MyAPIGateway.Entities.GetEntityById(spawn.EntityId) as IMyCubeGrid;
            if (grid != null)
            {
                if (Vector3D.Distance(grid.GetPosition(), spawn.SubZonePosition) < configSpawner.AlertRadius)
                {
                    if (Log.Debug) Log.Msg($"Closing '{grid.DisplayName}' ");
                    List<IMyCubeGrid> cubeGrids = new List<IMyCubeGrid>();
                    grid.GetGridGroup(GridLinkTypeEnum.Mechanical).GetGrids(cubeGrids);
                    foreach (var subGrid in cubeGrids)
                    {
                        foreach (var cockpit in subGrid.GetFatBlocks<IMyCockpit>())
                        {
                            cockpit.RemovePilot();
                        }
                        subGrid.Close();
                    }
                }
                else
                {
                    if (Log.Debug) Log.Msg($"Spawn moved, not being removed: '{spawn.Name}'");
                }
            }
            currentSpawns.Spawns.Remove(spawn);
            if (save)
                SaveCurrentSpawns();
        }

        private void SaveCurrentSpawns()
        {
            try
            {
                MyAPIGateway.Utilities.SetVariable<string>(VariableId, Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(currentSpawns)));
            }
            catch (Exception e)
            {
                Log.Msg($"Error serializing currentSpawns\n {e}");
            }
            if (Log.Debug)
                foreach (var spawn in currentSpawns.Spawns)
                {
                    Log.Msg($"currentSpawn saved '{spawn.Name}");
                }
        }

        private void CheckSubZone(SpawnInfo spawn)
        {
            if (spawn.ZoneId >= 0)
                return;
            //ZoneId has not been set, add a zone
            {
                //add anomaly
                ZoneInfoInternal anomaly = new ZoneInfoInternal();
                anomaly.Type = ZoneInfoInternal.ZoneType.Anomaly;
                anomaly.UniqueName = spawn.Name;
                anomaly.Position = spawn.SubZonePosition;
                anomaly.AlertRadius = configSpawner.AlertRadius;
                anomaly.AlertRadiusSqrd = configSpawner.AlertRadius * configSpawner.AlertRadius;
                anomaly.AlertMessageEnter = TextReplace(configSpawner.AlertMessageEnter, "[NAME]", spawn.Name);
                anomaly.ColourEnter = CheckColour(configSpawner.ColourEnter);
                anomaly.AlertMessageLeave = TextReplace(configSpawner.AlertMessageLeave, "[NAME]", spawn.Name);
                anomaly.ColourLeave = CheckColour(configSpawner.ColourLeave);
                anomaly.AlertTimeMs = configSpawner.AlertTimeMs;
                spawn.ZoneId = ZonesSession.Instance.SubZoneTable.AddZone(anomaly);
                if (Log.Debug) Log.Msg($"Added SubZone {spawn.ZoneId} {spawn.Name}");
                return;
            }
        }

        private string CheckColour(string colour)
        {
            if (colour == null || colour.Trim().Length == 0)
                return "White";
            return colour.Trim();
        }

        private string TextReplace(string text, string key1, string value1, string key2 = null, string value2 = null)
        {
            if (text == null)
                return "";
            var sb = new StringBuilder(text.Trim());
            sb.Replace(key1, value1);
            if (key2 == null || value2 == null)
                return sb.ToString();
            sb.Replace(key2, value2);
            return sb.ToString();
        }

        private void RemoveSubZone(SpawnInfo spawn)
        {
            if (spawn.ZoneId < 0)
                return;

            ZonesSession.Instance.SubZoneTable.RemoveZone(spawn.ZoneId);
            if (Log.Debug) Log.Msg($"Removed SubZone {spawn.ZoneId}");
        }

        private long FindFactionId(string tag)
        {
            IMyFaction faction = null;
            if (tag != null)
                faction = MyAPIGateway.Session.Factions.TryGetFactionByTag(tag.Trim());
            if (faction != null)
            {
                Log.Msg($"Spawnwer using faction {tag}");
                return faction.FounderId;
            }
            faction = MyAPIGateway.Session.Factions.TryGetFactionByTag("UNKN");
            if (faction != null)
            {
                Log.Msg($"Spawnwer using default faction UNKN");
                return faction.FounderId;
            }
            Log.Msg($"Spawnwer UNKN not found using NOBODY");
            return 0;
        }
    }
}
