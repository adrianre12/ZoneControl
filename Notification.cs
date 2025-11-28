using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;
using static ZoneControl.Notification.NotificationConfig;

namespace ZoneControl.Notification
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    internal class Notification : MySessionComponentBase
    {
        const int DefaultTickCounter = 120; // 2s
        const int DefaultRefreshPlayersCounter = 2;//10; // in multiples of DefaultTickCounter
        const int DefaultIntuderNotificationCounter = 2;// 15; // in multiples of DefaultRefreshPlayersCounter

        HashSet<string> fonts = new HashSet<string>() {"Debug","Red","Green","Blue", "White","DarkBlue","UrlNormal","UrlHighlight","ErrorMessageBoxCaption","ErrorMessageBoxText",
            "InfoMessageBoxCaption","InfoMessageBoxText","ScreenCaption","GameCredits","LoadingScreen","BuildInfo","BuildInfoHighlight"};


        public static Notification Instance; // the only way to access session comp from other classes and the only accepted static field.
        private int tickCounter = 0;
        private int refreshPlayersCounter = 0;
        private int intruderNotificationCounter = 0;
        private NotificationConfig config;
        private List<IMyPlayer> players = new List<IMyPlayer>();
        private Vector3D playerPosition;
        private List<NotificationConfig.GPS> zonePositions;
        private Dictionary<long, string> playerInZone = new Dictionary<long, string>();

        public override void LoadData()
        {
            Instance = this;
        }

        public override void BeforeStart()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            Log.Msg("ZoneNotification Start");
            config = new NotificationConfig();
            config = config.LoadSettings();
            zonePositions = new List<GPS>();
            foreach (var gps in config.GPSlocations)
            {
                //using squared radius to optimise distance checks
                zonePositions.Add(new GPS(gps.UniqueName, gps.Position, gps.AlertRadius * gps.AlertRadius,
                    gps.AlertMessageEnter, CheckFontColour(gps.ColourEnter), gps.AlertMessageLeave,
                        CheckFontColour(gps.ColourLeave), gps.AlertTimeMs, gps.FactionTag, gps.NoIntruders));
                Log.Msg($"Adding Zone {gps.UniqueName} to Zone list");
            }

            Dictionary<string, Vector3D> planetPositions = GetPlanetPositions();
            Vector3D planetPosition;
            foreach (var planet in config.PlanetLocations)
            {
                if (planetPositions.TryGetValue(planet.PlanetName, out planetPosition))
                {
                    //using squared radius to optimise distance checks
                    zonePositions.Add(new GPS(planet.PlanetName, planetPosition, planet.AlertRadius * planet.AlertRadius,
                        planet.AlertMessageEnter, CheckFontColour(planet.ColourEnter), planet.AlertMessageLeave,
                            CheckFontColour(planet.ColourLeave), planet.AlertTimeMs, planet.FactionTag, planet.NoIntruders));
                    Log.Msg($"Adding Planet Zone {planet.PlanetName} to Zone list");
                }
            }
        }
        protected override void UnloadData()
        {
            Instance = null; // important for avoiding this object to remain allocated in memory
        }

        public override void UpdateAfterSimulation()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            if (++tickCounter < DefaultTickCounter) return;
            tickCounter = 0;
            //Log.Msg("Tick");
            //MyVisualScriptLogicProvider.ShowNotification("Tick", 1500, font: "Red", playerId: -1);

            if (++refreshPlayersCounter < DefaultRefreshPlayersCounter) return;
            refreshPlayersCounter = 0;
            RefreshPlayers();
            //Log.Msg("Tock");

            CheckPlayerPositions();
        }

        public string CheckFontColour(string font)
        {
            if (fonts.Contains(font))
                return font;

            Log.Msg($"Invalid colour in config: {font}");
            return "White";
        }

        Dictionary<string, Vector3D> GetPlanetPositions()
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

        private void CheckPlayerPositions()
        {
            //Log.Msg("CheckPlayerPositions");

            if (++intruderNotificationCounter > DefaultIntuderNotificationCounter)
                intruderNotificationCounter = 0;

            foreach (var player in players)
            {
                GPS closestZone = CheckPlayerPosition(player);
                if (closestZone.NoIntruders && intruderNotificationCounter == 0)
                    CheckIfIntruding(player, closestZone);
            }

        }

        private void CheckIfIntruding(IMyPlayer player, GPS closestZone)
        {
            if (player == null)//|| player.PromoteLevel != MyPromoteLevel.None)
            {
                Log.Msg("player=null or admin");
                return;
            }

            Log.Msg($"CheckIfIntruding zone {closestZone.UniqueName} {closestZone.FactionTag}");
            // $"GPS:{name}:{Position.X}:{Position.Y}:{Position.Z}:#FFFF0000:";
            if (closestZone.FactionTag == null || closestZone.FactionTag.Trim().Length == 0)
                return;

            Vector3D position = player.GetPosition();

            Log.Msg($"CheckIfIntruding {player.DisplayName} {closestZone.UniqueName}");
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
                    cockpit.RemovePilot();
                    cockpit.CubeGrid.Close();
                }
            }

            MyVisualScriptLogicProvider.ShowNotification(config.IntruderMessage, config.IntruderAlertTimeMs, config.IntruderColour, playerId: player.IdentityId);
            player.Character.GetInventory().Clear();

            MyVisualScriptLogicProvider.SetPlayersHydrogenLevel(player.IdentityId, 0);
            if (MyVisualScriptLogicProvider.GetPlayersEnergyLevel(player.IdentityId) > 0.01) MyVisualScriptLogicProvider.SetPlayersEnergyLevel(player.IdentityId, 0.01f);
            MyVisualScriptLogicProvider.SendChatMessage($"{config.IntruderChatMessagePt1} '{player.DisplayName}' {config.IntruderChatMessagePt2}", config.ChatSenderName, -1, config.IntruderColour);
        }

        /*        public static bool IsCreativeEnabled()
                {
                    if (MyAPIGateway.Session.CreativeMode) return true;
                    return MyAPIGateway.Session.EnableCopyPaste;
                }*/

        enum MessageType
        {
            None,
            Enter,
            Leave
        }

        private GPS CheckPlayerPosition(IMyPlayer player)
        {
            playerPosition = player.GetPosition();

            double distanceSqr;
            double lastDistance = double.MaxValue;
            GPS closestZone = new GPS();
            string playerZoneName;
            bool inZone = false;
            MessageType messageType = MessageType.None;
            Log.Msg($"Position {playerPosition} zonePositions.count={zonePositions.Count}");
            foreach (var zone in zonePositions)
            {
                if (zone.AlertRadius == 0)
                    continue;

                //Log.Msg($"Zone Position {zone.Position}");
                if (playerInZone.TryGetValue(player.IdentityId, out playerZoneName))
                    inZone = playerZoneName == zone.UniqueName;

                distanceSqr = Vector3D.DistanceSquared(zone.Position, playerPosition);
                Log.Msg($"Zone {zone.UniqueName} Position {zone.Position} distance={System.Math.Sqrt(distanceSqr)} inZone={inZone} noIntruding={zone.NoIntruders}");
                if (distanceSqr < zone.AlertRadius)
                {
                    if (inZone)
                    {
                        messageType = MessageType.None;
                        closestZone = zone;
                        break;
                    }
                    if (distanceSqr < lastDistance)
                    {
                        lastDistance = distanceSqr;
                        closestZone = zone;
                        messageType = MessageType.Enter;
                        if (playerInZone.ContainsKey(player.IdentityId))
                            playerInZone[player.IdentityId] = zone.UniqueName;
                        else
                            playerInZone.Add(player.IdentityId, zone.UniqueName);
                    }
                }
                else
                {
                    if (inZone)
                    {
                        closestZone = zone;
                        playerInZone.Remove(player.IdentityId);
                        messageType = MessageType.Leave;
                        break;
                    }
                }

            }

            Log.Msg($"MessageType={messageType} ClosestZone={closestZone.UniqueName} FactionTag={closestZone.FactionTag}");
            switch (messageType)
            {
                case MessageType.Enter:
                    {
                        MyVisualScriptLogicProvider.ShowNotification(closestZone.AlertMessageEnter, disappearTimeMs: closestZone.AlertTimeMs, font: closestZone.ColourEnter, playerId: player.IdentityId);
                        break;
                    }
                case MessageType.Leave:
                    {
                        MyVisualScriptLogicProvider.ShowNotification(closestZone.AlertMessageLeave, disappearTimeMs: closestZone.AlertTimeMs, font: closestZone.ColourLeave, playerId: player.IdentityId);
                        break;
                    }
                default:
                    break;
            }

            return closestZone;
        }
    }
}
