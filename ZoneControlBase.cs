using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace ZoneControl
{
    internal abstract class ZoneControlBase : MyGameLogicComponent
    {
        internal IMyFunctionalBlock block;
        internal long gridId;

        public enum OverrideState
        {
            None,
            Enabled,
            Disabled
        }
        private OverrideState overrideSetting;

        private bool originalEnabledState;
        private long overrideCounter = 0;
        internal OverrideState OverrideDefault = OverrideState.None;
        internal bool? DefaultEnabledState = null;
        internal long OverrideDefaultTimeout = 0;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            block = Entity as IMyFunctionalBlock;
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            if (block?.CubeGrid?.Physics == null)
                return;

            NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;

            if (!MyAPIGateway.Session.IsServer)
                return;

            originalEnabledState = block.Enabled;
            SetDefaultOverride();
            if (CheckDuplicate())
            {
                block.Enabled = false;
                OverrideDefault = OverrideState.Disabled;
                overrideSetting = OverrideState.Disabled;
            }

            block.EnabledChanged += Block_EnabledChanged;
        }

        public override void UpdateAfterSimulation100()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;
            if (overrideCounter > 0 && --overrideCounter <= 0)
                SetDefaultOverride();
        }

        public virtual void Block_EnabledChanged(IMyTerminalBlock obj)
        {
            //Log.Msg("Base Block_EnabledChanged");
            if (CheckDuplicate())
            {
                Log.Msg($"Duplicate block grid='{block.CubeGrid.CustomName}' block='{block.CustomName}'");
                block.Enabled = false;
                return;
            }
            if (overrideSetting == OverrideState.None)
            {
                originalEnabledState = block.Enabled;
                return;
            }

            block.Enabled = overrideSetting == OverrideState.Enabled;
        }

        public void SetDefaultOverride()
        {
            overrideSetting = OverrideDefault;
            if (overrideSetting == OverrideState.None)
            {
                if (DefaultEnabledState != null)
                    originalEnabledState = (bool)DefaultEnabledState;
                block.Enabled = originalEnabledState;
                //Log.Msg($"{block.CustomName} overrideSetting={overrideSetting} originalEnabledState={originalEnabledState} enabled={block.Enabled}");
                return;
            }

            //Log.Msg($"{block.CustomName} overrideSetting={overrideSetting} originalEnabledState={originalEnabledState} enabled={block.Enabled}");
            block.Enabled = overrideSetting == OverrideState.Enabled;

        }

        public void SetOverrideCounter()
        {
            overrideCounter = 36 * OverrideDefaultTimeout; //ticks
        }

        public void SetOverride(OverrideState state)
        {
            //Log.Msg($"{block.CustomName} overrideState={state}");
            overrideSetting = state;
            SetOverrideCounter();
            if (overrideSetting == OverrideState.None)
                block.Enabled = originalEnabledState;

            Block_EnabledChanged(null);
        }

        internal abstract bool CheckDuplicate();

        public override void Close()
        {
            base.Close();
            if (!MyAPIGateway.Session.IsServer)
                return;

            block.EnabledChanged -= Block_EnabledChanged;
        }
    }
}
