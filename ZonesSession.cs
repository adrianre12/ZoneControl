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
        private ZoneInfoInternal currentZone = null;
        private ZoneSpawner zoneSpawner = null;


        internal enum CmdFlag
        {
            True,
            Pending,
            False
        }

        internal struct CmdMsg
        {
            public IMyPlayer Player;
            public string Msg;
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
            try
            {
                if (MyAPIGateway.Utilities.IsDedicated)
                    MyAPIGateway.Utilities.MessageRecieved -= Utilities_MessageRecieved;
                else
                    MyAPIGateway.Utilities.MessageEntered -= Utilities_MessageEntered;
                zoneSpawner?.Close();
                Instance = null;
            }
            catch (Exception e)
            {
                Log.Msg($"Error in UnloadData\n{e.ToString()}");
            }
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

                zoneSpawner = new ZoneSpawner(config);
                if (MyAPIGateway.Utilities.IsDedicated)
                    MyAPIGateway.Utilities.MessageRecieved += Utilities_MessageRecieved;
                else
                    MyAPIGateway.Utilities.MessageEntered += Utilities_MessageEntered;
            }
        }

        private void Utilities_MessageEntered(string msg, ref bool sendToOthers)
        {
            //Log.Msg($"Recieved local msg={msg}");
            Utilities_MessageRecieved(0, msg);
        }

        private void Utilities_MessageRecieved(ulong steamId, string msg)
        {
            //Log.Msg($"Recieved steamId={steamId} msg={msg}");

            bool control = msg.StartsWith("/ZoneControl");
            bool spawner = msg.StartsWith("/ZoneSpawner");
            if (!control && !spawner)
                return;

            IMyPlayer player = null;
            if (steamId != 0)
            {
                player = MyAPIGateway.Players.TryGetIdentityId(MyAPIGateway.Players.TryGetIdentityId(steamId));
                if (player == null) //belt and braces
                    return;


                if (player.PromoteLevel < MyPromoteLevel.Admin)
                {
                    Log.Msg($"Non Admin player {player.DisplayName} tried to run command {msg}", player.IdentityId);
                    return;
                }
            }

            if (control)
                cmdQueue.Enqueue(new CmdMsg() { Player = player, Msg = msg });
            if (spawner)
                zoneSpawner.Enqueue(new CmdMsg() { Player = player, Msg = msg });
        }

        private void CommandHandler(CmdMsg cmdMsg)
        {
            long playerId = cmdMsg.Player?.IdentityId ?? 0;
            var args = cmdMsg.Msg.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (args.Length < 2)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Help:");
                sb.AppendLine("/ZoneControl Status");
                sb.AppendLine("   Lists the current Zones.");

                sb.AppendLine("/ZoneSpawner");
                sb.AppendLine("   Commands for the Spawner");


                Log.Msg(sb.ToString(), playerId);
                return;
            }

            Log.Msg($"Player {cmdMsg.Player?.DisplayName ?? "Local"} ran command {cmdMsg.Msg}");
            switch (args[1])
            {
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
                        break;
                    }

                default:
                    {
                        Log.Msg($"Error unknown command '{cmdMsg.Msg}'", playerId);
                        break;
                    }
            }
        }

        public override void UpdateAfterSimulation()
        {
            if (MyAPIGateway.Session.IsServer)
                UpdateAfterSimulationHost();
            //if (!MyAPIGateway.Utilities.IsDedicated)
            //     UpdateAfterSimulationClient();
        }

        public void UpdateAfterSimulationHost()
        {
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

            zoneSpawner.Update(currentFrame);
        }

        /*        public void UpdateAfterSimulationClient()
                {
                    //Log.Msg($"Client {zoneTargets.Targets.Count}");
                }*/

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


            //Log.Msg($"CheckIfIntruding {ps.Player.DisplayName} player factionTag={playerFactionTag} zone {zone.UniqueName} {zone.FactionTag}");

            if (!zone.NoIntruders || zone.FactionTag == null || zone.FactionTag.Length == 0)
                return false;

            if (playerFactionTag == zone.FactionTag.Trim())
                return false;

            Vector3D position = ps.Player.GetPosition();

            MyVisualScriptLogicProvider.ShowNotification(config.Intruder.Message, config.Intruder.AlertTimeMs, config.Intruder.Colour, playerId: ps.Player.IdentityId);

            if (ps.Player.PromoteLevel != MyPromoteLevel.None && playerFactionTag != config.Intruder.AdminTestFactionTag.Trim())
                return false; //admins dont get punished unless in AdminTestFactionTag

            Log.Msg($"Intruder: {ZonesConfigBase.VectorToGPS(ps.Player.DisplayName, position)}");
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
