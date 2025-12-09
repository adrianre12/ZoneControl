using Sandbox.Game;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System.Collections.Generic;
using System.Text;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace ZoneControl.Wormhole
{
    public static class TerminalControls
    {
        const string IdPrefix = "ZoneControl_Wormhole_";
        static bool Done = false;

        public static void DoOnce(IMyModContext context)
        {
            if (Done)
                return;
            Done = true;

            EditControls();
            EditActions();
            CreateControls();
            CreateActions(context);
        }

        static bool CustomVisibleCondition(IMyTerminalBlock b)
        {
            return b?.GameLogic?.GetAs<WormDrive>() != null;
        }

        static bool CustomHiddenCondition(IMyTerminalBlock b)
        {
            return b?.GameLogic?.GetAs<WormDrive>() == null;
        }

        static bool CustomHiddenEnabledCondition(IMyTerminalBlock b)
        {
            //return (b?.GameLogic?.GetAs<WormDrive>() == null) && (b as IMyFunctionalBlock).Enabled == false;
            var wd = b?.GameLogic?.GetAs<WormDrive>();
            return (b as IMyFunctionalBlock).Enabled && wd != null && wd.SelectedTargetListItem > -1;
        }

        static void CreateControls()
        {
            {
                var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyJumpDrive>(""); // separators don't store the id
                c.SupportsMultipleBlocks = true;
                c.Visible = CustomVisibleCondition;

                MyAPIGateway.TerminalControls.AddControl<IMyJumpDrive>(c);
            }

            {
                var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyJumpDrive>(IdPrefix + "TargetListBox");
                c.Title = MyStringId.GetOrCompute("Locations");
                c.Tooltip = MyStringId.GetOrCompute("Select Wormhole target location");
                c.SupportsMultipleBlocks = true;
                c.Visible = CustomVisibleCondition;

                c.VisibleRowsCount = 5;
                c.Multiselect = false;
                c.ListContent = (b, content, preSelect) =>
                {
                    //Log.Msg("ListContent");
                    WormDrive wd = b?.GameLogic?.GetAs<WormDrive>();
                    if (wd == null)
                        return;
                    var targets = ZonesSession.Instance.GetZoneTargets(wd.WormholeZoneId);
                    //Log.Msg($"targets[{wd.WormholeZoneId}] {targets.Count}");

                    for (int i = 0; i < targets.Count; i++)
                    {

                        //Log.Msg($"list content add index = {i}, Value = {targets[i].Name}");
                        var item = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(targets[i].Name),
                                                                    tooltip: MyStringId.NullOrEmpty,
                                                                    userData: i);

                        content.Add(item);
                        //Log.Msg($"SelectedTargetListItem={wd.SelectedTargetListItem}");
                        if (i == wd.SelectedTargetListItem)
                            preSelect.Add(item);
                    }
                };
                c.ItemSelected = (b, selected) =>
                {
                    Log.Msg($"Selected {selected[0].Text}");
                    WormDrive wd = b?.GameLogic?.GetAs<WormDrive>();
                    if (wd == null)
                        return;
                    wd.SelectedTargetListItem = (int)selected[0].UserData;
                };

                MyAPIGateway.TerminalControls.AddControl<IMyJumpDrive>(c);
            }

            /*            {
                            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyJumpDrive>(IdPrefix + "JumpButton");
                            c.Title = MyStringId.GetOrCompute("Jump");
                            c.Tooltip = MyStringId.GetOrCompute("Start Wormhole Jump");
                            c.SupportsMultipleBlocks = true;
                            c.Visible = CustomVisibleCondition;
                            c.Enabled = CustomHiddenEnabledCondition;

                            c.Action = (b) =>
                            {
                                WormDrive wd = b?.GameLogic?.GetAs<WormDrive>();
                                if (wd == null)
                                    return;
                                var targets = ZonesSession.Instance.GetZoneTargets(wd.WormholeZoneId);
                                if (targets.Count == 0 || wd.SelectedTargetListItem >= targets.Count || wd.SelectedTargetListItem < 0)
                                    return;
                                var target = targets[wd.SelectedTargetListItem];
                                wd.JumpTarget.Value = target.Position;
                                Log.Msg($"BtnJump to '{target.Name}'");
                            };

                            MyAPIGateway.TerminalControls.AddControl<IMyJumpDrive>(c);
                        }*/
        }

        static void CreateActions(IMyModContext context)
        {
            {
                var a = MyAPIGateway.TerminalControls.CreateAction<IMyJumpDrive>(IdPrefix + "JumpAction");

                a.Name = new StringBuilder("Jump");

                a.ValidForGroups = false;
                a.Icon = @"Textures\GUI\Icons\Actions\MeteorSwitchOn.dds";

                a.Action = (b) =>
                {
                    WormDrive wd = b?.GameLogic?.GetAs<WormDrive>();
                    if (wd == null)
                        return;
                    var targets = ZonesSession.Instance.GetZoneTargets(wd.WormholeZoneId);
                    if (targets.Count == 0 || wd.SelectedTargetListItem >= targets.Count || wd.SelectedTargetListItem < 0)
                        return;
                    var target = targets[wd.SelectedTargetListItem];
                    IMyGridJumpDriveSystem jumpSystem = b.CubeGrid.JumpSystem;
                    if (!jumpSystem.IsJumpValid(b.OwnerId))
                    {
                        //Log.Msg("Jump not valid");
                        MyVisualScriptLogicProvider.ShowNotification("Jump not valid", 10000, "Red");
                        return;
                    }
                    double distance = (target.Position - b.CubeGrid.WorldMatrix.Translation).Length();
                    if (distance < jumpSystem.GetMinJumpDistance(b.OwnerId) || distance > jumpSystem.GetMaxJumpDistance(b.OwnerId))
                    {
                        //Log.Msg("Jump too short or long");
                        MyVisualScriptLogicProvider.ShowNotification("Jump distance too short or long", 10000, "Red");
                        return;
                    }

                    //Log.Msg($"ActionJump to '{target.Name}' position={target.Position}");
                    MyVisualScriptLogicProvider.ShowNotification($"Jumping to {target.Name}", 1500, "Green");
                    jumpSystem.RequestJump(target.Position, b.OwnerId, 10, b.EntityId);

                };

                a.Writer = (b, sb) => //for some reason this stops working after a jump
                {
                    /*var jd = b as IMyJumpDrive;
                    IMyGridJumpDriveSystem jumpSystem = b.CubeGrid.JumpSystem;

                    if (jumpSystem.IsJumping)
                    {
                        sb.AppendLine("Jumping");
                        sb.Append($"{(int)jumpSystem.GetRemainingJumpTime()}");
                    }
                    else
                    {
                        sb.AppendLine(jd.Enabled ? "On" : "Off");
                        sb.Append($"{(int)(100 * jd.CurrentStoredPower / jd.MaxStoredPower)}%");
                    }

                    Log.Msg($"Status = {jd.Status} '{sb.ToString()}'");*/

                };

                a.InvalidToolbarTypes = new List<MyToolbarType>()
                {
                    MyToolbarType.ButtonPanel,
                    MyToolbarType.Character,
                    MyToolbarType.Seat
                };

                a.Enabled = CustomVisibleCondition;

                MyAPIGateway.TerminalControls.AddAction<IMyJumpDrive>(a);
            }

            {
                var a = MyAPIGateway.TerminalControls.CreateAction<IMyJumpDrive>(IdPrefix + "AbortAction");

                a.Name = new StringBuilder("Abort");

                a.ValidForGroups = false;
                a.Icon = @"Textures\GUI\Icons\Actions\MeteorSwitchOff.dds";

                a.Action = (b) =>
                {
                    Log.Msg("ActionAbort ");
                    b.CubeGrid.JumpSystem.AbortJump(6);
                };

                a.Writer = (b, sb) => //for some reason this stops working after a jump
                {
                };

                a.InvalidToolbarTypes = new List<MyToolbarType>()
                {
                    MyToolbarType.ButtonPanel,
                    MyToolbarType.Character,
                    MyToolbarType.Seat
                };

                a.Enabled = CustomVisibleCondition;

                MyAPIGateway.TerminalControls.AddAction<IMyJumpDrive>(a);
            }
        }


        static void EditControls()
        {
            List<IMyTerminalControl> controls;

            MyAPIGateway.TerminalControls.GetControls<IMyJumpDrive>(out controls);

            foreach (IMyTerminalControl c in controls)
            {
                // a quick way to dump all IDs to SE's log
                /*string name = MyTexts.GetString((c as IMyTerminalControlTitleTooltip)?.Title.String ?? "N/A");
                string valueType = (c as ITerminalProperty)?.TypeName ?? "N/A";
                Log.Msg($"[DEV] terminal property: id='{c.Id}'; type='{c.GetType().Name}'; valueType='{valueType}'; displayName='{name}'");*/

                switch (c.Id)
                {
                    case "OnOff":
                    case "ShowInTerminal":
                    case "ShowInToolbarConfig":
                    case "Name":
                    case "ShowOnHUD":
                    case "Recharge":
                        {
                            break;
                        }
                    default:
                        {
                            //c.Enabled = TerminalChainedDelegate.Create(c.Enabled, CustomHiddenCondition); // grays out
                            c.Visible = TerminalChainedDelegate.Create(c.Visible, CustomHiddenCondition); // hides
                            break;
                        }
                }
            }
        }

        static void EditActions()
        {
            List<IMyTerminalAction> actions;
            MyAPIGateway.TerminalControls.GetActions<IMyJumpDrive>(out actions);

            foreach (IMyTerminalAction a in actions)
            {
                // a quick way to dump all IDs to SE's log 
                //MyLog.Default.WriteLine($"[DEV] toolbar action: id='{a.Id}'; displayName='{a.Name}'");

                switch (a.Id)
                {
                    case "OnOff":
                    case "OnOff_On":
                    case "OnOff_Off":
                    case "ShowOnHUD":
                    case "ShowOnHUD_On":
                    case "ShowOnHUD_Off":
                        {

                            break;
                        }

                    default:
                        {
                            a.Enabled = TerminalChainedDelegate.Create(a.Enabled, CustomHiddenCondition);
                            break;
                        }
                }
            }
        }
    }
}
