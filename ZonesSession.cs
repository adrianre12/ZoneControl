using ProtoBuf;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;
using static ZoneControl.ZoneControlBase;
using static ZoneControl.ZonesConfigBase;
using static ZoneControl.ZonesConfigBase.IntruderInfo;

namespace ZoneControl
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    internal class ZonesSession : MySessionComponentBase
    {
        const int DefaultPlayerRefreshPeriod = 600; // 10s
        const int DefaultPunishmentPeriod = 18000; // 5 mins
        const string VariableId = nameof(ZonesSession);

        public static ZonesSession Instance;

        private ZonesConfig config;
        private ZoneTable zoneTable;
        internal ZoneTable SubZoneTable;
        private List<IMyPlayer> players = new List<IMyPlayer>();
        private int nextPlayerRefreshFrame = 0;
        private int nextPlayerIndex = 0;
        private ZoneInfoInternal currentZone = null;
        private ZoneSpawner zoneSpawner = null;

        [ProtoContract]
        private class ZoneTargets
        {
            [ProtoMember(1)]
            public Dictionary<long, List<GPSposition>> Targets = new Dictionary<long, List<GPSposition>>();

            public ZoneTargets() { }
        }
        private ZoneTargets zoneTargets = new ZoneTargets();

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
            Log.Msg("LoadData...........");
            if (MyAPIGateway.Session.IsServer)
                LoadDataOnHost();
            else
                LoadDataOnClient();
        }

        protected override void UnloadData()
        {
            zoneSpawner.Close();
            Instance = null;
        }

        public void LoadDataOnHost()
        {
            Log.Msg("ZoneNotification Host Start");
            config = ZonesConfig.LoadConfig();
            zoneTable = ZoneTable.NewZoneDictionary(config);
            SubZoneTable = ZoneTable.NewSubZoneDictionary(config);
            foreach (var zone in SubZoneTable.Zones)
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

            zoneSpawner = new ZoneSpawner(config, SubZoneTable);
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

                //Look for subZones
                CheckPlayerPosition(SubZoneTable);

                //isIntruder not set, check position
                currentZone = CheckPlayerPosition(zoneTable);
                // check if intruding
                if (CheckIfIntruding(currentZone))
                {
                    ps.IsIntruder = true;

                    return;
                }

                NextPlayer();
                return;
            }

            if (currentFrame > nextPlayerRefreshFrame)
            {
                nextPlayerRefreshFrame = currentFrame + DefaultPlayerRefreshPeriod;
                RefreshPlayers();
                NextPlayer();
            }

            zoneSpawner.Update(currentFrame);
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

        public List<GPSposition> GetZoneTargets(long zoneId)
        {
            return zoneTargets.Targets.GetValueOrDefault(zoneId, new List<GPSposition>());
        }

        private ZoneInfoInternal CheckPlayerPosition(ZoneTable dict)
        {
            if (ps.Player == null)
                return null;

            //Log.Msg($"CheckPlayerPosition... {ps.Player.DisplayName} ------------------------");

            Vector3D playerPosition = ps.Player.GetPosition();

            ZoneInfoInternal foundZone;
            ZoneInfoInternal lastZone;
            bool moved = dict.GetZone(ps.Player.IdentityId, playerPosition, out foundZone, out lastZone);
            //Log.Msg($"moved={moved} foundZone={foundZone?.UniqueName} lastZone={lastZone?.UniqueName}");
            if (!moved)
            { //Has not moved
                //Log.Msg("Not moved");
                return foundZone; //can be null
            }

            if (lastZone != null && lastZone.AlertMessageLeave.Length > 0)
                MyVisualScriptLogicProvider.ShowNotification(lastZone.AlertMessageLeave,
                    disappearTimeMs: lastZone.AlertTimeMs, font: lastZone.ColourLeave, playerId: ps.Player.IdentityId);

            if (foundZone != null && foundZone.AlertMessageEnter.Length > 0)
                MyVisualScriptLogicProvider.ShowNotification(foundZone.AlertMessageEnter,
                    disappearTimeMs: foundZone.AlertTimeMs, font: foundZone.ColourEnter, playerId: ps.Player.IdentityId);

            return foundZone;
        }

        private bool CheckIfIntruding(ZoneInfoInternal zone)
        {
            if (zone == null || ps.Player == null)
            {
                return false;
            }

            string playerFactionTag = MyVisualScriptLogicProvider.GetPlayersFactionTag(ps.Player.IdentityId).Trim();


            //Log.Msg($"CheckIfIntruding {ps.Player.DisplayName} player factionTag={playerFactionTag} zone {zone.UniqueName} {zone.FactionTag}");

            if (!zone.NoIntruders || zone.FactionTag == null || zone.FactionTag.Length == 0)
                return false;

            if (playerFactionTag == zone.FactionTag.Trim())
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

        internal ZoneInfoInternal FindClosestWormholeCached(long gridId, Vector3D vector3D)
        {
            ZoneInfoInternal currentZone;
            ZoneInfoInternal lastZone;
            SubZoneTable.GetZone(gridId, vector3D, out currentZone, out lastZone);
            return currentZone;
        }
    }
}
