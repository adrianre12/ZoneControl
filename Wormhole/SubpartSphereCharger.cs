using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System;
using VRage.Game.Components;
using VRage.Game.ModAPI.Network;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Sync;
using VRageMath;

namespace ZoneControl.Wormhole
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_JumpDrive), false, new string[] { "LargeWormholeDrive" })]
    internal class SubpartSphereCharger : MyGameLogicComponent
    {
        private const float MAX_DISTANCE_SQ = 20 * 20;
        internal const float MaxChargeTimeSeconds = 120; //2mins
        internal const float DefaultIncrementPerTick = 1 / (MaxChargeTimeSeconds * 60); //2mins @ 60 ticks/s
        private const float FadeChangePerTick = 0.0025f;

        private static readonly Color EmissiveGreen = new Color(57, 255, 20);

        internal IMyJumpDrive block;
        private float oneOverMaxStoredPower;
        private float visibleLerpValue;
        private float lastVisibleLerpValue = float.MaxValue;
        private float currentStoredPower;
        private MySync<float, SyncDirection.BothWays> newChargeValue;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            block = (IMyJumpDrive)Entity;

            if (block.CubeGrid?.Physics == null)
                return;

            block.Recharge = false;

            if (!MyAPIGateway.Utilities.IsDedicated) //only on client
            {
                oneOverMaxStoredPower = 1 / block.MaxStoredPower;

                NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;
                block.EnabledChanged += Block_EnabledChanged;
            }

            if (MyAPIGateway.Session.IsServer) // only on server
            {
                newChargeValue.ValueChanged += NewChargeValue_ValueChanged;
            }
        }

        private void NewChargeValue_ValueChanged(MySync<float, SyncDirection.BothWays> obj) //only on server
        {
            block.CurrentStoredPower = newChargeValue.Value;
        }

        private void Block_EnabledChanged(IMyTerminalBlock obj) //only on client
        {
            if (block.Enabled)
            {
                currentStoredPower = block.CurrentStoredPower;
            }
        }

        public override void UpdateBeforeSimulation() //only on client
        {
            var camPos = MyAPIGateway.Session.Camera.WorldMatrix.Translation; // local machine camera position
            if (Vector3D.DistanceSquared(camPos, block.GetPosition()) > MAX_DISTANCE_SQ)
                return;

            if (!block.IsFunctional)
            {
                lastVisibleLerpValue = float.MaxValue;
                return;
            }

            if (block.Enabled) //charger
            {
                if (currentStoredPower >= block.MaxStoredPower)
                {
                    currentStoredPower = block.MaxStoredPower;
                }
                else
                {
                    currentStoredPower += block.MaxStoredPower * DefaultIncrementPerTick;
                }
                block.CurrentStoredPower = currentStoredPower;
            }
            //Log.Msg($"currentStoredPower={currentStoredPower} CurrentStoredPower={block.CurrentStoredPower}");
            SetEmissiveWhite(currentStoredPower * oneOverMaxStoredPower);
        }

        public override void UpdateBeforeSimulation100() // only on client
        {
            base.UpdateBeforeSimulation100();
            if (block.Enabled)
                newChargeValue.Value = currentStoredPower;
        }

        private void SetEmissiveWhite(float lerpValue)
        {
            if (!block.Enabled)
            {
                lerpValue = 0;
            }
            if (lerpValue == lastVisibleLerpValue)
                return;

            if (lerpValue > visibleLerpValue)
            {
                visibleLerpValue += Math.Min(FadeChangePerTick, lerpValue - visibleLerpValue);
            }
            else if (lerpValue < visibleLerpValue)
            {
                visibleLerpValue -= Math.Min(FadeChangePerTick, visibleLerpValue - lerpValue);
            }
            else
            {
                visibleLerpValue = lerpValue;
            }
            //Log.Msg($"lerpValue={lerpValue} visibleLerpValue={visibleLerpValue} lastVisibleLerpValue={lastVisibleLerpValue}");

            lastVisibleLerpValue = visibleLerpValue;
            try
            {
                block.SetEmissivePartsForSubparts("EmissveWhite", Color.Lerp(Color.DimGray, EmissiveGreen, visibleLerpValue), visibleLerpValue);
            }
            catch (Exception e)
            {
                Log.Msg(e.ToString());
            }
        }

        public override void Close()
        {
            if (MyAPIGateway.Session.IsServer) // only on server
            {
                newChargeValue.ValueChanged -= NewChargeValue_ValueChanged;
            }
        }
    }
}
