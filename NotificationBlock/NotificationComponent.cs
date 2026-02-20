using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using ZoneControl.NetworkLayer;
using ZoneControl.Spawner;

namespace ZoneControl.NotificationBlock
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ButtonPanel), false, new[] { "NotificationBlock" })]

    internal class NotificationComponent : MyGameLogicComponent
    {
        const int DefaultRefreshPeriodTicks = 30 * 60;

        private IMyFunctionalBlock block;

        private int refreshAfterFrame;

        private SpawnSummary spawnSummary;
        private ScreenNotification screen0;
        private int spawnSummaryUpdateFrame;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            block = Entity as IMyFunctionalBlock;

            //if (Log.Debug) Log.Msg($"Init IsServer={MyAPIGateway.Session.IsServer} IsDedicated={MyAPIGateway.Utilities.IsDedicated}");

            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame() //only on server
        {
            if (!MyAPIGateway.Utilities.IsDedicated) //Client
            {
                TerminalControls.DoOnce(ModContext);
            }

            if (MyAPIGateway.Session.IsServer)
            {
                refreshAfterFrame = MyAPIGateway.Session.GameplayFrameCounter + DefaultRefreshPeriodTicks;
                screen0 = new ScreenNotification((IMyTextSurfaceProvider)block, 0);

                NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
            }
            spawnSummary = SpawnerSession.Instance.GetSpawnSummary();
        }

        public override void UpdateAfterSimulation100() //only on server
        {
            var currentFrame = MyAPIGateway.Session.GameplayFrameCounter;
            spawnSummary = SpawnerSession.Instance.GetSpawnSummary();

            if (currentFrame < refreshAfterFrame && spawnSummary.UpdatedAtFrame == spawnSummaryUpdateFrame)
                return;
            refreshAfterFrame = currentFrame + DefaultRefreshPeriodTicks;
            spawnSummaryUpdateFrame = spawnSummary.UpdatedAtFrame;

            screen0.Refresh(spawnSummary.Selected);
        }

        /*        public override void Close()
                {
                    if (MyAPIGateway.Utilities.IsDedicated)
                        return;

                }*/


        internal void ButtonPressed(IMyEntity user, int button) //client
        {
            if (!block.Enabled)
                return;

            long identityId = MyAPIGateway.Session.Player.IdentityId;

            Log.Msg($"Button pressed:{button} id={identityId}");
            Networking.Instance.SendGPSbuttonPressed(button, identityId);
        }
    }
}