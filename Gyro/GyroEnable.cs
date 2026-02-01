using Sandbox.Common.ObjectBuilders;
using VRage.Game.Components;
using VRage.ObjectBuilders;

namespace ZoneControl.Jumpdrive
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Gyro), false)]
    internal class GyroEnable : ZoneControlBase
    {

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
        }

    }
}
