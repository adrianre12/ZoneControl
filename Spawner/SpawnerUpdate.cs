using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using static ZoneControl.Utils;

namespace ZoneControl.Spawner
{
    internal partial class SpawnerSession
    {
        private bool updateSpawns;
        private int nextSpawnIndex = -1;
        private Random rng = new Random();
        private int nextRefreshFrame = 1800; // inital delay 30s, frame counter should be 0 at startup
        private CurrentSpawnsData currentSpawns = new CurrentSpawnsData();
        private SpawnSummary spawnSummary = new SpawnSummary();

        internal void Update(int currentFrame)
        {
            if (cmdQueue.Count > 0)
            {
                CommandHandler(cmdQueue.Dequeue());
                return;
            }

            if (!config.Enabled)
            {
                if (currentSpawns.Spawns.Count > 0)
                    RemoveAllSpawns();
                return;
            }

            if (updateSpawns)
            {
                if (Log.Debug) Log.Msg($"updateSpawns={updateSpawns} nextSpawnIndex={nextSpawnIndex}");

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
                            ZonesSession.Instance.SubZoneTable.AddExtraMessage(spawn.ZoneId, config.MessageUrgent, config.MessageColour, true);
                        }
                        else
                        {
                            //Log.Msg($"Adding Warn Msg '{configSpawner.MessageWarn}'");
                            ZonesSession.Instance.SubZoneTable.AddExtraMessage(spawn.ZoneId, config.MessageWarn, config.MessageColour, false);
                        }
                    }
                    CheckSubZone(spawn);
                    --nextSpawnIndex;
                    return;
                }

                //All done
                if (config.Enabled && currentSpawns.Spawns.Count < config.MaxSpawns)
                {
                    AddSpawn();
                }

                //randomSpawnList.Clear();
                updateSpawns = false;
            }

            if (currentFrame < nextRefreshFrame)
                return;
            //Log.Msg($"Tick Spawner enabled= {Config.Enabled} count={CurrentSpawns.Spawns.Count} max={Config.MaxSpawns} ");
            nextRefreshFrame = currentFrame + defaultRefreshPeriodTicks;
            updateSpawns = true;
            nextSpawnIndex = currentSpawns.Spawns.Count - 1;
        }

        private bool AddSpawn(bool force = false, string prefabName = "")
        {
            if (Log.Debug) Log.Msg($"Starting AddSpawn force={force} prefabName='{prefabName}'");
            double rnd = force ? 1 : updateRndMultiplier * rng.NextDouble(); //make >1 to get no spawn probability
            if (Log.Debug) Log.Msg($"AddSpawn rnd={rnd}");

            if (rnd > 1)
            {
                if (Log.Debug) Log.Msg($"No spawn this time rnd={rnd}");
                return false;
            }

            SpawnInfo newSpawn = new SpawnInfo();
            //var gameTime = MyAPIGateway.Session.GameDateTime;
            //newSpawn.Name = $"Anomaly {gameTime.ToString("yyMMdd HH:mm")}";

            //newSpawn.Name = $"Anomaly {DateTime.Now.ToString("yyMMdd HH:mm")}"; //tmp name

            //find prefab
            double totalWeightNorm = 0;
            PrefabInfoInternal selectedPrefab = null;
            if (prefabName != "")
            {
                foreach (PrefabInfoInternal pi in prefabs)
                {
                    if (Log.Debug) Log.Msg($"'{pi.Subtype}' == '{prefabName}'");
                    if (pi.Subtype == prefabName)
                    {
                        if (Log.Debug) Log.Msg($"Selected prefab '{pi.Subtype}'");
                        selectedPrefab = pi;
                        break;
                    }
                }
                if (selectedPrefab == null)
                {
                    Log.Msg($"Error: prefab '{prefabName}' not found in list");
                    return false;
                }
            }
            else
            {
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
                    return false;
                }
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
                return false;
            }

            newSpawn.Position = spawnPosition.Value;
            newSpawn.SubZonePosition = spawnPosition.Value + 0.8f * config.AlertRadius * (float)rng.NextDouble() * MyUtils.GetRandomVector3Normalized();
            newSpawn.RemoveAt = DateTime.Now.Ticks + (long)(DateTimeTicksPerHour * (selectedPrefab.LifetimeMin + ((selectedPrefab.LifetimeMax - selectedPrefab.LifetimeMin) * rng.NextDouble())));
            ++currentSpawns.SpawnCounter;
            newSpawn.AnomalyId = currentSpawns.SpawnCounter;
            newSpawn.Name = $"Anomaly#{currentSpawns.SpawnCounter}";
            //newSpawn.DPname = TextReplace(configSpawner.DataPadTitle, "[NAME]", newSpawn.Name);
            //newSpawn.DPdata = TextReplace(configSpawner.DataPadMessage, "[NAME]", newSpawn.Name, "[GPS]", ZonesConfigBase.VectorToGPS(newSpawn.Name, newSpawn.Position, configSpawner.GPScolourHex));

            MyVisualScriptLogicProvider.SpawnPrefab(selectedPrefab.Subtype, spawnPosition.Value, Vector3D.Forward, Vector3D.Up, factionOwnerId, spawningOptions: SpawningOptions.RotateFirstCockpitTowardsDirection | SpawningOptions.UseOnlyWorldMatrix);

            currentSpawns.Spawns.Add(newSpawn);
            return true;
        }

        /// <summary>
        /// Called after the prefab has been spawned, only way to find the entityId
        /// </summary>
        public void PrefabSpawnedDetailed(long entityId, string prefabName)
        {
            IMyEntity entity;
            if (!MyAPIGateway.Entities.TryGetEntityById(entityId, out entity))
            {
                Log.Msg("Spawner could not find Entity");
                return;
            }
            IMyCubeGrid cubeGrid = entity as IMyCubeGrid;
            if (Log.Debug)
            {
                var bigOwner = cubeGrid?.BigOwners.Count > 0 ? cubeGrid?.BigOwners[0] : 0;
                Log.Msg($"Grid name={prefabName} BigOwners Count={cubeGrid?.BigOwners.Count} BigOwners[0]={bigOwner} FactionOwner={factionOwnerId}");
            }
            if (cubeGrid?.BigOwners.Count == 0 || cubeGrid?.BigOwners[0] != factionOwnerId)
            {
                if (Log.Debug)
                    Log.Msg($"Rejecting name={prefabName}");
                return;
            }
            if (Log.Debug) Log.Msg($"Prefab spawned id={entityId}, name={prefabName}");

            foreach (var spawn in currentSpawns.Spawns)
            {
                if (spawn.EntityId != 0)
                    continue;
                if (Vector3D.DistanceSquared(entity.GetPosition(), spawn.Position) < 0.0001)
                {
                    cubeGrid.IsStatic = true;
                    spawn.EntityId = entityId;
                    CheckSubZone(spawn);
                    if (Log.Debug) Log.Msg($"Spawned '{spawn.Name}' ZoneId={spawn.ZoneId} RemoveAt={new DateTime(spawn.RemoveAt)}");

                    SaveCurrentSpawns();
                    return;
                }
            }
            Log.Msg("Spawnwer could not find Entity in CurrentSpawns");
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
            if (spawn.ZoneId >= 0)
            {
                ZonesSession.Instance.SubZoneTable.RemoveZone(spawn.ZoneId);
                if (Log.Debug) Log.Msg($"Removed SubZone {spawn.ZoneId}");
            }

            //close grid
            var grid = MyAPIGateway.Entities.GetEntityById(spawn.EntityId) as IMyCubeGrid;
            if (grid != null)
            {
                if (Vector3D.Distance(grid.GetPosition(), spawn.SubZonePosition) < config.AlertRadius)
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
                Log.Msg($"Error serializing CurrentSpawns\n {e}");
            }
            if (Log.Debug)
                foreach (var spawn in currentSpawns.Spawns)
                {
                    Log.Msg($"currentSpawn saved '{spawn.Name}");
                }

            spawnSummary = new SpawnSummary(currentSpawns);
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
                anomaly.AlertRadius = config.AlertRadius;
                anomaly.AlertRadiusSqrd = config.AlertRadius * config.AlertRadius;
                anomaly.AlertMessageEnter = TextReplace(config.AlertMessageEnter, "[NAME]", spawn.Name);
                anomaly.ColourEnter = CheckColour(config.ColourEnter);
                anomaly.AlertMessageLeave = TextReplace(config.AlertMessageLeave, "[NAME]", spawn.Name);
                anomaly.ColourLeave = CheckColour(config.ColourLeave);
                anomaly.AlertTimeMs = config.AlertTimeMs;
                spawn.ZoneId = ZonesSession.Instance.SubZoneTable.AddZone(anomaly);
                if (Log.Debug) Log.Msg($"Added SubZone {spawn.ZoneId} {spawn.Name}");
                return;
            }
        }

        public SpawnSummary GetSpawnSummary()
        {
            return spawnSummary;
        }
    }
}
