using ProtoBuf;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;
using static ZoneControl.ZoneConfigBase;
using static ZoneControl.ZoneConfigBase.IntruderInfo;
using static ZoneControl.ZoneControlBase;
using static ZoneControl.ZonesConfig;
using static ZoneControl.ZonesConfig.ZoneInfoInternal;

namespace ZoneControl
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    internal class ZonesSession : MySessionComponentBase
    {
        const int DefaultRefreshPeriod = 600; // 10s
        const int DefaultPunishmentPeriod = 18000; // 5 mins
        const double CacheMovementLimitSqrd = 100; //10m
        const string VariableId = nameof(ZonesSession);

        public static ZonesSession Instance;

        private IMyModContext Mod;
        private ZonesConfig config;
        private List<ZoneInfoInternal> zones = new List<ZoneInfoInternal>();
        private List<IMyPlayer> players = new List<IMyPlayer>();
        private int nextRefreshFrame = 0;
        private int nextPlayerIndex = 0;
        private ZoneInfoInternal currentZone = null;

        [ProtoContract]
        private class ZoneTargets
        {
            [ProtoMember(1)]
            public Dictionary<long, List<GPSposition>> Targets = new Dictionary<long, List<GPSposition>>();

            public ZoneTargets() { }
        }
        private ZoneTargets zoneTargets = new ZoneTargets();

        private struct ZoneCacheItem
        {
            public Vector3D Position;
            public ZoneInfoInternal Zone;
        }
        private Dictionary<long, ZoneCacheItem> zoneCache = new Dictionary<long, ZoneCacheItem>();
        private Dictionary<long, ZoneCacheItem> subZoneCache = new Dictionary<long, ZoneCacheItem>();


        private class PlayerState
        {
            public IMyPlayer Player = null;
            public bool IsIntruder = false;

            public PlayerState()
            {
            }
        }

        private PlayerState ps = new PlayerState();
        private Dictionary<long, int> punishmentCache = new Dictionary<long, int>();

        public override void LoadData()
        {
            Instance = this;
            Mod = ModContext;
            Log.Msg("LoadData...........");
            if (MyAPIGateway.Session.IsServer)
                LoadDataOnHost();
            else
                LoadDataOnClient();
        }

        protected override void UnloadData()
        {
            Instance = null;
        }

        public void LoadDataOnHost()
        {
            Log.Msg("ZoneNotification Host Start");
            config = ZonesConfig.LoadConfig();
            zones = ZonesConfig.NewZoneList(config);

            foreach (var zone in zones)
            {
                if (zone.Type == ZoneInfoInternal.ZoneType.Wormhole)
                    zoneTargets.Targets.Add(zone.Id, zone.Targets);
            }

            try
            {
                string saveText = Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(zoneTargets));
                MyAPIGateway.Utilities.SetVariable<string>(VariableId, saveText);
            }
            catch (Exception e)
            {
                Log.Msg($"Error serializing zoneTargets\n {e}");
            }
        }

        public void LoadDataOnClient()
        {
            Log.Msg("ZoneNotification Client Start");

            try
            {
                string saveText;
                if (!MyAPIGateway.Utilities.GetVariable<string>(VariableId, out saveText))
                    throw new Exception($"Variable {VariableId} not found in sandbox.sbc!");
                zoneTargets = MyAPIGateway.Utilities.SerializeFromBinary<ZoneTargets>(Convert.FromBase64String(saveText));
            }
            catch (Exception e)
            {
                Log.Msg($"Error deserializing zoneTargets\n {e}");
                zoneTargets = new ZoneTargets();
            }
        }

        public override void UpdateAfterSimulation()
        {
            if (MyAPIGateway.Session.IsServer)
                UpdateAfterSimulationHost();
            else
                UpdateAfterSimulationClient();
        }

        public void UpdateAfterSimulationHost()
        {
            var currentFrame = MyAPIGateway.Session.GameplayFrameCounter;

            if (ps.Player != null)
            { // process player
                //isIntruder set. punish
                if (ps.IsIntruder)
                {
                    Punish();
                    NextPlayer();
                    return;
                }


                CheckPlayerPosition(subZoneCache, ZoneType.Wormhole | ZoneType.Anomaly);

                //isIntruder not set, check position
                //CheckPlayerPosition(zoneCache, ZoneType.Zone);
                // check if intruding
                if (CheckIfIntruding())
                {
                    ps.IsIntruder = true;

                    return;
                }

                NextPlayer();
                return;
            }

            if (currentFrame < nextRefreshFrame)
                return;

            nextRefreshFrame = currentFrame + DefaultRefreshPeriod;
            RefreshPlayers();
            NextPlayer();

            /* cache tests
            Log.Msg($"{FindClosestZone(1, Vector3D.Zero).UniqueName}");
            Log.Msg($"{FindClosestZone(1, new Vector3D(100, 0, 0)).UniqueName}");
            Log.Msg($"{FindClosestZoneCached(1, Vector3D.Zero).UniqueName}");
            Log.Msg($"{FindClosestZoneCached(1, Vector3D.Zero).UniqueName}");
            Log.Msg($"{FindClosestZoneCached(1, new Vector3D(100, 0, 0)).UniqueName}");
            */
        }

        public void UpdateAfterSimulationClient()
        {
            //Log.Msg($"Client {zoneTargets.Targets.Count}");

        }

        private void RefreshPlayers()
        {
            players.Clear();
            MyAPIGateway.Players.GetPlayers(players);
            nextPlayerIndex = 0;
        }

        private void NextPlayer()
        {
            //Log.Msg($"NextPlayer {nextPlayerIndex}");
            if (nextPlayerIndex >= players.Count)
            {
                ps = new PlayerState();
                return;
            }
            ps.Player = players[nextPlayerIndex];
            ps.IsIntruder = false;
            ++nextPlayerIndex;
        }

        public ZoneInfoInternal FindClosestWormholeCached(long Id, Vector3D position)
        {
            // check cached
            ZoneCacheItem cacheItem;
            if (zoneCache.TryGetValue(Id, out cacheItem))
            {
                if (Vector3D.DistanceSquared(cacheItem.Position, position) < CacheMovementLimitSqrd)
                {
                    if (cacheItem.Zone.InZone(position))
                    {
                        //Log.Msg("Cache Hit");
                        return cacheItem.Zone;
                    }
                }
                zoneCache.Remove(Id);
            }
            // cache miss find closest
            //Log.Msg("Cache Miss");

            ZoneInfoInternal zone = FindClosestZone(ZoneType.Wormhole, Id, position);
            if (zone != null)
            {
                zoneCache[Id] = new ZoneCacheItem() { Position = position, Zone = zone };
                return zone;
            }
            return null;
        }

        private ZoneInfoInternal FindClosestZone(ZoneType zoneType, long Id, Vector3D position)
        {
            ZoneInfoInternal tmpZone = null;
            ZoneInfoInternal zone = null;
            bool foundZone = false;
            double distance;
            double foundDistance = 0;
            double foundRadius = 0;

            Log.Msg($"zoneType={zoneType}");
            for (int i = 0; i < zones.Count; i++)
            {
                tmpZone = zones[i];
                if (!zoneType.HasFlag(tmpZone.Type))
                    continue;
                if (tmpZone.AlertRadius == 0)
                    continue;
                Log.Msg($"zone {zone.UniqueName} position={zone.Position} Type={zone.Type}");
                distance = Vector3D.DistanceSquared(position, tmpZone.Position);
                if (foundZone) //already found a zone, look for more
                {
                    if (foundRadius != tmpZone.AlertRadiusSqrd) //not same size as zone, return zone
                        return zone;

                    if (distance > tmpZone.AlertRadiusSqrd || distance >= foundDistance) //It is not closer, look for another
                        continue;

                    foundDistance = distance;
                    zone = tmpZone;
                }
                else
                {
                    if (distance < tmpZone.AlertRadiusSqrd) // in the first zone
                    {
                        foundZone = true;
                        zone = tmpZone;
                        foundDistance = distance;
                        foundRadius = tmpZone.AlertRadiusSqrd;
                    }
                }
            }
            if (foundZone) // catch the last found zone
                return zone;

            return null;
        }

        public List<GPSposition> GetZoneTargets(long zoneId)
        {
            return zoneTargets.Targets.GetValueOrDefault(zoneId, new List<GPSposition>());
        }

        private ZoneInfoInternal CheckPlayerPosition(Dictionary<long, ZoneCacheItem> cache, ZoneType zoneType)
        {
            if (ps.Player == null)
                return null;

            //Log.Msg($"CheckPlayerPosition... {ps.Player.DisplayName} ------------------------");

            Vector3D playerPosition = ps.Player.GetPosition();

            currentZone = FindClosestZone(zoneType, ps.Player.IdentityId, playerPosition);
            ZoneCacheItem cached;
            bool cacheHit = cache.TryGetValue(ps.Player.IdentityId, out cached);


            if (currentZone == null) //not in a zone
            {
                if (cacheHit) //we were in a zone
                {
                    //Log.Msg($"Left zone {cached.Zone.UniqueName} to void");
                    cache.Remove(ps.Player.IdentityId); // become zoneless, find it on the next pass
                    if (cached.Zone.AlertMessageLeave.Length > 0)
                        MyVisualScriptLogicProvider.ShowNotification(cached.Zone.AlertMessageLeave,
                        disappearTimeMs: cached.Zone.AlertTimeMs, font: cached.Zone.ColourLeave,
                        playerId: ps.Player.IdentityId);
                }
                return null;
            }

            //currentZone != null
            if (cacheHit)
            {
                if (currentZone == cached.Zone) // In the same zone
                {
                    //Log.Msg($"Already in zone {currentZone.UniqueName}");
                    return currentZone;
                }

                //Log.Msg($"Change zone from {cached.Zone.UniqueName} to {currentZone.UniqueName}");
                if (cached.Zone.AlertMessageLeave.Length > 0)
                    MyVisualScriptLogicProvider.ShowNotification(cached.Zone.AlertMessageLeave,
                    disappearTimeMs: cached.Zone.AlertTimeMs, font: cached.Zone.ColourLeave,
                    playerId: ps.Player.IdentityId);
                cache[ps.Player.IdentityId] = new ZoneCacheItem() { Position = playerPosition, Zone = currentZone };
                if (currentZone.AlertMessageEnter.Length > 0)
                    MyVisualScriptLogicProvider.ShowNotification(currentZone.AlertMessageEnter, disappearTimeMs: currentZone.AlertTimeMs,
                    font: currentZone.ColourEnter, playerId: ps.Player.IdentityId);
                return currentZone;
            }

            // cache miss 

            //Log.Msg($"Entered zone {currentZone.UniqueName} from void");
            if (currentZone.AlertMessageEnter.Length > 0)
                MyVisualScriptLogicProvider.ShowNotification(currentZone.AlertMessageEnter,
                disappearTimeMs: currentZone.AlertTimeMs, font: currentZone.ColourEnter, playerId: ps.Player.IdentityId);
            cache.Add(ps.Player.IdentityId, new ZoneCacheItem() { Position = playerPosition, Zone = currentZone });

            return currentZone;
        }

        private bool CheckIfIntruding()
        {
            if (currentZone == null || ps.Player == null)
            {
                return false;
            }

            string playerFactionTag = MyVisualScriptLogicProvider.GetPlayersFactionTag(ps.Player.IdentityId).Trim();


            //Log.Msg($"CheckIfIntruding {ps.Player.DisplayName} player factionTag={playerFactionTag} zone {currentZone.UniqueName} {currentZone.FactionTag}");

            if (!currentZone.NoIntruders || currentZone.FactionTag == null || currentZone.FactionTag.Length == 0)
                return false;

            if (playerFactionTag == currentZone.FactionTag.Trim())
                return false;

            Vector3D position = ps.Player.GetPosition();

            MyVisualScriptLogicProvider.ShowNotification(config.Intruder.Message, config.Intruder.AlertTimeMs, config.Intruder.Colour, playerId: ps.Player.IdentityId);

            if (ps.Player.PromoteLevel != MyPromoteLevel.None && playerFactionTag != config.Intruder.AdminTestFactionTag.Trim())
                return false; //admins dont get punished unless in AdminTestFactionTag

            Log.Msg($"Intruder: GPS:{ps.Player.DisplayName}:{position.X}:{position.Y}:{position.Z}:#FFF17575:");
            return true;
        }

        private void Punish()
        {
            if (config.Intruder.Punishment == PunishmentType.None)
            {
                return;
            }

            // Punish player
            switch (config.Intruder.Punishment)
            {
                case PunishmentType.Destroy:
                    {
                        ps.Player.Character.GetInventory().Clear();

                        MyVisualScriptLogicProvider.SetPlayersHydrogenLevel(ps.Player.IdentityId, 0);
                        if (MyVisualScriptLogicProvider.GetPlayersEnergyLevel(ps.Player.IdentityId) > 0.01)
                            MyVisualScriptLogicProvider.SetPlayersEnergyLevel(ps.Player.IdentityId, 0.01f);
                        break;
                    }
                default:
                    break;
            }

            if (ps.Player.Character.UsingEntity is MyCockpit)
            {
                var cockpit = ps.Player.Character.UsingEntity as IMyCockpit;
                if (cockpit.CubeGrid == null)
                {
                    Log.Msg("Punish cubegrid null");
                    return;
                }
                var grid = cockpit.CubeGrid;

                Log.Msg($"Punish Player '{ps.Player.DisplayName}' grid name '{grid.DisplayName}' ");

                int expiryFrame;
                if (punishmentCache.TryGetValue(grid.EntityId, out expiryFrame))
                {
                    if (expiryFrame > MyAPIGateway.Session.GameplayFrameCounter)
                        return;
                    punishmentCache.Remove(grid.EntityId);
                }

                punishmentCache.Add(grid.EntityId, MyAPIGateway.Session.GameplayFrameCounter + DefaultPunishmentPeriod);
                MyVisualScriptLogicProvider.ShowNotification(config.Intruder.PunishmentMsg, config.Intruder.AlertTimeMs, config.Intruder.Colour, playerId: ps.Player.IdentityId);
                MyVisualScriptLogicProvider.SendChatMessage($"{config.Intruder.ChatMessagePt1} '{ps.Player.DisplayName}' {config.Intruder.ChatMessagePt2}", config.Intruder.ChatSenderName, 0, config.Intruder.Colour);

                switch (config.Intruder.Punishment)
                {

                    case PunishmentType.Disable:
                        {

                            foreach (var jd in grid.GetFatBlocks<IMyGyro>())
                            {
                                var fb = jd as IMyFunctionalBlock;
                                ZoneControlBase gl = fb.GameLogic?.GetAs<ZoneControlBase>();
                                if (gl == null)
                                    continue;

                                gl.SetOverride(OverrideState.Disabled);
                            }

                            foreach (var jd in grid.GetFatBlocks<IMyJumpDrive>())
                            {
                                var fb = jd as IMyFunctionalBlock;
                                ZoneControlBase gl = fb.GameLogic?.GetAs<ZoneControlBase>();
                                if (gl == null)
                                    continue;

                                gl.SetOverride(OverrideState.Disabled);
                            }
                            break;
                        }

                    case PunishmentType.Destroy:
                        {
                            cockpit.RemovePilot();
                            cockpit.CubeGrid.Close();
                            break;
                        }
                    default:
                        break;

                }
            }


        }

    }
}
