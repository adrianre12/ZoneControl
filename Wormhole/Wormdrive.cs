using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ObjectBuilders;
using VRageMath;
using static ZoneControl.ZonesConfig;

namespace ZoneControl.Wormhole
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_JumpDrive), false, new string[] { "LargeWormholeDrive" })]
    internal class Wormdrive : WormdriveBase
    {
        const double MaxMovementSqrd = 1d;
        private Vector3D activationPosition;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            OverrideDefault = OverrideState.None;
            OverrideDefaultTimeout = 2; //15 ;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();

            TerminalControls.DoOnce(ModContext);
        }
        public override void UpdateAfterSimulation100()
        {
            base.UpdateAfterSimulation100();
            if (block.Enabled)
            {
                double movedSqrd = Vector3D.DistanceSquared(activationPosition, block.CubeGrid.GetPosition());
                if (movedSqrd > MaxMovementSqrd)
                {
                    SetDefaultOverride();
                    return;
                }
            }
        }

        public override void Block_EnabledChanged(IMyTerminalBlock obj)
        {
            base.Block_EnabledChanged(obj);
            Log.Msg($"Wormhole Enable changed chargerRegister.Count={chargerRegister.Count}");
            if (block.Enabled)
            {
                //check for wormhole zone
                ZoneInfo closetZone = ZonesSession.Instance.FindClosestZoneCached(gridId, block.CubeGrid.GetPosition());
                if (closetZone == null || !closetZone.Wormhole)
                {
                    SetDefaultOverride();
                    return;
                }

                activationPosition = block.CubeGrid.GetPosition();
                SetOverrideCounter();
            }

            //look for charger and enable/disable
            SetChargerState(block.Enabled ? OverrideState.Enabled : OverrideState.Disabled);

            //look for jumpdrives enable/disable
            SetJumpdriveState(block.Enabled ? OverrideState.Enabled : OverrideState.Disabled);
        }

        private void SetChargerState(OverrideState overrideState)
        {
            IMyFunctionalBlock charger;
            if (chargerRegister.TryGetValue(gridId, out charger) && charger != null)
            {
                WormdriveCharger wc = charger.GameLogic?.GetAs<WormdriveCharger>();
                if (wc != null)
                {
                    wc.SetOverride(overrideState);
                    Log.Msg($"Set charger {charger.EntityId}");
                }
            }
        }

        private void SetJumpdriveState(OverrideState overrideState)
        {
            //Log.Msg($"My subtype {block.GetObjectBuilder().SubtypeId}");
            foreach (var jd in block.CubeGrid.GetFatBlocks<IMyJumpDrive>())
            {
                Log.Msg($"Found {jd.CustomName}");// {jd.GetObjectBuilder().SubtypeId}");
                var fb = jd as IMyFunctionalBlock;
                if (fb == null || fb == block)
                    continue;

                Log.Msg($"selected {jd.CustomName}");
                ZoneControlBase gl = fb.GameLogic?.GetAs<ZoneControlBase>();
                if (gl == null)
                    continue;

                Log.Msg($"gamelogic {jd.CustomName}");
            }
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