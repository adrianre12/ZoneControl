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

namespace ZoneControl
{
    internal class ZoneSpawner
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

            public SpawnInfo() { }

            public SpawnInfo(SpawnInfo spawnInfo)
            {
                Name = spawnInfo.Name;
                Position = new Vector3D(spawnInfo.Position);
                RemoveAt = spawnInfo.RemoveAt;
                SubZonePosition = new Vector3D(spawnInfo.SubZonePosition);
                EntityId = spawnInfo.EntityId;
                ZoneId = spawnInfo.ZoneId;
            }
        }

        [ProtoContract]
        internal class CurrentSpawns
        {
            [ProtoMember(1)]
            public List<SpawnInfo> Spawns = new List<SpawnInfo>();
            [ProtoMember(2)]
            public int SpawnCounter = 0;

            public CurrentSpawns() { }
        }

        const string VariableId = nameof(ZoneSpawner);
        const int UpdatePeriodMins = 1;//5;

        const int UrgentMsgPeriodMins = 2 * UpdatePeriodMins;
        const int WarnMsgPeriodMins = 30;

        const int UpdateRndMultiplier = 2 / UpdatePeriodMins; //60 / UpdatePeriodMins;

        const int DefaultRefreshPeriodTicks = 60 * 60 * UpdatePeriodMins;
        const long DateTimeTicksPerHour = 36000000000L;
        const long DateTimeTicksPerMin = 600000000L;
        const long DateTimeTicksUrgentMsgPeriod = UrgentMsgPeriodMins * DateTimeTicksPerMin;
        const long DateTimeTicksWarnMsgPeriod = WarnMsgPeriodMins * DateTimeTicksPerMin;

        private int nextRefreshFrame = 1800; // 30s, frame counter should be 0 at startup
        private List<PrefabInfoInternal> prefabs; //all prefabs with weighting.
        private ZonesConfig.SpawnerInfo configSpawner;
        private bool updateSpawns;
        private CurrentSpawns currentSpawns = new CurrentSpawns();
        private int nextSpawnIndex = -1;
        private Random random = new Random();
        private long factionOwnerId;

        public ZoneSpawner(ZonesConfig config)
        {
            prefabs = new List<PrefabInfoInternal>();
            configSpawner = config.Spawner;
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
                pi.WeightNorm = configSpawner.SpawnRateMultiplier * pi.Weighting / totalWeighting;
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
                    Log.Msg($"currentSpawn loaded '{spawn.Name}' position={spawn.Position} zonePos={spawn.SubZonePosition}");
                }
            }

            MyVisualScriptLogicProvider.PrefabSpawnedDetailed += PrefabSpawnedDetailed;
        }

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

        internal void Update(int currentFrame)
        {
            if (updateSpawns)
            {
                //Log.Msg($"updateSpawns={updateSpawns} nextSpawnIndex={nextSpawnIndex}");

                //do the loop
                if (nextSpawnIndex >= 0)
                {// update spawns
                    SpawnInfo spawn = currentSpawns.Spawns[nextSpawnIndex];
                    Log.Msg($"Updating spawn[{nextSpawnIndex}] '{spawn.Name}' ZoneId={spawn.ZoneId} RemoveAt={(new DateTime(spawn.RemoveAt)).ToString()}");

                    //remove if disabled or if too old
                    if (!configSpawner.Enabled || spawn.RemoveAt < DateTime.Now.Ticks)
                    {
                        RemoveSpawn(spawn);
                        --nextSpawnIndex;
                        return;
                    }
                    //Log.Msg($"WarnAt={new DateTime(spawn.RemoveAt - DateTimeTicksWarnMsgPeriod).ToString()}");
                    if (spawn.ZoneId > 0 && spawn.RemoveAt - DateTimeTicksWarnMsgPeriod < DateTime.Now.Ticks)
                    {
                        if (spawn.RemoveAt - DateTimeTicksUrgentMsgPeriod < DateTime.Now.Ticks)
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
                    AddSpawn();

                updateSpawns = false;
            }
            if (currentFrame < nextRefreshFrame)
                return;
            nextRefreshFrame = currentFrame + DefaultRefreshPeriodTicks;
            updateSpawns = true;
            nextSpawnIndex = currentSpawns.Spawns.Count - 1;
        }

        private void AddSpawn()
        {
            double rnd = UpdateRndMultiplier * random.NextDouble(); //make >1 to get no spawn probability
            Log.Msg($"AddSpawn rnd={rnd}");

            SpawnInfo newSpawn = new SpawnInfo();
            //var gameTime = MyAPIGateway.Session.GameDateTime;
            //newSpawn.Name = $"Anomaly {gameTime.ToString("yyMMdd HH:mm")}";

            newSpawn.Name = $"Anomaly {DateTime.Now.ToString("yyMMdd HH:mm")}"; //tmp name

            //find prefab
            double totalWeightNorm = 0;
            PrefabInfoInternal selectedPrefab = null;
            foreach (PrefabInfoInternal pi in prefabs)
            {
                totalWeightNorm += pi.WeightNorm;
                if (rnd < totalWeightNorm)
                {
                    Log.Msg($"Selected prefab '{pi.Subtype}'");
                    selectedPrefab = pi;
                    break;
                }
            }
            if (selectedPrefab == null)
            {
                Log.Msg($"No spawn this time rnd={rnd}");
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

            //calculate anomaly position
            newSpawn.SubZonePosition = spawnPosition.Value + 0.8f * configSpawner.AlertRadius * (float)random.NextDouble() * MyUtils.GetRandomVector3Normalized();

            // removeAt
            newSpawn.RemoveAt = DateTime.Now.Ticks + (long)(DateTimeTicksPerHour * (selectedPrefab.LifetimeMin + ((selectedPrefab.LifetimeMax - selectedPrefab.LifetimeMin) * random.NextDouble())));
            //spawn grid
            //Log.Msg("Spawn Prefab");
            MyVisualScriptLogicProvider.SpawnPrefab(selectedPrefab.Subtype, spawnPosition.Value, Vector3D.Forward, Vector3D.Up, factionOwnerId, spawningOptions: SpawningOptions.RotateFirstCockpitTowardsDirection | SpawningOptions.UseOnlyWorldMatrix);

            //save
            currentSpawns.Spawns.Add(newSpawn);
        }

        /// <summary>
        /// Called after the prefab has been spawned, only way to find the entityId
        /// </summary>
        public void PrefabSpawnedDetailed(long entityId, string prefabName)
        {
            Log.Msg($"Prefab spawned id={entityId}, name={prefabName}");
            IMyEntity entity;
            if (!MyAPIGateway.Entities.TryGetEntityById(entityId, out entity))
            {
                Log.Msg("Spawnwer could not find Entity");
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
                    ++currentSpawns.SpawnCounter;
                    spawn.Name = $"Anomaly#{currentSpawns.SpawnCounter}";
                    CheckSubZone(spawn);
                    Log.Msg($"Spawned '{spawn.Name}' ZoneId={spawn.ZoneId} RemoveAt={(new DateTime(spawn.RemoveAt)).ToString()}");

                    SaveCurrentSpawns();
                    return;
                }
            }
            Log.Msg("Spawnwer could not find Entity in currentSpawns");
        }

        private void RemoveSpawn(SpawnInfo spawn)
        {
            //remove anomally
            RemoveSubZone(spawn);

            //close grid
            var grid = MyAPIGateway.Entities.GetEntityById(spawn.EntityId) as IMyCubeGrid;
            if (grid != null)
            {
                if (Vector3D.Distance(grid.GetPosition(), spawn.SubZonePosition) < configSpawner.AlertRadius)
                {
                    Log.Msg($"Closing '{grid.DisplayName}' ");
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
                    Log.Msg($"Spawn moved, not being removed: '{spawn.Name}'");
                }
            }
            currentSpawns.Spawns.Remove(spawn);
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
            foreach (var spawn in currentSpawns.Spawns)
            {
                Log.Msg($"currentSpawn saved '{spawn.Name}' position={spawn.Position} zonePos={spawn.SubZonePosition}");
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
                anomaly.AlertMessageEnter = MessageEdit(configSpawner.AlertMessageEnter, spawn.Name);
                anomaly.ColourEnter = CheckColour(configSpawner.ColourEnter);
                anomaly.AlertMessageLeave = MessageEdit(configSpawner.AlertMessageLeave, spawn.Name);
                anomaly.ColourLeave = CheckColour(configSpawner.ColourLeave);
                anomaly.AlertTimeMs = configSpawner.AlertTimeMs;
                spawn.ZoneId = ZonesSession.Instance.SubZoneTable.AddZone(anomaly);
                Log.Msg($"Added SubZone {spawn.ZoneId} {spawn.Name}");
                return;
            }
        }

        private string CheckColour(string colour)
        {
            if (colour == null || colour.Trim().Length == 0)
                return "White";
            return colour.Trim();
        }

        private string MessageEdit(string message, string name)
        {
            if (message == null)
                return "";
            var sb = new StringBuilder(message.Trim());
            sb.Replace("[NAME]", name);
            return sb.ToString();
        }

        private void RemoveSubZone(SpawnInfo spawn)
        {
            if (spawn.ZoneId < 0)
                return;

            ZonesSession.Instance.SubZoneTable.RemoveZone(spawn.ZoneId);
            Log.Msg($"Removed SubZone {spawn.ZoneId}");
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
