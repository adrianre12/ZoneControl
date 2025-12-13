using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace ZoneControl.Wormhole
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_JumpDrive), false, new string[] { "LargeWormholeDrive" })]

    internal class SubpartShieldSpinner : MyGameLogicComponent
    {
        private const string SUBPART_NAME = "shield"; // dummy name without the "subpart_" prefix
        private const float DEGREES_PER_TICK1 = -0.5f; // rotation per tick in degrees (60 ticks per second)
        private const float DEGREES_PER_TICK2 = 0.2f; // rotation per tick in degrees (60 ticks per second)

        //        private const float ACCELERATE_PERCENT_PER_TICK = 0.05f; // aceleration percent of "DEGREES_PER_TICK" per tick.
        //        private const float DEACCELERATE_PERCENT_PER_TICK = 0.01f; // deaccleration percent of "DEGREES_PER_TICK" per tick.
        private readonly Vector3 ROTATION_AXIS1 = Vector3.Up; // rotation axis for the subpart, you can do new Vector3(0.0f, 0.0f, 0.0f) for custom values
        private readonly Vector3 ROTATION_AXIS2 = Vector3.Forward; // rotation axis for the subpart, you can do new Vector3(0.0f, 0.0f, 0.0f) for custom values
        private const float MAX_DISTANCE_SQ = 20 * 20; // player camera must be under this distance (squared) to see the subpart spinning

        internal IMyFunctionalBlock block;
        private bool subpartFirstFind = true;
        private Matrix subpartLocalMatrix; // keeping the matrix here because subparts are being re-created on paint, resetting their orientations
                                           //       private float targetSpeedMultiplier; // used for smooth transition

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
            Log.Msg("spiner update");
            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateBeforeSimulation()
        {
            //Log.Msg("spiner update");
            try
            {
                //bool shouldSpin = block.IsFunctional; // block.IsWorking; // if block is functional and enabled and powered.

                if (!block.IsFunctional)
                    return;
                /*               if (!shouldSpin && Math.Abs(targetSpeedMultiplier) < 0.00001f)
                                   return;

                               if (shouldSpin && targetSpeedMultiplier < 1)
                               {
                                   targetSpeedMultiplier = Math.Min(targetSpeedMultiplier + ACCELERATE_PERCENT_PER_TICK, 1);
                               }
                               else if (!shouldSpin && targetSpeedMultiplier > 0)
                               {
                                   targetSpeedMultiplier = Math.Max(targetSpeedMultiplier - DEACCELERATE_PERCENT_PER_TICK, 0);
                               }*/

                var camPos = MyAPIGateway.Session.Camera.WorldMatrix.Translation; // local machine camera position

                if (Vector3D.DistanceSquared(camPos, block.GetPosition()) > MAX_DISTANCE_SQ)
                    return;

                MyEntitySubpart subpart;
                if (Entity.TryGetSubpart(SUBPART_NAME, out subpart)) // subpart does not exist when block is in build stage
                {
                    if (subpartFirstFind) // first time the subpart was found
                    {
                        subpartFirstFind = false;
                        subpartLocalMatrix = subpart.PositionComp.LocalMatrixRef;
                    }

                    /*                    if (targetSpeedMultiplier > 0)
                                        {*/
                    //subpartLocalMatrix = Matrix.CreateFromAxisAngle(ROTATION_AXIS, MathHelper.ToRadians(targetSpeedMultiplier * DEGREES_PER_TICK)) * subpartLocalMatrix;

                    subpartLocalMatrix = Matrix.CreateFromAxisAngle(ROTATION_AXIS1, MathHelper.ToRadians(DEGREES_PER_TICK1)) * subpartLocalMatrix;

                    subpartLocalMatrix = Matrix.CreateFromAxisAngle(ROTATION_AXIS2, MathHelper.ToRadians(DEGREES_PER_TICK2)) * subpartLocalMatrix;


                    subpartLocalMatrix = Matrix.Normalize(subpartLocalMatrix); // normalize to avoid any rotation inaccuracies over time resulting in weird scaling
                                                                               // }

                    subpart.PositionComp.SetLocalMatrix(ref subpartLocalMatrix);
                }
            }
            catch (Exception e)
            {
                Log.Msg(e.ToString());
            }
        }

    }
}
