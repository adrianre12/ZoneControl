using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System.Collections.Generic;
using VRage.Game.ModAPI;

namespace ZoneControl.Wormhole
{
    public static class TerminalControls
    {
        //const string IdPrefix = "ZoneControl_Wormhole_";
        static bool Done = false;

        public static void DoOnce(IMyModContext context)
        {
            if (Done)
                return;
            Done = true;

            EditControls();
            //CreateControls();
        }

        /*        static bool CustomVisibleCondition(IMyTerminalBlock b)
                {
                    return b?.GameLogic?.GetAs<WormholeComp>() != null;
                }*/

        static bool CustomHiddenCondition(IMyTerminalBlock b)
        {
            return b?.GameLogic?.GetAs<WormholeComp>() == null;
        }

        /*        static void CreateControls()
                {
                    {
                        var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyJumpDrive>(""); // separators don't store the id
                        c.SupportsMultipleBlocks = true;
                        c.Visible = CustomVisibleCondition;

                        MyAPIGateway.TerminalControls.AddControl<IMyJumpDrive>(c);
                    }

                }*/


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
                    case "ShowInInventory":
                    case "ShowInToolbarConfig":
                    case "Name":
                    case "ShowOnHUD":
                    case "CustomData":
                        {
                            break;
                        }
                    case "Jump":
                    case "Recharge":
                    case "SelectedTarget":
                    case "RemoveBtn":
                    case "SelectBtn":
                    case "GpsList":
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
    }
}
