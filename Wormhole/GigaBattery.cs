using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace ESCore
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_BatteryBlock), false, new[] { "LargeGigaBatteryBlock", "SmallGigaBatteryBlock", "SmallGigaSmallBatteryBlock" })]

    public class GigaBattery : MyGameLogicComponent
    {
        internal MyBatteryBlock block;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Log.Msg($"Init GigaBattery...");
            block = Entity as MyBatteryBlock;

            NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateAfterSimulation100()
        {
            block.CurrentStoredPower = block.MaxStoredPower;
        }
    }
}
