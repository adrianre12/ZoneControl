using ProtoBuf;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;
using ZoneControl.NetworkLayer;
using ZoneControl.Spawner;
using ZoneControl.Wormhole;
using static ZoneControl.Utils;
using static ZoneControl.ZoneControlBase;
using static ZoneControl.ZonesConfigBase;
using static ZoneControl.ZonesConfigBase.IntruderInfo;
using static ZoneControl.ZoneTable;

namespace ZoneControl
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    internal class ZonesSession : MySessionComponentBase
    {
        const int DefaultPlayerRefreshPeriod = 600; // 10s
        const int DefaultPunishmentPeriod = 18000; // 5 mins
        const string VariableId = nameof(ZonesSession);
        const int DefaultWarnMsgCounter = 5; //RefreshPeriods 60s
        const int DefaultUrgentMsgCounter = 1; //RefreshPeriods 20s

        public static ZonesSession Instance;

        private ZonesConfig config;
        private ZoneTable zoneTable;
        internal ZoneTable SubZoneTable;
        private List<IMyPlayer> players = new List<IMyPlayer>();
        private int nextPlayerRefreshFrame = 0;
        private int nextPlayerIndex = 0;
        private int warnMsgCounter = DefaultWarnMsgCounter;
        private int urgentMsgCounter = DefaultUrgentMsgCounter;
        private HashSet<long> punishAdminSet = new HashSet<long>();


        internal struct CmdMsg
        {
            public IMyPlayer Player;
            public CmdMsgPacket Packet;
        }

        private Queue<CmdMsg> cmdQueue = new Queue<CmdMsg>();

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
            Instance = null;
        }

        public void LoadDataOnHost()
        {
            Log.Msg("Host Start");
            config = ZonesConfig.LoadConfig();
            Log.Debug = config.Debug?.ToLower() == "true";
        }

        public void LoadDataOnClient()
        {
            Log.Msg("Client Start");

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

        public override void BeforeStart()
        {
            //Log.Msg("BeforeStart");
            base.BeforeStart();
            if (MyAPIGateway.Session.IsServer)
            {
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
            }
        }


        internal void CmdQueueEnqueue(CmdMsg cmdMsg)
        {
            cmdQueue.Enqueue(cmdMsg);
        }

        private void CommandHandler(CmdMsg cmdMsg)
        {
            long playerId = cmdMsg.Player?.IdentityId ?? 0;
            List<string> args = cmdMsg.Packet.Args;
            if (args.Count < 2)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Help:");

                sb.AppendLine("/ZoneControl GPS FactionTag");
                sb.AppendLine("   Adds a faction's Wormhole and zone positions to the GPS list. FactionTag is the 3-letter faction tag.");

                if (playerId == 0 || cmdMsg.Player.PromoteLevel >= MyPromoteLevel.Admin)
                {
                    sb.AppendLine("/ZoneControl Status");
                    sb.AppendLine("   Lists the current Zones.");

                    sb.AppendLine("/ZoneControl PunishMe");
                    sb.AppendLine("   Toggles if Admins get punished when intruding");

                    sb.AppendLine("/ZoneControl EnableShip");
                    sb.AppendLine("   Removes the Disable punishment from a ship. You must be in a cockpit.");

                    sb.AppendLine("/ZoneControl EnableWD");
                    sb.AppendLine("   Enables Wormdrive for one jump outside a wormhole. Use a Jumpdrive for the jump.");

                    sb.AppendLine("/ZoneSpawner");
                    sb.AppendLine("   Commands for the Spawner");
                }

                Log.Msg(sb.ToString(), playerId);
                return;
            }
            string playerName = cmdMsg.Player?.DisplayName ?? "Local";
            Log.Msg($"Player {playerName} ran command {cmdMsg.Packet.Msg}");


            if (playerId == 0 || cmdMsg.Player.PromoteLevel >= MyPromoteLevel.Admin)
            {
                switch (args[1])
                {
                    case "Debug":
                        {
                            Log.Debug = !Log.Debug;
                            Log.Msg($"Log Debug={Log.Debug}", playerId);
                            return;
                        }

                    case "Status":
                        {
                            var sb = new StringBuilder();
                            sb.AppendLine("Status:");
                            sb.AppendLine("Zones:");
                            int i = 1;
                            foreach (var zone in zoneTable.Zones)
                            {
                                if (sb.Length == 0)
                                    sb.AppendLine();
                                sb.AppendLine($"{zone.Type} {zone.UniqueName}");
                                ++i;
                                if (i == 10)
                                {
                                    Log.Msg(sb.ToString(), playerId);
                                    sb.Clear();
                                    i = 0;
                                }
                            }
                            foreach (var zone in SubZoneTable.Zones)
                            {
                                if (sb.Length == 0)
                                    sb.AppendLine();
                                sb.AppendLine($"{zone.Type} {zone.UniqueName}");
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
                            return;
                        }

                    case "PunishMe":
                        {
                            long id = playerId == 0 ? MyAPIGateway.Session.Player.IdentityId : playerId;

                            if (punishAdminSet.Contains(id))
                            {
                                punishAdminSet.Remove(id);
                                Log.Msg($"Mistress Everdawn is disapointed but still removes {playerName} from her whipping list.", playerId);
                            }
                            else
                            {
                                punishAdminSet.Add(id);
                                Log.Msg($"Mistress Everdawn happily adds {playerName} to her whipping list.", playerId);
                            }
                            return;
                        }

                    case "EnableShip":
                        {
                            var player = cmdMsg.Player ?? MyAPIGateway.Session.Player;
                            if (Log.Debug) Log.Msg($"Enable for player '{player.DisplayName}'");
                            var cockpit = player.Character?.UsingEntity as IMyCockpit;
                            if (cockpit == null)
                            {
                                Log.Msg("You must be in a cockpit", playerId);
                                return;
                            }

                            var grid = cockpit.CubeGrid;
                            if (grid == null)
                            {
                                Log.Msg("Error Punish cubegrid null", playerId);
                                return;
                            }

                            Log.Msg($"Enable grid '{player.DisplayName}' grid name '{grid.DisplayName}'");

                            int expiryFrame;
                            if (punishmentCache.TryGetValue(grid.EntityId, out expiryFrame))
                            {
                                punishmentCache.Remove(grid.EntityId);
                            }

                            foreach (var jd in grid.GetFatBlocks<IMyGyro>())
                            {
                                var fb = jd as IMyFunctionalBlock;
                                ZoneControlBase gl = fb.GameLogic?.GetAs<ZoneControlBase>();
                                if (gl == null)
                                    continue;

                                gl.SetOverride(OverrideState.None);
                            }

                            foreach (var jd in grid.GetFatBlocks<IMyJumpDrive>())
                            {
                                var fb = jd as IMyFunctionalBlock;
                                ZoneControlBase gl = fb.GameLogic?.GetAs<ZoneControlBase>();
                                if (gl == null)
                                    continue;
                                if (gl.IsNotWormholeDrive)
                                    gl.SetOverride(OverrideState.None);
                            }

                            Log.Msg($"Grid '{grid.DisplayName} punishment removed.", playerId);

                            return;
                        }

                    case "EnableWD":
                        {
                            var player = cmdMsg.Player ?? MyAPIGateway.Session.Player;
                            if (Log.Debug) Log.Msg($"EnableWD for player '{player.DisplayName}'");
                            var cockpit = player.Character?.UsingEntity as IMyCockpit;
                            if (cockpit == null)
                            {
                                Log.Msg("You must be in a cockpit", playerId);
                                return;
                            }

                            var grid = cockpit.CubeGrid;
                            if (grid == null)
                            {
                                Log.Msg("Error cubegrid null", playerId);
                                return;
                            }

                            Log.Msg($"Enable grid '{player.DisplayName}' grid name '{grid.DisplayName}'");


                            foreach (var jd in grid.GetFatBlocks<IMyJumpDrive>()) // have to do WD first as WD disables JD
                            {
                                var fb = jd as IMyFunctionalBlock;
                                WormDrive wdgl = fb.GameLogic?.GetAs<WormDrive>();
                                if (wdgl?.IsNotWormholeDrive == false)
                                {
                                    wdgl.SetAdminBypassChecks();
                                    jd.CurrentStoredPower = jd.MaxStoredPower;
                                    jd.Enabled = true;
                                }
                            }
                            foreach (var jd in grid.GetFatBlocks<IMyJumpDrive>())
                            {
                                var fb = jd as IMyFunctionalBlock;
                                ZoneControlBase jdgl = fb.GameLogic?.GetAs<ZoneControlBase>();
                                if (jdgl.IsNotWormholeDrive)
                                {
                                    jdgl.SetOverride(OverrideState.None);
                                    jd.CurrentStoredPower = jd.MaxStoredPower;
                                    jd.Enabled = true;
                                }
                            }

                            Log.Msg($"Grid '{grid.DisplayName} Wormdrive enabled.", playerId);

                            return;
                        }

                    default:
                        {
                            break;
                        }
                }
            }
            //Player commands
            switch (args[1])
            {
                case "GPS":
                    {
                        if (args.Count < 3 || (args[2].Length == 0 && args[2] != "Open"))
                        {
                            Log.Msg("The FactionTag must be given.", playerId);
                            return;
                        }
                        string factionTag = args[2] == "Open" ? "" : args[2];
                        foreach (var zone in zoneTable.Zones)
                        {
                            if (zone.Type == ZoneInfoInternal.ZoneType.Zone && zone.FactionTag != null && zone.FactionTag == factionTag)
                            {
                                Log.Msg($"Adding GPS for {zone.UniqueName}", playerId);

                                MyVisualScriptLogicProvider.AddGPS(zone.UniqueName, "Faction owned Zone", zone.Position, VRageMath.Color.White, 900, playerId);
                            }
                        }
                        foreach (var zone in SubZoneTable.Zones)
                        {
                            if (zone.Type == ZoneInfoInternal.ZoneType.Wormhole && zone.FactionTag != null && zone.FactionTag == factionTag)
                            {
                                Log.Msg($"Adding GPS for {zone.UniqueName}", playerId);

                                MyVisualScriptLogicProvider.AddGPS(zone.UniqueName, "Faction owned Wormhole", zone.Position, VRageMath.Color.White, 0, playerId);
                            }
                        }

                        return;
                    }
                default:
                    {
                        Log.Msg($"Error unknown command '{cmdMsg.Packet.Msg}'", playerId);
                        return;
                    }
            }
        }

        public override void UpdateAfterSimulation()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            if (cmdQueue.Count > 0)
            {
                CommandHandler(cmdQueue.Dequeue());
                return;
            }

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
                CallSpawnPirate(CheckPlayerPosition(SubZoneTable));

                //isIntruder not set, check if intruding
                if (CheckIfIntruding(CheckPlayerPosition(zoneTable)))
                {
                    ps.IsIntruder = true;

                    return;
                }

                NextPlayer();
                if (ps.Player == null)
                    SubZoneTable.EnableCacheNullZone();

                return;
            }

            if (currentFrame > nextPlayerRefreshFrame)
            {
                nextPlayerRefreshFrame = currentFrame + DefaultPlayerRefreshPeriod;
                if (warnMsgCounter-- < 1)
                    warnMsgCounter = DefaultWarnMsgCounter;
                if (urgentMsgCounter-- < 1)
                    urgentMsgCounter = DefaultUrgentMsgCounter;
                //Log.Msg($"warnMsgCounter={warnMsgCounter} urgentMsgCounter={urgentMsgCounter}");
                RefreshPlayers();
                NextPlayer();
            }

            SpawnerSession.Instance?.Update(currentFrame);
        }

        private void CallSpawnPirate(ZoneInfoInternal subZone)
        {
            if (subZone?.Type == ZoneInfoInternal.ZoneType.Anomaly)
            {
                SpawnerSession.Instance.SpawnPirate(subZone.Id);
            }
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

            Vector3D playerPosition = ps.Player.GetPosition();

            //if (Log.Debug) Log.Msg($"CheckPlayerPosition... {ps.Player.DisplayName}  position={playerPosition} ------------------------");

            ZoneInfoInternal foundZone;
            ZoneInfoInternal lastZone;
            MsgItem extraMsg = null;
            bool moved = dict.GetZone(ps.Player.IdentityId, playerPosition, out foundZone, out lastZone, out extraMsg);

            //if (Log.Debug) Log.Msg($"moved={moved} foundZone={foundZone?.UniqueName} lastZone={lastZone?.UniqueName} extraMsg='{extraMsg.Msg}'");

            if (foundZone != null && extraMsg.Msg != null)
            {
                if (extraMsg.Urgent && urgentMsgCounter == 0 || !extraMsg.Urgent && warnMsgCounter == 0)
                {
                    MyVisualScriptLogicProvider.ShowNotification(extraMsg.Msg,
                        disappearTimeMs: foundZone.AlertTimeMs, font: extraMsg.Colour, playerId: ps.Player.IdentityId);
                }
            }
            if (!moved)
            { //Has not moved
              //Log.Msg("Not changed zone");
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


            //if (Log.Debug) Log.Msg($"CheckIfIntruding {ps.Player.DisplayName} player factionTag={playerFactionTag} zone {zone.UniqueName} {zone.FactionTag}");

            if (!zone.NoIntruders || zone.FactionTag == null || zone.FactionTag.Length == 0 || zone.FactionTag == config.Intruder.AdminTestFactionTag)
                return false;

            if (playerFactionTag == zone.FactionTag.Trim())
                return false;

            Vector3D position = ps.Player.GetPosition();

            MyVisualScriptLogicProvider.ShowNotification(config.Intruder.Message, config.Intruder.AlertTimeMs, config.Intruder.Colour, playerId: ps.Player.IdentityId);

            if (ps.Player.PromoteLevel >= MyPromoteLevel.Admin && !punishAdminSet.Contains(ps.Player.IdentityId))
            {
                if (Log.Debug) Log.Msg($"Admin {ps.Player.DisplayName} escapes punishment {ps.Player.IdentityId}");
                return false; //admins dont get punished unless in punishAdminSet
            }
            Log.Msg($"Intruder: {VectorToGPS(ps.Player.DisplayName, position)}");
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
                                if (gl.IsNotWormholeDrive)
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
            MsgItem extraMsg = null;
            SubZoneTable.GetZone(gridId, vector3D, out currentZone, out lastZone, out extraMsg);
            return currentZone;
        }

        /*        public MyObjectBuilder_Datapad GetRandomDatapad()
                {
                    return zoneSpawner.GetRandomDatapad();
                }*/
    }
}
