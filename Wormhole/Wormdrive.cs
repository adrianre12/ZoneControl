using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ObjectBuilders;

namespace ZoneControl.Wormhole
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_JumpDrive), false, new string[] { "LargeWormholeDrive" })]
    internal class Wormdrive : WormdriveBase
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();

            TerminalControls.DoOnce(ModContext);
        }
        public override void UpdateAfterSimulation100()
        {
            base.UpdateAfterSimulation100();
        }

        public override void Block_EnabledChanged(IMyTerminalBlock obj)
        {
            base.Block_EnabledChanged(obj);
            Log.Msg($"Wormhole Enable changed chargerRegister.Count={chargerRegister.Count}");
            //check for wormhole zone
            //look for charger and enable/disable
            IMyFunctionalBlock charger;
            if (chargerRegister.TryGetValue(gridId, out charger) && charger != null)
            {
                Log.Msg($"Set charger {charger.EntityId}");
                WormdriveCharger wc = charger.GameLogic?.GetAs<WormdriveCharger>();
                if (wc != null)
                    wc.SetOverride(block.Enabled ? OverrideState.Enabled : OverrideState.Disabled);
            }
            //look for jumpdrives enable/disable

        }

        internal override bool CheckDuplicate()
        {
            Log.Msg("Check for duplicate");

            IMyFunctionalBlock fblock;
            if (!driveRegister.TryGetValue(gridId, out fblock))
            {
                driveRegister[gridId] = block;
                return false;
            }
            if (fblock.EntityId == block.EntityId)
                return false;
            return true;
        }

        public override void Close()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;
            base.Close();

            var gridId = block.CubeGrid.EntityId;
            IMyFunctionalBlock fblock;
            if (driveRegister.TryGetValue(gridId, out fblock))
                if (fblock.EntityId == block.EntityId)
                    driveRegister.Remove(gridId);
        }

    }
}