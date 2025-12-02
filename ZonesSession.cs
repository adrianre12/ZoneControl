using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;
using static ZoneControl.ZoneControlBase;
using static ZoneControl.ZonesConfig;

namespace ZoneControl
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    internal class ZonesSession : MySessionComponentBase
    {
        const int DefaultRefreshPeriod = 600; // 10s
        const int DefaultPunishmentPeriod = 18000; // 5 mins
        const double CacheMovementLimitSqrd = 100; //10m

        public static ZonesSession Instance;

        private ZonesConfig config;
        private List<ZoneInfo> zones = new List<ZoneInfo>();
        private List<IMyPlayer> players = new List<IMyPlayer>();
        private int nextRefreshFrame = 0;
        private int nextPlayerIndex = 0;
        private ZoneInfo currentZone = null;

        private struct ZoneCacheItem
        {
            public Vector3D Position;
            public ZoneInfo Zone;
        }
        private Dictionary<long, ZoneCacheItem> zoneCache = new Dictionary<long, ZoneCacheItem>();

        private class PlayerState
        {
            public IMyPlayer Player = null;
            public bool IsIntruder = false;

            public PlayerState()
            {
            }

            public PlayerState(IMyPlayer player, bool isIntruder = false)
            {
                Player = player;
                IsIntruder = isIntruder;
            }
        }

        private PlayerState ps = new PlayerState();
        private Dictionary<long, int> punishmentCache = new Dictionary<long, int>();

        public override void LoadData()
        {
            Instance = this;
        }
        protected override void UnloadData()
        {
            Instance = null;
        }

        public override void BeforeStart()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            Log.Msg("ZoneNotification Start");
            config = ZonesConfig.Load();

            Dictionary<string, Vector3D> planetPositions = GetPlanetPositions();

            foreach (var info in config.Positions)
            {
                zones.Add(new ZoneInfo(info));
                Log.Msg($"Adding Zone {info.UniqueName} to Zones list");
            }

            Vector3D planetPosition;
            foreach (var info in config.Planets)
            {
                if (planetPositions.TryGetValue(info.PlanetName, out planetPosition))
                {
                    zones.Add(new ZoneInfo(info, planetPosition));
                    Log.Msg($"Adding Planet Zone {info.PlanetName} to Zones list");
                }
            }


            zones = zones.OrderBy(x => x.AlertRadius).ToList();
            //foreach (var zone in zones) Log.Msg($"Zone {zone.UniqueName} radius {zone.AlertRadius}");
        }

        public override void UpdateAfterSimulation()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;
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
                //isIntruder not set, check position
                CheckPlayerPosition();
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

        private Dictionary<string, Vector3D> GetPlanetPositions()
        {
            Dictionary<string, Vector3D> planetPositions = new Dictionary<string, Vector3D>();
            MyAPIGateway.Entities.GetEntities(null, e =>
            {
                if (e is MyPlanet)
                {
                    var planet = e as MyPlanet;
                    if (planetPositions.ContainsKey(planet.StorageName))
                    {
                        Log.Msg($"Error duplicate planet name found: {planet.StorageName}");
                        return false;
                    }
                    Log.Msg($"Planet Found {planet.StorageName}");
                    planetPositions.Add(planet.StorageName, planet.WorldMatrix.Translation);
                }
                return false;
            });
            return planetPositions;
        }

        private void RefreshPlayers()
        {
            players.Clear();
            MyAPIGateway.Players.GetPlayers(players);
            nextPlayerIndex = 0;
            Log.Msg($"RefreshPlayers count={players.Count}");

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

        public ZoneInfo FindClosestZoneCached(long Id, Vector3D position)
        {
            // check cached
            ZoneCacheItem cacheItem;
            if (zoneCache.TryGetValue(Id, out cacheItem))
            {
                if (Vector3D.DistanceSquared(cacheItem.Position, position) < CacheMovementLimitSqrd)
                {
                    if (cacheItem.Zone.InZone(position))
                    {
                        Log.Msg("Cache Hit");
                        return cacheItem.Zone;
                    }
                }
                zoneCache.Remove(Id);
            }
            // cache miss find closest
            Log.Msg("Cache Miss");

            ZoneInfo zone = FindClosestZone(Id, position);
            if (zone != null)
            {
                zoneCache[Id] = new ZoneCacheItem() { Position = position, Zone = zone };
                return zone;
            }
            return null;
        }

        private ZoneInfo FindClosestZone(long Id, Vector3D position)
        {
            ZoneInfo zone = null;
            //Log.Msg($"zones.count={zones.Count}");
            for (int i = 0; i < zones.Count; i++)
            {
                zone = zones[i];
                if (zone.AlertRadius == 0)
                    continue;
                //Log.Msg($"zone {zone.UniqueName} position={zone.Position}");
                if (zone.InZone(position))
                    return zone;
            }
            return null;
        }

        private ZoneInfo CheckPlayerPosition()
        {
            if (ps.Player == null)
                return null;

            Log.Msg($"CheckPlayerPosition... {ps.Player.DisplayName} ------------------------");

            Vector3D playerPosition = ps.Player.GetPosition();

            currentZone = FindClosestZone(ps.Player.IdentityId, playerPosition);
            ZoneCacheItem cached;
            bool cacheHit = zoneCache.TryGetValue(ps.Player.IdentityId, out cached);


            if (currentZone == null) //not in a zone
            {
                if (cacheHit) //we were in a zone
                {
                    Log.Msg($"Left zone {cached.Zone.UniqueName} to void");
                    zoneCache.Remove(ps.Player.IdentityId); // become zoneless, find it on the next pass
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
                    Log.Msg($"Already in zone {currentZone.UniqueName}");
                    return currentZone;
                }

                Log.Msg($"Change zone from {cached.Zone.UniqueName} to {currentZone.UniqueName}");
                if (cached.Zone.AlertMessageLeave.Length > 0)
                    MyVisualScriptLogicProvider.ShowNotification(cached.Zone.AlertMessageLeave,
                    disappearTimeMs: cached.Zone.AlertTimeMs, font: cached.Zone.ColourLeave,
                    playerId: ps.Player.IdentityId);
                zoneCache[ps.Player.IdentityId] = new ZoneCacheItem() { Position = playerPosition, Zone = currentZone };
                if (currentZone.AlertMessageEnter.Length > 0)
                    MyVisualScriptLogicProvider.ShowNotification(currentZone.AlertMessageEnter, disappearTimeMs: currentZone.AlertTimeMs,
                    font: currentZone.ColourEnter, playerId: ps.Player.IdentityId);
                return currentZone;
            }

            // cache miss 

            Log.Msg($"Entered zone {currentZone.UniqueName} from void");
            if (currentZone.AlertMessageEnter.Length > 0)
                MyVisualScriptLogicProvider.ShowNotification(currentZone.AlertMessageEnter,
                disappearTimeMs: currentZone.AlertTimeMs, font: currentZone.ColourEnter, playerId: ps.Player.IdentityId);
            zoneCache.Add(ps.Player.IdentityId, new ZoneCacheItem() { Position = playerPosition, Zone = currentZone });

            return currentZone;
        }

        private bool CheckIfIntruding()
        {
            if (currentZone == null || ps.Player == null)//|| ps.Player.PromoteLevel != MyPromoteLevel.None)
            {
                return false;
            }

            string playerFactionTag = MyVisualScriptLogicProvider.GetPlayersFactionTag(ps.Player.IdentityId).Trim();


            Log.Msg($"CheckIfIntruding {ps.Player.DisplayName} player factionTag={playerFactionTag} zone {currentZone.UniqueName} {currentZone.FactionTag}");

            if (!currentZone.NoIntruders || currentZone.FactionTag == null || currentZone.FactionTag.Trim().Length == 0)
                return false;

            if (playerFactionTag != null && playerFactionTag == currentZone.FactionTag.Trim())
                return false;

            Vector3D position = ps.Player.GetPosition();

            Log.Msg($"Intruder: GPS:{ps.Player.DisplayName}:{position.X}:{position.Y}:{position.Z}:#FFF17575:");

            MyVisualScriptLogicProvider.ShowNotification(config.IntruderMessage, config.IntruderAlertTimeMs, config.IntruderColour, playerId: ps.Player.IdentityId);
            return true;
        }

        private void Punish()
        {
            if (ps.Player.Character.UsingEntity is MyCockpit)
            {
                Log.Msg("Punish -----------------");
                var cockpit = ps.Player.Character.UsingEntity as IMyCockpit;
                if (cockpit.CubeGrid == null)
                {
                    Log.Msg("cubegrid null");
                    return;
                }
                var grid = cockpit.CubeGrid;

                Log.Msg($"Player '{ps.Player.DisplayName}' grid name '{grid.DisplayName}' ");

                int expiryFrame;
                if (punishmentCache.TryGetValue(grid.EntityId, out expiryFrame))
                {
                    if (expiryFrame > MyAPIGateway.Session.GameplayFrameCounter)
                        return;
                    punishmentCache.Remove(grid.EntityId);
                }

                punishmentCache.Add(grid.EntityId, MyAPIGateway.Session.GameplayFrameCounter + DefaultPunishmentPeriod);
                MyVisualScriptLogicProvider.ShowNotification(config.IntruderPunishmentMsg, config.IntruderAlertTimeMs, config.IntruderColour, playerId: ps.Player.IdentityId);
                MyVisualScriptLogicProvider.SendChatMessage($"{config.IntruderChatMessagePt1} '{ps.Player.DisplayName}' {config.IntruderChatMessagePt2}", config.ChatSenderName, -1, config.IntruderColour);

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

            }


            //cockpit.RemovePilot();
            //cockpit.CubeGrid.Close();



            //MyVisualScriptLogicProvider.ShowNotification(config.IntruderMessage, config.IntruderAlertTimeMs, config.IntruderColour, playerId: player.IdentityId);
            //player.Character.GetInventory().Clear();

            //MyVisualScriptLogicProvider.SetPlayersHydrogenLevel(player.IdentityId, 0);
            //if (MyVisualScriptLogicProvider.GetPlayersEnergyLevel(player.IdentityId) > 0.01) MyVisualScriptLogicProvider.SetPlayersEnergyLevel(player.IdentityId, 0.01f);
        }

    }
}
