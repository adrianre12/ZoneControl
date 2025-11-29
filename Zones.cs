using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.Components;

namespace ZoneControl
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    internal class Zones : MySessionComponentBase
    {
        public static Zones Instance;

        private ZonesConfig config;
        private List<ZonesConfig.ZoneInfo> zonePositions;


        public override void LoadData()
        {
            Instance = this;
        }
        protected override void UnloadData()
        {
            Instance = null;
        }

        public override void BeforeStart()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            Log.Msg("ZoneNotification Start");
            config = ZonesConfig.Load();
            //zonePositions = new List<GPS>();
        }
    }
}
