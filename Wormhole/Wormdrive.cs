using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Network;
using VRage.ObjectBuilders;
using VRage.Sync;
using VRage.Utils;
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
        internal MySync<Vector3D, SyncDirection.BothWays> JumpTarget; //PositiveInfinity do nothing, position jump, negative infinity abort
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
            JumpTarget.SetLocalValue(Vector3D.PositiveInfinity);

            if (!MyAPIGateway.Utilities.IsDedicated) //client only
            {
                TerminalControls.DoOnce(ModContext);
                WormholeZoneId.ValueChanged += TargetZoneId_ValueChanged;
                block.AppendingCustomInfo += AppendingCustomInfo;
            }

            if (!MyAPIGateway.Session.IsServer) // server only
                return;
            JumpTarget.ValueChanged += JumpTarget_ValueChanged;
        }

        private void JumpTarget_ValueChanged(MySync<Vector3D, SyncDirection.BothWays> obj) //only on server
        {
            if (JumpTarget.Value == Vector3D.PositiveInfinity)
            {
                return;
            }

            IMyGridJumpDriveSystem jumpSystem = block.CubeGrid.JumpSystem;

            if (JumpTarget.Value == Vector3D.NegativeInfinity)
            {
                //Log.Msg("Abort jump");
                jumpSystem.AbortJump(6);
            }
            else
            {
                //Log.Msg($"Start Jump to {JumpTarget.Value}");
                jumpSystem.RequestJump(JumpTarget.Value, block.OwnerId, 10, block.EntityId);
                //jumpSystem.PerformJump(JumpTarget.Value);
                //jumpSystem.Jump(JumpTarget.Value, block.OwnerId, 10);
            }
            JumpTarget.Value = Vector3D.PositiveInfinity;
        }

        private void TargetZoneId_ValueChanged(MySync<long, SyncDirection.FromServer> obj) // only on client
        {
            Log.Msg($"TargetZoneId changed {WormholeZoneId.Value}");
            SelectedTargetListItem = -1;
        }

        public override void UpdateAfterSimulation100()
        {
            base.UpdateAfterSimulation100();

            if (!MyAPIGateway.Utilities.IsDedicated) //client only
            {
                try
                {
                    if (MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
                    {
                        block.RefreshCustomInfo();
                        block.SetDetailedInfoDirty();
                    }
                }
                catch (Exception e)
                {
                    Log.Msg(e.ToString());
                }

            }

            if (!MyAPIGateway.Session.IsServer)
                return;

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

            JumpTarget.Value = Vector3D.PositiveInfinity;
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

        private void AppendingCustomInfo(IMyTerminalBlock block, StringBuilder sb)
        {
            var jumpDrive = (IMyJumpDrive)block;

            sb.Append("\nWormhole charged In: ");
            MyValueFormatter.AppendTimeInBestUnit((1 - jumpDrive.CurrentStoredPower / jumpDrive.MaxStoredPower) * SubpartSphereCharger.MaxChargeTimeSeconds, sb);
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
            JumpTarget.ValueChanged -= JumpTarget_ValueChanged;

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