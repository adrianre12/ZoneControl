using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace ZoneControl.NotificationBlock
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TextPanel), false, new[] { "NotificationBlock" })]
    internal class NotificationComponent : MyGameLogicComponent
    {
        private IMyFunctionalBlock block;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            block = Entity as IMyFunctionalBlock;

            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            block.Physics = null; //disable the mass

            NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
        }
        public override void UpdateAfterSimulation100()
        {
            Log.Msg($"Tick {block.CubeGrid.DisplayName}");
        }

        public override void OnAddedToScene()
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

        }

        internal void ButtonPressed(IMyEntity user, int button)
        {
            Log.Msg($"Button pressed: {button}");
        }
    }
}