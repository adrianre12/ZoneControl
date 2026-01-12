using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using static ZoneControl.Utils;
using static ZoneControl.ZonesSession;

namespace ZoneControl.Spawner
{
    internal partial class SpawnerSession
    {
        private Queue<CmdMsg> cmdQueue = new Queue<CmdMsg>();
        private bool updateSpawns;
        private int nextSpawnIndex = -1;
        private Random rng = new Random();

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
                        if (CurrentSpawns.Spawns.Count > 0)
                            RemoveAllSpawns();
                        Log.Msg($"All spawns removed.", playerId);
                        break;
                    }

                case "SetSpawnCounter":
                    {
                        if (Config.Enabled)
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
                        CurrentSpawns.SpawnCounter = value;
                        SaveCurrentSpawns();
                        break;
                    }

                case "AddSpawn":
                    {
                        if (!Config.Enabled)
                        {
                            Log.Msg("Spwaner must be enabled to run AddSpawn", playerId);
                            break;
                        }

                        if (CurrentSpawns.Spawns.Count >= Config.MaxSpawns)
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
                        sb.AppendLine($"Enabled: {Config.Enabled}");
                        sb.AppendLine($"Spawns: {CurrentSpawns.Spawns.Count} of {Config.MaxSpawns}");
                        int i = 2;
                        foreach (var spawn in CurrentSpawns.Spawns)
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
        do commands and update
        internal void Update(int currentFrame)
        {
            if (cmdQueue.Count > 0)
            {
                CommandHandler(cmdQueue.Dequeue());
                return;
            }

            if (!Config.Enabled)
            {
                if (CurrentSpawns.Spawns.Count > 0)
                    RemoveAllSpawns();
                return;
            }

            if (updateSpawns)
            {
                //Log.Msg($"updateSpawns={updateSpawns} nextSpawnIndex={nextSpawnIndex}");

                //do the loop
                if (nextSpawnIndex >= 0)
                {// update spawns
                    SpawnInfo spawn = CurrentSpawns.Spawns[nextSpawnIndex];
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
                            ZonesSession.Instance.SubZoneTable.AddExtraMessage(spawn.ZoneId, Config.MessageUrgent, Config.MessageColour, true);
                        }
                        else
                        {
                            //Log.Msg($"Adding Warn Msg '{configSpawner.MessageWarn}'");
                            ZonesSession.Instance.SubZoneTable.AddExtraMessage(spawn.ZoneId, Config.MessageWarn, Config.MessageColour, false);
                        }
                    }
                    CheckSubZone(spawn);
                    --nextSpawnIndex;
                    return;
                }

                //All done
                if (Config.Enabled && CurrentSpawns.Spawns.Count < Config.MaxSpawns)
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
            nextSpawnIndex = CurrentSpawns.Spawns.Count - 1;
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
            newSpawn.SubZonePosition = spawnPosition.Value + 0.8f * Config.AlertRadius * (float)rng.NextDouble() * MyUtils.GetRandomVector3Normalized();
            newSpawn.RemoveAt = DateTime.Now.Ticks + (long)(DateTimeTicksPerHour * (selectedPrefab.LifetimeMin + ((selectedPrefab.LifetimeMax - selectedPrefab.LifetimeMin) * rng.NextDouble())));
            ++CurrentSpawns.SpawnCounter;
            newSpawn.AnomalyId = CurrentSpawns.SpawnCounter;
            newSpawn.Name = $"Anomaly#{CurrentSpawns.SpawnCounter}";
            //newSpawn.DPname = TextReplace(configSpawner.DataPadTitle, "[NAME]", newSpawn.Name);
            //newSpawn.DPdata = TextReplace(configSpawner.DataPadMessage, "[NAME]", newSpawn.Name, "[GPS]", ZonesConfigBase.VectorToGPS(newSpawn.Name, newSpawn.Position, configSpawner.GPScolourHex));

            MyVisualScriptLogicProvider.SpawnPrefab(selectedPrefab.Subtype, spawnPosition.Value, Vector3D.Forward, Vector3D.Up, factionOwnerId, spawningOptions: SpawningOptions.RotateFirstCockpitTowardsDirection | SpawningOptions.UseOnlyWorldMatrix);

            CurrentSpawns.Spawns.Add(newSpawn);
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

            foreach (var spawn in CurrentSpawns.Spawns)
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
            Log.Msg("Spawnwer could not find Entity in CurrentSpawns");
        }

        private void RemoveAllSpawns()
        {
            for (int i = CurrentSpawns.Spawns.Count - 1; i >= 0; --i)
            {
                var spawn = CurrentSpawns.Spawns[i];
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
                if (Vector3D.Distance(grid.GetPosition(), spawn.SubZonePosition) < Config.AlertRadius)
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
            CurrentSpawns.Spawns.Remove(spawn);
            if (save)
                SaveCurrentSpawns();
        }

        private void SaveCurrentSpawns()
        {
            try
            {
                MyAPIGateway.Utilities.SetVariable<string>(VariableId, Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(CurrentSpawns)));
            }
            catch (Exception e)
            {
                Log.Msg($"Error serializing CurrentSpawns\n {e}");
            }
            if (Log.Debug)
                foreach (var spawn in CurrentSpawns.Spawns)
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
                anomaly.AlertRadius = Config.AlertRadius;
                anomaly.AlertRadiusSqrd = Config.AlertRadius * Config.AlertRadius;
                anomaly.AlertMessageEnter = TextReplace(Config.AlertMessageEnter, "[NAME]", spawn.Name);
                anomaly.ColourEnter = CheckColour(Config.ColourEnter);
                anomaly.AlertMessageLeave = TextReplace(Config.AlertMessageLeave, "[NAME]", spawn.Name);
                anomaly.ColourLeave = CheckColour(Config.ColourLeave);
                anomaly.AlertTimeMs = Config.AlertTimeMs;
                spawn.ZoneId = ZonesSession.Instance.SubZoneTable.AddZone(anomaly);
                if (Log.Debug) Log.Msg($"Added SubZone {spawn.ZoneId} {spawn.Name}");
                return;
            }
        }
    }
}
