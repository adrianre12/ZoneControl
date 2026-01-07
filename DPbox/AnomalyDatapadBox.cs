using Sandbox.Game;
using Sandbox.ModAPI;
using System;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace ZoneControl.DPbox
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_TerminalBlock), false, new[] { "AnomalyFreight1" })]
    internal class AnomalyDatapadBox : MyGameLogicComponent
    {
        const int DefaultRefreshPeriod = 30; //seconds

        public static Random GlobalRandom = new Random();

        private IMyTerminalBlock block;
        private int refreshPeriodTicks = DefaultRefreshPeriod * 60;
        private int refreshAfterFrame;
        private MyIni config = new MyIni();
        private MyInventory inventory;


        //private MyDefinitionId DatapadDefId = new MyDefinitionId(typeof(MyObjectBuilder_Datapad), "Datapad");

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Log.Msg("DPbox Init...");

            if (!MyAPIGateway.Session.IsServer)
                return;

            block = Entity as IMyTerminalBlock;

            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();

            block.ShowInTerminal = false;
            block.ShowInInventory = false;
            block.ShowInToolbarConfig = false;
            block.ShowOnHUD = false;
            inventory = (MyInventory)block.GetInventory();

            LoadConfigFromCD();
            refreshAfterFrame = MyAPIGateway.Session.GameplayFrameCounter + DefaultRefreshPeriod;
            NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateAfterSimulation100()
        {
            var currentFrame = MyAPIGateway.Session.GameplayFrameCounter;
            //Log.Msg($"Tick {block.CubeGrid.DisplayName} frame={currentFrame}, termExpiry={showInTerminalExpiary} refresh={refreshAfterFrame}");


            if (currentFrame < refreshAfterFrame)
                return;

            refreshAfterFrame = currentFrame + refreshPeriodTicks;

            LoadConfigFromCD();
            inventory.Clear();
            CreateDatapad();

        }

        private void CreateDatapad()
        {
            var dp = ZonesSession.Instance.GetRandomDatapad();
            if (dp != null)
                inventory.AddItems(1, dp);
        }

        private void LoadConfigFromCD()
        {
            if (!ParseConfigFromCD())
            {
                Log.Msg("Error in CD, creating a new config.");
                SaveConfigToCD();
            }
        }

        private void SaveConfigToCD()
        {
            Log.Msg("Saving config to CD.");

            config.Clear();
            var sb = new StringBuilder();
            sb.AppendLine($"Minimum RefreshPeriod = {DefaultRefreshPeriod} seconds");

            config.AddSection("Box");
            config.SetSectionComment("Box", sb.ToString());
            config.Set("Box", "RefreshPeriod", DefaultRefreshPeriod);

            config.Invalidate();
            block.CustomData = config.ToString();
        }

        private bool ParseConfigFromCD()
        {
            //Log.Msg("ParseConfigFromCD");
            if (config.TryParse(block.CustomData))
            {
                if (!config.ContainsSection("Box"))
                    return false;

                int refreshPeriod;
                if (!config.Get("Box", "RefreshPeriod").TryGetInt32(out refreshPeriod))
                    return false;
                refreshPeriodTicks = Math.Max(DefaultRefreshPeriod, refreshPeriod) * 60;

                return true;
            }
            Log.Msg("Error: Failed to load config");
            return false;
        }
    }
}
