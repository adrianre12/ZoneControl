using System.Collections.Generic;
using VRageMath;

namespace ZoneControl.Spawner
{
    internal class SpawnSummary
    {
        public List<SummaryItem> Items = new List<SummaryItem>();
        public List<SummaryItem> Selected = new List<SummaryItem>();

        public SpawnSummary()
        {
            Items = new List<SummaryItem>();
            Selected = new List<SummaryItem>();
        }

        public SpawnSummary(CurrentSpawnsData currentSpawns)
        {
            Items = new List<SummaryItem>();
            foreach (var spawn in currentSpawns.Spawns)
            {
                Items.Add(new SummaryItem(spawn));
            }
        }


    }
    internal class SummaryItem
    {
        public string Name = "";
        public long RemoveAt = 0; //system DateTime ticks
        public Vector3D SubZonePosition = Vector3D.MaxValue;
        public long AnomalyId = 0;

        public SummaryItem()
        {
        }

        public SummaryItem(SpawnInfo spawnInfo)
        {
            Name = spawnInfo.Name;
            RemoveAt = spawnInfo.RemoveAt;
            SubZonePosition = spawnInfo.SubZonePosition;
            AnomalyId = spawnInfo.AnomalyId;
        }
    }
}
