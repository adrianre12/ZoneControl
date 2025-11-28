using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using VRage.Game.Components;
using VRage.ObjectBuilders;

namespace ZoneControl.Wormhole
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_BatteryBlock), false, new[] { "LargeWormChargerBlock" })]
    internal class WormdriveCharger : FunctionalBlockBase
    {
        const float MinCharge = 0.10f;
        const float MaxCharge = 0.99f;
        const float MinLoadPower = 5000f; //5 GW
        const float GW2MWH = 1 / 2160f; // * 100 / 60 /3600f

        private MyBatteryBlock chargerBlock;
        private float minPower;
        private float maxPower;
        private float minChangePerTick;
        private float lastStoredPower;


        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            Log.Msg($"Init WormdriveCharger...");
            chargerBlock = Entity as MyBatteryBlock;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            minPower = chargerBlock.MaxStoredPower * MinCharge;
            maxPower = chargerBlock.MaxStoredPower * MaxCharge;
            minChangePerTick = -MinLoadPower * GW2MWH;
            chargerBlock.CurrentStoredPower = maxPower;
            lastStoredPower = 0;
        }

        public override void UpdateAfterSimulation100()
        {
            base.UpdateAfterSimulation100();
            float storedPowerChange = chargerBlock.CurrentStoredPower - lastStoredPower;
            //Log.Msg($"charge={chargerBlock.CurrentStoredPower} min={minPower}  minChangePerTick={minChangePerTick} change={storedPowerChange}");
            if (storedPowerChange <= 0 && storedPowerChange > minChangePerTick)
            {
                SetOverride(OverrideState.Disabled);
                return;
            }
            lastStoredPower = chargerBlock.CurrentStoredPower;
            if (chargerBlock.CurrentStoredPower > minPower)
                return;

            chargerBlock.CurrentStoredPower = maxPower;
        }
    }
}
