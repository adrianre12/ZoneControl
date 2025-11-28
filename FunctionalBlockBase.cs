using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace ZoneControl
{
    internal class FunctionalBlockBase : MyGameLogicComponent
    {
        internal IMyFunctionalBlock block;

        public enum OverrideState
        {
            None,
            Enabled,
            Disabled
        }

        private OverrideState overrideSetting;
        private bool originalEnabledState;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            //if (!MyAPIGateway.Session.IsServer)
            //    return;

            block = Entity as IMyFunctionalBlock;

            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
            block.EnabledChanged += Block_EnabledChanged;
        }

        public override void UpdateAfterSimulation100()
        {
            //Log.Msg($"Tick {block.CubeGrid.DisplayName}");
        }

        private void Block_EnabledChanged(IMyTerminalBlock obj)
        {
            if (overrideSetting == OverrideState.None)
            {
                originalEnabledState = block.Enabled;
                return;
            }

            block.Enabled = overrideSetting == OverrideState.Enabled;
        }

        public void SetOverride(OverrideState state)
        {
            overrideSetting = state;
            if (overrideSetting == OverrideState.None)
                block.Enabled = originalEnabledState;
            Block_EnabledChanged(null);
        }

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
            block.EnabledChanged -= Block_EnabledChanged;
        }
    }
}
