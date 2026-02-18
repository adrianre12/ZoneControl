using Sandbox.Common.ObjectBuilders;
using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using VRage.Game.Components;
using VRage.Game.ModAPI.Network;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Sync;
using VRageMath;
using ZoneControl.Spawner;

namespace ZoneControl.NotificationBlock
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ButtonPanel), false, new[] { "NotificationBlock" })]

    internal class NotificationComponent : MyGameLogicComponent
    {
        const int DefaultRefreshPeriodTicks = 30 * 60;
        const int GPSDisplayPeriod = 14400; // 4hrs

        private IMyFunctionalBlock block;

        private MySync<long, SyncDirection.BothWays> buton1UserId;
        private MySync<long, SyncDirection.BothWays> buton2UserId;
        private MySync<long, SyncDirection.BothWays> buton3UserId;

        private int refreshAfterFrame;

        private SpawnSummary summary = new SpawnSummary();
        private int summaryPtr = 0;
        private ScreenNotification screen0;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            block = Entity as IMyFunctionalBlock;

            buton1UserId.SetLocalValue(0);
            buton2UserId.SetLocalValue(0);
            buton3UserId.SetLocalValue(0);

            if (Log.Debug) Log.Msg($"Init IsServer={MyAPIGateway.Session.IsServer} IsDedicated={MyAPIGateway.Utilities.IsDedicated}");

            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame() //only on server
        {
            if (!MyAPIGateway.Utilities.IsDedicated) //Client
            {
                if (Log.Debug) Log.Msg($"Init Should be client");
                TerminalControls.DoOnce(ModContext);
            }

            if (MyAPIGateway.Session.IsServer)
            {
                if (Log.Debug) Log.Msg($"Init Should be server");

                buton1UserId.ValueChanged += Buton1UserId_ValueChanged;
                buton2UserId.ValueChanged += Buton2UserId_ValueChanged;
                buton3UserId.ValueChanged += Buton3UserId_ValueChanged;

                refreshAfterFrame = MyAPIGateway.Session.GameplayFrameCounter + DefaultRefreshPeriodTicks;
                screen0 = new ScreenNotification((IMyTextSurfaceProvider)block, 0);

                NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
            }
        }

        public override void UpdateAfterSimulation100() //only on server
        {
            var currentFrame = MyAPIGateway.Session.GameplayFrameCounter;

            if (currentFrame < refreshAfterFrame)
                return;
            refreshAfterFrame = currentFrame + DefaultRefreshPeriodTicks;

            UpdateSpawnSummary();
            screen0.Refresh(summary.Selected);
            buton1UserId.Value = 0; //hack to get them to reset
            buton2UserId.Value = 0;
            buton3UserId.Value = 0;
        }

        private void UpdateSpawnSummary()
        {
            SpawnSummary tmpSummary = SpawnerSession.Instance.GetSpawnSummary();
            if (summary != tmpSummary)
            {
                summary = tmpSummary;
                summaryPtr = 0;
            }

            summary.Selected.Clear();
            int numRows = Math.Min(3, summary.Items.Count);
            for (int i = 0; i < numRows; i++)
            {
                if (summaryPtr >= summary.Items.Count)
                    summaryPtr = 0;
                summary.Selected.Add(summary.Items[summaryPtr]);
                //Log.Msg($"Selected {summaryPtr} {summary.Items[summaryPtr].Name}");
                summaryPtr++;
            }
        }
        public override void Close()
        {
            if (MyAPIGateway.Utilities.IsDedicated)
                return;

            buton1UserId.ValueChanged -= Buton1UserId_ValueChanged;
            buton2UserId.ValueChanged -= Buton2UserId_ValueChanged;
            buton3UserId.ValueChanged -= Buton3UserId_ValueChanged;
        }

        private void Buton1UserId_ValueChanged(MySync<long, SyncDirection.BothWays> ong)
        {
            if (buton1UserId.Value == 0)
                return;
            if (summary.Selected.Count > 0)
            {
                var sel = summary.Selected[0];
                if (Log.Debug) Log.Msg($"Button1 pressed by {buton1UserId.Value} {sel.Name}");
                MyVisualScriptLogicProvider.AddGPS(sel.Name, "Aproximate position of detected navigation hazzard.", sel.SubZonePosition, VRageMath.Color.White, GPSDisplayPeriod, buton1UserId.Value);
                MyVisualScriptLogicProvider.SendChatMessageColored($"Added GPS for {sel.Name}", Color.Yellow, "", buton1UserId.Value);
            }
            buton1UserId.Value = 0; //hack to get them to reset
            buton2UserId.Value = 0;
            buton3UserId.Value = 0;
        }

        private void Buton2UserId_ValueChanged(MySync<long, SyncDirection.BothWays> obj)
        {
            if (buton2UserId.Value == 0)
                return;
            if (summary.Selected.Count > 1)
            {
                var sel = summary.Selected[1];
                if (Log.Debug) Log.Msg($"Button2 pressed by {buton2UserId.Value} {sel.Name}");
                MyVisualScriptLogicProvider.AddGPS(sel.Name, "Aproximate position of detected navigation hazzard.", sel.SubZonePosition, VRageMath.Color.White, GPSDisplayPeriod, buton2UserId.Value);
                MyVisualScriptLogicProvider.SendChatMessageColored($"Added GPS for {sel.Name}", Color.Yellow, "GPS", buton1UserId.Value);
            }
            buton1UserId.Value = 0; //hack to get them to reset
            buton2UserId.Value = 0;
            buton3UserId.Value = 0;
        }

        private void Buton3UserId_ValueChanged(MySync<long, SyncDirection.BothWays> onj)
        {
            if (buton3UserId.Value == 0)
                return;
            if (summary.Selected.Count > 2)
            {
                var sel = summary.Selected[2];
                if (Log.Debug) Log.Msg($"Button3 pressed by {buton3UserId.Value} {sel.Name}");
                MyVisualScriptLogicProvider.AddGPS(sel.Name, "Aproximate position of detected navigation hazzard.", sel.SubZonePosition, VRageMath.Color.White, GPSDisplayPeriod, buton3UserId.Value);
                MyVisualScriptLogicProvider.SendChatMessageColored($"Added GPS for {sel.Name}", Color.Yellow, "GPS", buton1UserId.Value);
            }
            buton1UserId.Value = 0; //hack to get them to reset
            buton2UserId.Value = 0;
            buton3UserId.Value = 0;
        }

        internal void ButtonPressed(IMyEntity user, int button) //client
        {
            if (!block.Enabled)
                return;

            long identityId = MyAPIGateway.Session.Player.IdentityId;

            Log.Msg($"Button pressed:{button} id={identityId}");
            switch (button)
            {
                case 1:
                    {
                        //Log.Msg($"buttonValue={buton1UserId.Value}");
                        buton1UserId.Value = buton1UserId.Value == 0 ? identityId : 0;
                        buton2UserId.Value = 0;//hack to get them to reset
                        buton3UserId.Value = 0;
                        break;
                    }
                case 2:
                    {
                        //Log.Msg($"buttonValue={buton2UserId.Value}");
                        buton2UserId.Value = buton2UserId.Value == 0 ? identityId : 0;
                        buton1UserId.Value = 0; //hack to get them to reset
                        buton3UserId.Value = 0;
                        break;
                    }
                case 3:
                    {
                        //Log.Msg($"buttonValue={buton3UserId.Value}");
                        buton3UserId.Value = buton3UserId.Value == 0 ? identityId : 0;
                        buton1UserId.Value = 0; //hack to get them to reset
                        buton2UserId.Value = 0;
                        break;
                    }
            }
        }
    }
}