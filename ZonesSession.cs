using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;
using static ZoneControl.ZonesConfig;

namespace ZoneControl
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    internal class ZonesSession : MySessionComponentBase
    {
        const int DefaultTickCounter = 600; // 10s
        const double CacheMovementLimitSqrd = 100; //10m

        public static ZonesSession Instance;

        private ZonesConfig config;
        private List<ZoneInfo> zones = new List<ZoneInfo>();
        private int tickCounter = DefaultTickCounter;
        private List<IMyPlayer> players = new List<IMyPlayer>();

        private struct ZoneCacheItem
        {
            public Vector3D Position;
            public ZoneInfo Zone;
        }
        private Dictionary<long, ZoneCacheItem> zoneCache = new Dictionary<long, ZoneCacheItem>();

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

            if (tickCounter-- > 0) return;
            tickCounter = DefaultTickCounter;

            RefreshPlayers(); // do I want to do this less often?

            /*            Log.Msg($"{FindClosestZone(1, Vector3D.Zero).UniqueName}");
                        Log.Msg($"{FindClosestZone(1, new Vector3D(100, 0, 0)).UniqueName}");
                        Log.Msg($"{FindClosestZoneCached(1, Vector3D.Zero).UniqueName}");
                        Log.Msg($"{FindClosestZoneCached(1, Vector3D.Zero).UniqueName}");
                        Log.Msg($"{FindClosestZoneCached(1, new Vector3D(100, 0, 0)).UniqueName}");*/


            CheckPlayerPositions();
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
            //Log.Msg("RefreshPlayers");
            players.Clear();
            MyAPIGateway.Players.GetPlayers(players);
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
                //Log.Msg($"zone {zone.UniqueName} position={zone.Position}");
                if (zone.InZone(position))
                    return zone;
            }
            return null;
        }

        private void CheckPlayerPositions()
        {
            //Log.Msg("CheckPlayerPositions");

            foreach (var player in players)
            {
                ZoneInfo closestZone = CheckPlayerPosition(player);
                CheckIfIntruding(player, closestZone);
            }

        }

        enum MessageType
        {
            None,
            Enter,
            Leave
        }

        private ZoneInfo CheckPlayerPosition(IMyPlayer player)
        {
            Log.Msg($"CheckPlayerPosition... {player.DisplayName} ------------------------");

            Vector3D playerPosition = player.GetPosition();

            ZoneInfo currentZone = FindClosestZone(player.IdentityId, playerPosition);

            if (currentZone != null && currentZone.AlertRadius == 0)
                return null;
            if (currentZone != null)
                Log.Msg($"Zone {currentZone.UniqueName} Position {currentZone.Position} ");
            else
                Log.Msg("No zone found");

            MessageType messageType = MessageType.None;

            ZoneCacheItem playerCachedZone;
            if (zoneCache.TryGetValue(player.IdentityId, out playerCachedZone))
            {
                if (currentZone != null && playerCachedZone.Zone == currentZone)
                { //already in a zone
                    Log.Msg("Already in zone");
                    messageType = MessageType.None;
                }
                else
                { // leaving cahched zone
                    Log.Msg("Left zone");
                    currentZone = playerCachedZone.Zone; // so we get the right message
                    zoneCache.Remove(player.IdentityId); // become zoneless, find it on the next pass
                    messageType = MessageType.Leave;
                }
            }
            else if (currentZone != null)
            { //entered zone from void
                Log.Msg("Entered zone from void");
                messageType = MessageType.Enter;
                zoneCache.Add(player.IdentityId, new ZoneCacheItem() { Position = playerPosition, Zone = currentZone });
            }
            else
            { //no currentZone no playerCachedZone, in the void
                Log.Msg("Leaving zone to void");
                return null;
            }


            Log.Msg($"MessageType={messageType} ClosestZone={currentZone.UniqueName} FactionTag={currentZone.FactionTag}");
            switch (messageType)
            {
                case MessageType.Enter:
                    {
                        MyVisualScriptLogicProvider.ShowNotification(currentZone.AlertMessageEnter, disappearTimeMs: currentZone.AlertTimeMs, font: currentZone.ColourEnter, playerId: player.IdentityId);
                        break;
                    }
                case MessageType.Leave:
                    {
                        MyVisualScriptLogicProvider.ShowNotification(currentZone.AlertMessageLeave, disappearTimeMs: currentZone.AlertTimeMs, font: currentZone.ColourLeave, playerId: player.IdentityId);
                        break;
                    }
                default:
                    break;
            }

            return currentZone;
        }

        private void CheckIfIntruding(IMyPlayer player, ZoneInfo closestZone)
        {
            if (closestZone == null || player == null)//|| player.PromoteLevel != MyPromoteLevel.None)
            {
                return;
            }

            Log.Msg($"CheckIfIntruding {player.DisplayName} zone {closestZone.UniqueName} {closestZone.FactionTag}");
            if (!closestZone.NoIntruders || closestZone.FactionTag == null || closestZone.FactionTag.Trim().Length == 0)
                return;

            Vector3D position = player.GetPosition();

            MyVisualScriptLogicProvider.ShowNotification(config.IntruderMessage, config.IntruderAlertTimeMs, config.IntruderColour, playerId: player.IdentityId);

            if (player.Character.UsingEntity is MyCockpit)
            {
                Log.Msg("----------------- Cockpit");
                var cockpit = player.Character.UsingEntity as IMyCockpit;
                if (cockpit.CubeGrid == null)
                {
                    Log.Msg("cubegrid null");
                }
                else
                {
                    Log.Msg($"grid name {cockpit.CubeGrid.DisplayName}");
                    //cockpit.RemovePilot();
                    //cockpit.CubeGrid.Close();
                }
            }

            //MyVisualScriptLogicProvider.ShowNotification(config.IntruderMessage, config.IntruderAlertTimeMs, config.IntruderColour, playerId: player.IdentityId);
            //player.Character.GetInventory().Clear();

            //MyVisualScriptLogicProvider.SetPlayersHydrogenLevel(player.IdentityId, 0);
            //if (MyVisualScriptLogicProvider.GetPlayersEnergyLevel(player.IdentityId) > 0.01) MyVisualScriptLogicProvider.SetPlayersEnergyLevel(player.IdentityId, 0.01f);
            MyVisualScriptLogicProvider.SendChatMessage($"{config.IntruderChatMessagePt1} '{player.DisplayName}' {config.IntruderChatMessagePt2}", config.ChatSenderName, -1, config.IntruderColour);
        }

    }
}
