using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ObjectBuilders;

namespace ZoneControl.Wormhole
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_BatteryBlock), false, new[] { "LargeWormChargerBlock" })]
    internal class WormDriveCharger : WormDriveBase
    {
        const float MinCharge = 0.10f;
        const float MaxCharge = 0.99f;
        const float MinLoadPower = 5000f; //5 GW
        const float GW2MWH = 1 / 2160f; // * 100 / 60 /3600f

        private MyBatteryBlock chargerBlock;
        private float maxPower;
        private float minChangePerTick;
        private int lowPowerCounter;


        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            Log.Msg($"Init WormdriveCharger...");
            chargerBlock = Entity as MyBatteryBlock;
            OverrideDefault = OverrideState.Disabled;
            OverrideDefaultTimeout = 5;
            DefaultEnabledState = false;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            maxPower = chargerBlock.MaxStoredPower * MaxCharge;
            minChangePerTick = MinLoadPower * GW2MWH;
            chargerBlock.CurrentStoredPower = maxPower;
        }

        public override void UpdateAfterSimulation100()
        {
            base.UpdateAfterSimulation100();
            //Log.Msg($"enabled={block.Enabled} functional={block.IsFunctional}");
            if (!block.Enabled || !block.IsFunctional)
                return;

            float storedPowerChange = maxPower - chargerBlock.CurrentStoredPower;
            //Log.Msg($"charge={chargerBlock.CurrentStoredPower} lowPowerCounter={lowPowerCounter}  minChangePerTick={minChangePerTick} change={storedPowerChange}");
            if (storedPowerChange < minChangePerTick)
            {
                if (++lowPowerCounter > 1)
                {
                    //Log.Msg("End charge");
                    SetOverride(OverrideDefault);
                    return;
                }
            }
            else
                lowPowerCounter = 0;

            chargerBlock.CurrentStoredPower = maxPower;
        }

        public override void Block_EnabledChanged(IMyTerminalBlock obj)
        {
            base.Block_EnabledChanged(obj);
            if (!block.Enabled)
            {
                lowPowerCounter = 0;
            }
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
