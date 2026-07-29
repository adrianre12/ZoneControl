using Sandbox.ModAPI;
using System.Collections.Generic;
using VRageMath;

namespace ZoneControl.Spawner
{
    internal class SpawnSummary
    {
        public List<SummaryItem> Selected = new List<SummaryItem>();
        private int summaryPtr;
        public int UpdatedAtFrame;

        public SpawnSummary()
        {
            Selected = new List<SummaryItem>();
        }

        public SpawnSummary(CurrentSpawnsData currentSpawns)
        {
            summaryPtr = 0;
            UpdateSelected(currentSpawns);
        }

        public void UpdateSelected(CurrentSpawnsData currentSpawns)
        {
            /*            Selected.Clear();
                        int numRows = Math.Min(3, currentSpawns.Spawns.Count);
                        for (int i = 0; i < numRows; i++)
                        {
                            if (summaryPtr >= currentSpawns.Spawns.Count)
                                summaryPtr = 0;
                            SummaryItem summaryItem = new SummaryItem(currentSpawns.Spawns[summaryPtr]);
                            Selected.Add(summaryItem);
                            //if (Log.Debug) Log.Msg($"Selected {summaryPtr} {summaryItem.Name}");
                            summaryPtr++;
                        }
                        UpdatedAtFrame = MyAPIGateway.Session.GameplayFrameCounter;*/

            Selected.Clear();
            UpdatedAtFrame = MyAPIGateway.Session.GameplayFrameCounter;
            if (currentSpawns.Spawns.Count == 0)
            {
                summaryPtr = 0;
                return;
            }
            int startPtr = summaryPtr;
            while (Selected.Count < 3)
            {
                if (summaryPtr >= currentSpawns.Spawns.Count)
                    summaryPtr = 0;
                var spawn = currentSpawns.Spawns[summaryPtr];
                if (spawn.Type == SpawnType.Wreck)
                {
                    SummaryItem summaryItem = new SummaryItem(spawn);
                    Selected.Add(summaryItem);
                }
                //if (Log.Debug) Log.Msg($"Selected {summaryPtr} {summaryItem.Name}");
                summaryPtr++;
                if (summaryPtr == startPtr)
                    break;
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
