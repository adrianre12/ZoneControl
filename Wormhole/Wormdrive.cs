using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ObjectBuilders;
using VRageMath;
using static ZoneControl.ZonesConfig;

namespace ZoneControl.Wormhole
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_JumpDrive), false, new string[] { "LargeWormholeDrive" })]
    internal class WormDrive : WormDriveBase
    {
        const double MaxMovementSqrd = 1d;
        private Vector3D activationPosition;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            OverrideDefault = OverrideState.None;
            OverrideDefaultTimeout = 15;
            DefaultEnabledState = false;
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

            //Log.Msg($"Wormhole Enable changed chargerRegister.Count={chargerRegister.Count}");
            if (block.Enabled)
            {
                //check for wormhole zone
                ZoneInfo closetZone = ZonesSession.Instance.FindClosestZoneCached(gridId, block.CubeGrid.GetPosition());
                if (closetZone == null || !closetZone.Wormhole ||
                    (closetZone.FactionTag.Length > 0 && closetZone.FactionTag != block.GetOwnerFactionTag()))
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
            SetJumpdriveState(block.Enabled ? OverrideState.Disabled : OverrideState.None);
        }

        private void SetChargerState(OverrideState overrideState)
        {
            IMyFunctionalBlock charger;
            if (chargerRegister.TryGetValue(gridId, out charger) && charger != null)
            {
                WormDriveCharger wc = charger.GameLogic?.GetAs<WormDriveCharger>();
                if (wc != null)
                {
                    wc.SetOverride(overrideState);
                    Log.Msg($"Set charger {charger.EntityId}");
                }
            }
        }

        private void SetJumpdriveState(OverrideState overrideState)
        {
            var subTypeId = block.SlimBlock.GetObjectBuilder().SubtypeId;
            foreach (var jd in block.CubeGrid.GetFatBlocks<IMyJumpDrive>())
            {
                var fb = jd as IMyFunctionalBlock;
                if (fb == null || subTypeId == jd.SlimBlock.GetObjectBuilder().SubtypeId)
                    continue;
                ZoneControlBase gl = fb.GameLogic?.GetAs<ZoneControlBase>();
                if (gl == null)
                    continue;

                gl.SetOverride(overrideState);
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