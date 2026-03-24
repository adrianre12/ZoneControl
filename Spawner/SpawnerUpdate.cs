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
        const int DefaultSummaryUpdatePeriodTicks = 30 * 60;
        const int GPSDisplayPeriod = 14400; // 4hrs

        private bool updateSpawns;
        private int nextSpawnIndex = -1;
        private Random rng = new Random();
        private int nextRefreshFrame = 1800; // inital delay 30s, frame counter should be 0 at startup
        private int nextSummaryRefreshFrame = DefaultSummaryUpdatePeriodTicks;
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
                //if (Log.Debug) Log.Msg($"updateSpawns={updateSpawns} nextSpawnIndex={nextSpawnIndex}");

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
                    AddWreckSpawn();
                }
                updateSpawns = false;
            }

            if (nextSummaryRefreshFrame < currentFrame)
            {
                nextSummaryRefreshFrame = currentFrame + DefaultSummaryUpdatePeriodTicks;
                //if (Log.Debug) Log.Msg("Starting Summary selection.");
                spawnSummary.UpdateSelected(currentSpawns);
            }

            if (currentFrame < nextRefreshFrame)
                return;
            //Log.Msg($"Tick Spawner enabled= {Config.Enabled} count={CurrentSpawns.Spawns.Count} max={Config.MaxSpawns} ");
            nextRefreshFrame = currentFrame + defaultRefreshPeriodTicks;
            updateSpawns = true;
            nextSpawnIndex = currentSpawns.Spawns.Count - 1;
        }

        private bool AddWreckSpawn(bool force = false, string prefabName = "")
        {
            if (Log.Debug) Log.Msg($"Starting AddSpawn force={force} prefabName='{prefabName}'");
            double rnd = updateRndMultiplier * rng.NextDouble(); //make >1 to get no spawn probability

            if (!force && rnd > 1)
            {
                if (Log.Debug) Log.Msg($"No spawn this time rnd={rnd}");
                return false;
            }

            SpawnInfo newSpawn = new SpawnInfo();
            //var gameTime = MyAPIGateway.Session.GameDateTime;
            //newSpawn.Name = $"Anomaly {gameTime.ToString("yyMMdd HH:mm")}";

            //newSpawn.Name = $"Anomaly {DateTime.Now.ToString("yyMMdd HH:mm")}"; //tmp name

            //find prefab
            PrefabInfoInternal selectedPrefab = null;
            if (prefabName != "")
            {
                if (!prefabs.TryGetValue(prefabName, out selectedPrefab))
                {
                    Log.Msg($"Error: prefab '{prefabName}' not found in list");
                    return false;
                }
            }
            else
            {
                int c = 5;
                while (c-- > 0 && selectedPrefab == null)
                {
                    rnd = rng.NextDouble();
                    double totalWeightNorm = 0;

                    if (Log.Debug) Log.Msg($"AddSpawn c={c} rnd={rnd}");

                    foreach (PrefabInfoInternal pi in prefabs.Values)
                    {
                        totalWeightNorm += pi.WeightNorm;
                        if (rnd <= totalWeightNorm)
                        {
                            if (Log.Debug) Log.Msg($"Selected prefab '{pi.Subtype}'");
                            if (currentSpawns.HasGroupId(pi.GroupId))
                            {
                                if (Log.Debug) Log.Msg("GroupId already spawned, try again");
                                break;
                            }
                            selectedPrefab = pi;
                            break;
                        }
                    }
                }
                if (selectedPrefab == null)
                {
                    if (Log.Debug) Log.Msg($"Could not find a unique prefab");
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
            newSpawn.PrefabName = selectedPrefab.Subtype;
            newSpawn.GroupId = selectedPrefab.GroupId;
            newSpawn.PiratesEnabled = selectedPrefab.EnablePirates;

            MyVisualScriptLogicProvider.SpawnPrefab(selectedPrefab.Subtype, spawnPosition.Value, Vector3D.Forward, Vector3D.Up, selectedPrefab.EnablePirates ? pirateOwnerId : factionOwnerId, spawningOptions: SpawningOptions.UseOnlyWorldMatrix | SpawningOptions.SpawnRandomCargo | SpawningOptions.SetAuthorship);

            currentSpawns.Spawns.Add(newSpawn);
            return true;
        }

        public void SpawnPirate(int zoneId)
        {
            SpawnInfo spawn = currentSpawns.FindZoneId(zoneId);
            if (spawn == null)
            {
                Log.Msg($"Spawn not found for ZoneId={zoneId}");
                return;
            }
            if (!spawn.PiratesEnabled)
            {
                //if (Log.Debug) Log.Msg($"spawn '{spawn.Name}' PiratesEnabled = False;");
                return;
            }
            AddPirateSpawn(spawn);
        }

        private bool AddPirateSpawn(SpawnInfo spawn)
        {
            if (Log.Debug) Log.Msg($"Starting AddPirateSpawn for spawn={spawn.Name} prefabName='{config.PiratePrefab}'");

            //MyPrefabDefinition prefabDefinition = MyDefinitionManager.Static.GetPrefabDefinition(prefabName);

            PrefabInfoInternal spawnPrefab = null;
            if (!prefabs.TryGetValue(spawn.PrefabName, out spawnPrefab))
            {
                Log.Msg($"Could not find spawn prefab '{spawn.PrefabName}'");
                return false;
            }


            //find free position
            int i = 20;
            Vector3D? spawnPosition = null;
            while (i > 0 && spawnPosition == null)
            {
                --i;
                spawnPosition = spawn.Position + spawnPrefab.PirateRadius * MyUtils.GetRandomVector3Normalized();

                if (MyAPIGateway.GravityProviderSystem.IsPositionInNaturalGravity(spawnPosition.Value, 2000))  //more than 2Km outside grav
                    continue;

                spawnPosition = MyAPIGateway.Entities.FindFreePlace(spawnPosition.Value, 100);
            }

            if (spawnPosition == null)
            {
                Log.Msg($"Could not find free position for pirate");
                return false;
            }

            SpawnInfo pirateSpawn = new SpawnInfo();
            pirateSpawn.Type = SpawnType.Pirate;
            pirateSpawn.Position = spawnPosition.Value;
            pirateSpawn.RemoveAt = spawn.RemoveAt;
            pirateSpawn.Name = $"AnomPirate#{spawn.AnomalyId}";
            pirateSpawn.PrefabName = config.PiratePrefab;
            pirateSpawn.PirateAntenna = spawnPrefab.PirateAntenna;
            pirateSpawn.AnomalyId = spawn.AnomalyId;

            MyVisualScriptLogicProvider.SpawnPrefab(config.PiratePrefab, spawnPosition.Value, Vector3D.Forward, Vector3D.Up, pirateOwnerId, spawningOptions: SpawningOptions.UseOnlyWorldMatrix | SpawningOptions.SetAuthorship);

            currentSpawns.Spawns.Add(pirateSpawn);
            spawn.PiratesEnabled = false; //Stop pirates spawning again.
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
            //if (Log.Debug)
            //{
            //    var bigOwner = cubeGrid?.BigOwners.Count > 0 ? cubeGrid?.BigOwners[0] : 0;
            //    Log.Msg($"Grid name={prefabName} BigOwners Count={cubeGrid?.BigOwners.Count} BigOwners[0]={bigOwner} FactionOwner={factionOwnerId}");
            //}
            if (cubeGrid?.BigOwners.Count == 0 || (cubeGrid?.BigOwners[0] != factionOwnerId && cubeGrid?.BigOwners[0] != pirateOwnerId))
            {
                //if (Log.Debug) Log.Msg($"Rejecting name={prefabName}");
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
                    if (spawn.Type == SpawnType.Wreck)
                    {
                        CheckSubZone(spawn);
                        if (Log.Debug) Log.Msg($"Spawned '{spawn.Name}' prefab={spawn.PrefabName} GroupId={spawn.GroupId} ZoneId={spawn.ZoneId} RemoveAt={new DateTime(spawn.RemoveAt)}");

                    }
                    else
                    {
                        foreach (var antenna in cubeGrid.GetFatBlocks<IMyRadioAntenna>())
                        {
                            antenna.CustomName = spawn.PirateAntenna;
                            antenna.EnableBroadcasting = true;
                            antenna.Enabled = true;
                        }
                        if (Log.Debug) Log.Msg($"Spawned pirate '{spawn.Name}' prefab={spawn.PrefabName} PirateAntenna={spawn.PirateAntenna} RemoveAt={new DateTime(spawn.RemoveAt)}");
                    }

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
            if (spawn.Type == SpawnType.Wreck && spawn.ZoneId >= 0)
            {
                ZonesSession.Instance.SubZoneTable.RemoveZone(spawn.ZoneId);
                if (Log.Debug) Log.Msg($"Removed SubZone {spawn.ZoneId}");
            }

            //close grid
            var cubeGrid = MyAPIGateway.Entities.GetEntityById(spawn.EntityId) as IMyCubeGrid;
            if (cubeGrid != null && spawn.Type == SpawnType.Wreck)
            {
                bool factionOwned = cubeGrid?.BigOwners.Count > 0 && cubeGrid?.BigOwners[0] == factionOwnerId;

                if (Vector3D.Distance(cubeGrid.GetPosition(), spawn.SubZonePosition) < config.AlertRadius || factionOwned)
                {
                    if (Log.Debug) Log.Msg($"Closing '{cubeGrid.DisplayName}' ");
                    List<IMyCubeGrid> cubeGrids = new List<IMyCubeGrid>();
                    cubeGrid.GetGridGroup(GridLinkTypeEnum.Mechanical).GetGrids(cubeGrids);
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
            else
            {
                if (Log.Debug) Log.Msg($"Closing '{cubeGrid.DisplayName}' ");
                List<IMyCubeGrid> cubeGrids = new List<IMyCubeGrid>();
                cubeGrid.GetGridGroup(GridLinkTypeEnum.Mechanical).GetGrids(cubeGrids);
                foreach (var subGrid in cubeGrids)
                {
                    foreach (var cockpit in subGrid.GetFatBlocks<IMyCockpit>())
                    {
                        cockpit.RemovePilot();
                    }
                    subGrid.Close();
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
                MyAPIGateway.Utilities.SetVariable<string>(VariableId, Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary<CurrentSpawnsData>(currentSpawns)));
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
            if (spawn.ZoneId >= 0 || spawn.Type != SpawnType.Wreck)
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

        public void AddPlayerGPS(int button, long identityId)
        {
            if (button == 0)
                return;
            if (spawnSummary.Selected.Count >= button)
            {
                var sel = spawnSummary.Selected[button - 1];
                if (Log.Debug) Log.Msg($"AddPlayerGPS Button {button} pressed by {identityId} {sel.Name}");
                MyVisualScriptLogicProvider.AddGPS(sel.Name, "Aproximate position of detected navigation hazzard.", sel.SubZonePosition, VRageMath.Color.White, GPSDisplayPeriod, identityId);
                MyVisualScriptLogicProvider.SendChatMessageColored($"Added GPS for {sel.Name}", Color.Yellow, "", identityId);
            }
        }
    }
}
