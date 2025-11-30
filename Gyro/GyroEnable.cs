using Sandbox.Common.ObjectBuilders;
using VRage.Game.Components;
using VRage.ObjectBuilders;

namespace ZoneControl.Jumpdrive
{
    // probably should attach this to all JumpDrives and disable if it is a WormholeDrive
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Gyro), false, new string[] { "LargeBlockGyro", "SmallBlockGyro", "LargeBlockPrototechGyro", "SmallBlockPrototechGyro" })]
    internal class GyroEnable : ZoneControlBase
    {

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            OverrideDefaultTimeout = 20;
        }

        internal override bool CheckDuplicate() //not needed for jumpdrives
        {
            return false;
        }
    }
}
