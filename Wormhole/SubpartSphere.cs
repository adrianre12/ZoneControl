using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace ZoneControl.Wormhole
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_JumpDrive), false, new string[] { "LargeWormholeDrive" })]
    internal class SubpartSphere : MyGameLogicComponent
    {
        private const float MAX_DISTANCE_SQ = 20 * 20;
        internal const float MaxChargeTimeSeconds = 120; //2mins
        internal const float DefaultIncrementPerTick = 1 / (MaxChargeTimeSeconds * 60); //2mins @ 60 ticks/s
        private const float FadeChangePerTick = 0.005f;

        private static readonly Color EmissiveGreen = new Color(57, 255, 20);

        internal IMyFunctionalBlock block;
        internal IMyJumpDrive jumpDrive;
        private float oneOverMaxStoredPower;
        private float visibleLerpValue;
        private float lastVisibleLerpValue = float.MaxValue;
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (MyAPIGateway.Utilities.IsDedicated)
                return;

            block = (IMyFunctionalBlock)Entity;

            if (block.CubeGrid?.Physics == null)
                return;

            jumpDrive = (IMyJumpDrive)block;
            jumpDrive.Recharge = false;
            oneOverMaxStoredPower = 1 / jumpDrive.MaxStoredPower;

            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
        }


        public override void UpdateBeforeSimulation()
        {
            var camPos = MyAPIGateway.Session.Camera.WorldMatrix.Translation; // local machine camera position
            if (Vector3D.DistanceSquared(camPos, block.GetPosition()) > MAX_DISTANCE_SQ)
                return;

            if (!block.IsFunctional)
            {
                lastVisibleLerpValue = float.MaxValue;
                return;
            }

            var currentStoredPower = jumpDrive.CurrentStoredPower;
            if (block.Enabled) //charger
            {
                if (currentStoredPower >= jumpDrive.MaxStoredPower)
                {
                    currentStoredPower = jumpDrive.MaxStoredPower;
                }
                else
                {
                    currentStoredPower += jumpDrive.MaxStoredPower * DefaultIncrementPerTick;
                }
                jumpDrive.CurrentStoredPower = currentStoredPower;
            }
            SetEmissiveWhite(currentStoredPower * oneOverMaxStoredPower);
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


    }
}
