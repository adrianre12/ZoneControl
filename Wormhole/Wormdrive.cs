using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.ObjectBuilders;
using VRageMath;
using static ZoneControl.ZonesConfig;

namespace ZoneControl.Wormhole
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_JumpDrive), false, new string[] { "LargeWormholeDrive" })]
    internal class WormDrive : ZoneControlBase
    {
        const double MaxMovementSqrd = 1d;
        private Vector3D activationPosition;
        internal static Dictionary<long, IMyFunctionalBlock> driveRegister = new Dictionary<long, IMyFunctionalBlock>();


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

            Log.Msg($"Wormhole Enable changed");
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

            //look for jumpdrives enable/disable
            SetJumpdriveState(block.Enabled ? OverrideState.Disabled : OverrideState.None);
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
            //Log.Msg($"Check for duplicate count={driveRegister.Count}");

            IMyFunctionalBlock fblock;
            if (!driveRegister.TryGetValue(gridId, out fblock))
            {
                //Log.Msg("Dupe not in reg");
                driveRegister[gridId] = block;
                return false;
            }
            if (fblock.EntityId == block.EntityId)
            {
                //Log.Msg("Not a Dupe");
                return false;
            }
            //Log.Msg("Its a Dupe");

            return true;
        }

        public override void Close()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;
            base.Close();
            //Log.Msg($"Closing {block.DisplayName} driveRegister {driveRegister.Count}");

            IMyFunctionalBlock fblock;
            if (driveRegister.TryGetValue(gridId, out fblock))
            {
                //Log.Msg($"Found {fblock.DisplayName}");
                if (fblock.EntityId == block.EntityId)
                {

                    driveRegister.Remove(gridId);
                    //Log.Msg($"Removing {driveRegister.Count}");
                }
            }
        }

    }
}