using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using System.Collections.Generic;
using VRage.Game.ModAPI;

namespace ZoneControl.NotificationBlock
{
    public static class TerminalControls
    {
        const string IdPrefix = "ZoneControl_Notification_";
        static bool Done = false;

        public static void DoOnce(IMyModContext context)
        {
            if (Done)
                return;
            Done = true;

            EditControls();
        }

        static bool CustomVisibleCondition(IMyTerminalBlock b)
        {
            return b?.GameLogic?.GetAs<NotificationComponent>() != null;
        }

        static bool CustomHiddenCondition(IMyTerminalBlock b)
        {
            return b?.GameLogic?.GetAs<NotificationComponent>() == null;
        }




        static void EditControls()
        {
            List<IMyTerminalControl> controls;

            MyAPIGateway.TerminalControls.GetControls<IMyButtonPanel>(out controls);

            foreach (IMyTerminalControl c in controls)
            {
                // a quick way to dump all IDs to SE's log
                /*
                                string name = MyTexts.GetString((c as IMyTerminalControlTitleTooltip)?.Title.String ?? "N/A");
                                string valueType = (c as ITerminalProperty)?.TypeName ?? "N/A";
                                Log.Msg($"[DEV] terminal property: id='{c.Id}'; type='{c.GetType().Name}'; valueType='{valueType}'; displayName='{name}'");
                */
                switch (c.Id)
                {
                    case "AnyoneCanUse":
                    case "Open Toolbar":
                    case "ButtonText":
                    case "ButtonName":
                        {
                            //c.Enabled = TerminalChainedDelegate.Create(c.Enabled, CustomHiddenCondition); // grays out
                            c.Visible = TerminalChainedDelegate.Create(c.Visible, CustomHiddenCondition); // hides
                            break;
                        }
                    default:
                        break;
                }
            }
        }
    }
}
