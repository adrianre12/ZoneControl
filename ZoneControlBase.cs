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
            //if (!MyAPIGateway.Session.IsServer)
            //    return;

            block = Entity as IMyFunctionalBlock;
            //gridId = block.CubeGrid.EntityId;
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();

            if (!MyAPIGateway.Session.IsServer || block?.CubeGrid?.Physics == null)
                return;

            originalEnabledState = block.Enabled;
            SetDefaultOverride();
            if (CheckDuplicate())
            {
                block.Enabled = false;
                OverrideDefault = OverrideState.Disabled;
                overrideSetting = OverrideState.Disabled;
            }

            NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
            block.EnabledChanged += Block_EnabledChanged;
        }

        public override void UpdateAfterSimulation100()
        {
            //Log.Msg($"Tick {block.CustomName} {overrideCounter}");
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
            Log.Msg($"{block.CustomName} overrideState={state}");
            overrideSetting = state;
            SetOverrideCounter();
            if (overrideSetting == OverrideState.None)
                block.Enabled = originalEnabledState;

            Block_EnabledChanged(null);
        }

        internal abstract bool CheckDuplicate();


        /*        public override void OnAddedToScene()
        {
            base.OnAddedToScene();
            Log.Msg($"OnAddedToScene {block.CubeGrid.DisplayName}");
        }

        public override void OnRemovedFromScene()
        {
            Log.Msg($"OnRemovedFromScene {block.CubeGrid.DisplayName}");

        }

        public override void MarkForClose()
        {
            base.MarkForClose();
            Log.Msg($"MarkForClose {block.CubeGrid.DisplayName}");

        }*/

        public override void Close()
        {
            base.Close();
            if (!MyAPIGateway.Session.IsServer)
                return;

            block.EnabledChanged -= Block_EnabledChanged;
        }
    }
}
