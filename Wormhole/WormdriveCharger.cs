using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ObjectBuilders;

namespace ZoneControl.Wormhole
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_BatteryBlock), false, new[] { "LargeWormChargerBlock" })]
    internal class WormdriveCharger : WormdriveBase
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
            //Log.Msg($"enabled={block.Enabled} functional={block.IsFunctional}");
            if (!block.Enabled || !block.IsFunctional)
                return;

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

        internal override bool CheckDuplicate()
        {
            IMyFunctionalBlock fblock;
            if (!chargerRegister.TryGetValue(gridId, out fblock))
            {
                chargerRegister[gridId] = block;
                return false;
            }
            if (fblock.EntityId == block.EntityId)
                return false;
            return true;
        }

        public override void Close()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;
            base.Close();

            var gridId = block.CubeGrid.EntityId;
            IMyFunctionalBlock fblock;
            if (chargerRegister.TryGetValue(gridId, out fblock))
                if (fblock.EntityId == block.EntityId)
                    chargerRegister.Remove(gridId);
        }

    }
}
