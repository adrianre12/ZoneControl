using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace ZoneControl.Wormhole
{ // from https://github.com/THDigi/SE-ModScript-Examples/blob/master/Data/Scripts/Examples/Example_SpinningSubpart.cs
    // not using spinner component as it only does one fixed axis
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_JumpDrive), false, new string[] { "LargeWormholeDrive" })]

    internal class SubpartShieldSpinner : MyGameLogicComponent
    {
        private const string SUBPART_NAME = "shield";
        private const float DEGREES_PER_TICK1 = -0.5f;
        private const float DEGREES_PER_TICK2 = 0.2f;

        private readonly Vector3 ROTATION_AXIS1 = Vector3.Up;
        private readonly Vector3 ROTATION_AXIS2 = Vector3.Forward;
        private const float MAX_DISTANCE_SQ = 20 * 20;

        internal IMyFunctionalBlock block;
        private bool subpartFirstFind = true;
        private Matrix subpartLocalMatrix;


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
            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateBeforeSimulation()
        {
            if (!block.IsFunctional)
                return;

            var camPos = MyAPIGateway.Session.Camera.WorldMatrix.Translation; // local machine camera position

            if (Vector3D.DistanceSquared(camPos, block.GetPosition()) > MAX_DISTANCE_SQ)
                return;

            try
            {
                MyEntitySubpart subpart;
                if (Entity.TryGetSubpart(SUBPART_NAME, out subpart))
                {
                    if (subpartFirstFind)
                    {
                        subpartFirstFind = false;
                        subpartLocalMatrix = subpart.PositionComp.LocalMatrixRef;
                    }

                    subpartLocalMatrix = Matrix.CreateFromAxisAngle(ROTATION_AXIS1, MathHelper.ToRadians(DEGREES_PER_TICK1)) * subpartLocalMatrix;
                    subpartLocalMatrix = Matrix.CreateFromAxisAngle(ROTATION_AXIS2, MathHelper.ToRadians(DEGREES_PER_TICK2)) * subpartLocalMatrix;
                    subpartLocalMatrix = Matrix.Normalize(subpartLocalMatrix);

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
