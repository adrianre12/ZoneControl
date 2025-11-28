using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ObjectBuilders;

namespace ZoneControl.Wormhole
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_JumpDrive), false, new string[] { "LargeWormholeDrive" })]
    internal class WormholeComp : FunctionalBlockBase
    {
        private IMyFunctionalBlock block;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            //if (!MyAPIGateway.Session.IsServer)
            //    return;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();

            TerminalControls.DoOnce(ModContext);
            SetOverride(OverrideState.Enabled);
        }
        public override void UpdateAfterSimulation100()
        {
            base.UpdateAfterSimulation100();
        }


    }
}