using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace ZoneControl.Wormhole
{
    internal class JumpSystem
    {


        public static void Jump(IMyTerminalBlock block, Vector3D target)
        {
            Log.Msg($"Jump start {target}");
            if (MyAPIGateway.Session.IsServer != true)
                return;

            IMyGridJumpDriveSystem jumpSystem = block.CubeGrid?.JumpSystem;
            if (jumpSystem == null || jumpSystem.IsJumping)
                return;

            IMyJumpDrive jumpDrive = (IMyJumpDrive)block;
            if (jumpDrive.Status != Sandbox.ModAPI.Ingame.MyJumpDriveStatus.Ready)
                return;


        }

        private static bool IsJumpValid(IMyTerminalBlock block, ref Vector3D jumpTarget)
        {
            IMyCubeGrid grid = block.CubeGrid;
            IMyGridJumpDriveSystem jumpSystem = grid.JumpSystem;
            long userId = block.OwnerId;
            Vector3D gridPos = grid.WorldMatrix.Translation;

            // Check if grid is immovable
            if (!jumpSystem.IsJumpValid(userId))
                return false;

            // Check if the grid is leaving or entering gravity
            if (IsWithinGravity(jumpTarget) || IsWithinGravity(gridPos))
                return false;

            // Check if the jump is within the world size
            // if (!MyEntities.IsInsideWorld(jumpTarget))
            //    return false;

            // Check if a planet is in the way
            /*            IMyEntity intersection = GetIntersectionWithLine(gridPos, jumpTarget);
                        if (intersection != null)
                        {
                            if (intersection is MyPlanet)
                                return false;

                            Vector3D point = intersection.WorldMatrix.Translation;
                            Vector3D closestPointOnLine = MyUtils.GetClosestPointOnLine(ref gridPos, ref jumpTarget, ref point);
                            float halfExtents = intersection.PositionComp.LocalAABB.HalfExtents.Length();
                            jumpTarget = closestPointOnLine - Vector3D.Normalize(jumpTarget - gridPos) * halfExtents;
                        }*/

            // Check if there is an available place to jump to
            Vector3D? newTarget = jumpSystem.FindSuitableJumpLocation(jumpTarget);
            if (!newTarget.HasValue)
                return false;
            jumpTarget = newTarget.Value;

            // Check if grid is too close or too far
            double distance = (jumpTarget - gridPos).Length();
            if (distance < jumpSystem.GetMinJumpDistance(userId) || distance > jumpSystem.GetMaxJumpDistance(userId))
                return false;

            return true;
        }

        private static bool IsWithinGravity(Vector3D pos)
        {
            MyPlanet planet = MyGamePruningStructure.GetClosestPlanet(pos);
            return planet != null && IsWithinGravity(pos, planet);
        }

        private static bool IsWithinGravity(Vector3D pos, MyPlanet planet)
        {
            MyGravityProviderComponent gravity = planet.Components.Get<MyGravityProviderComponent>();
            return gravity != null && gravity.IsPositionInRange(pos);
        }

        /*        private static IMyEntity GetIntersectionWithLine(Vector3D start, Vector3D end)
                {
                    LineD line = new LineD(start, end);
                    VRage.Game.Models.MyIntersectionResultLineTriangleEx? intersectionWithLine = MyEntities.GetIntersectionWithLine(ref line, (MyEntity)block.CubeGrid, null, ignoreChildren: true, ignoreFloatingObjects: true, ignoreHandWeapons: true, ignoreObjectsWithoutPhysics: false, ignoreSubgridsOfIgnoredEntities: true);

                    if (!intersectionWithLine.HasValue)
                        return null;
                    return intersectionWithLine.Value.Entity;
                }
        */
    }
}
