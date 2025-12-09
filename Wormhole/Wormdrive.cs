using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.ModAPI.Network;
using VRage.ObjectBuilders;
using VRage.Sync;
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

        internal MySync<long, SyncDirection.FromServer> WormholeZoneId;
        //internal MySync<Vector3D, SyncDirection.BothWays> JumpTarget;
        public int SelectedTargetListItem = -1;

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
            //JumpTarget.SetLocalValue(Vector3D.NegativeInfinity);

            if (!MyAPIGateway.Utilities.IsDedicated) //client only
            {
                TerminalControls.DoOnce(ModContext);
                WormholeZoneId.ValueChanged += TargetZoneId_ValueChanged;
            }
            if (!MyAPIGateway.Session.IsServer) // server only
                return;
            //JumpTarget.ValueChanged += JumpTarget_ValueChanged;
        }

        /*        private void JumpTarget_ValueChanged(MySync<Vector3D, SyncDirection.BothWays> obj) //only on server
                {
                    if (JumpTarget.Value == Vector3D.NegativeInfinity)
                    {
                        return;
                    }
                    Log.Msg($"Start Jump to {JumpTarget.Value}");


                    IMyGridJumpDriveSystem jumpSystem = block.CubeGrid.JumpSystem;

                    //jumpSystem.RequestJump(JumpTarget.Value, block.OwnerId, 10, block.EntityId);
                    //jumpSystem.PerformJump(JumpTarget.Value);
                    jumpSystem.Jump(JumpTarget.Value, block.OwnerId, 10);

                    //JumpSystem.Jump(block, JumpTarget.Value);
                    JumpTarget.Value = Vector3D.NegativeInfinity;
                }*/

        private void TargetZoneId_ValueChanged(MySync<long, SyncDirection.FromServer> obj) // only on client
        {
            Log.Msg($"TargetZoneId changed {WormholeZoneId.Value}");
            SelectedTargetListItem = 0;
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

            Log.Msg($"Wormhole Enable changed {block.Enabled}");
            if (block.Enabled)
            {
                //check for wormhole zone
                ZoneInfo closetZone = ZonesSession.Instance.FindClosestZoneCached(gridId, block.CubeGrid.GetPosition());
                if (closetZone == null || !closetZone.Wormhole ||
                    (closetZone.FactionTag.Length > 0 && closetZone.FactionTag != block.GetOwnerFactionTag()))
                { // its not a accessable wormhole
                    SetDefaultOverride();
                    return;
                }

                activationPosition = block.CubeGrid.GetPosition();
                SetOverrideCounter();
                WormholeZoneId.Value = closetZone.Id;
            }
            else
            {
                WormholeZoneId.Value = -1;
            }

            //look for jumpdrives enable/disable
            SetJumpdriveState(block.Enabled ? OverrideState.Disabled : OverrideState.None);

            //JumpTarget.Value = Vector3D.NegativeInfinity;
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
            IMyFunctionalBlock fblock;
            if (!driveRegister.TryGetValue(gridId, out fblock))
            {
                driveRegister[gridId] = block;
                return false;
            }
            if (fblock.EntityId == block.EntityId)
            {
                return false;
            }

            return true;
        }

        public override void Close()
        {
            if (!MyAPIGateway.Utilities.IsDedicated) //client only
            {
                WormholeZoneId.ValueChanged -= TargetZoneId_ValueChanged;
            }
            if (!MyAPIGateway.Session.IsServer) // server only
                return;

            base.Close();
            //JumpTarget.ValueChanged -= JumpTarget_ValueChanged;

            IMyFunctionalBlock fblock;
            if (driveRegister.TryGetValue(gridId, out fblock))
            {
                if (fblock.EntityId == block.EntityId)
                {
                    driveRegister.Remove(gridId);
                }
            }
        }

    }
}