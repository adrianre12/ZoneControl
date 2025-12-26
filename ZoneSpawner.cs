using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRageMath;

namespace ZoneControl
{
    internal class ZoneSpawner
    {
        [ProtoContract]
        public class Spawn
        {
            [ProtoMember(1)]
            public Vector3D Position = Vector3D.MaxValue;
            [ProtoMember(2)]
            public long RemoveAt = 0; //system DateTime ticks
            [ProtoMember(3)]
            public Vector3D ZonePosition = Vector3D.MaxValue;
        }

        [ProtoContract]
        internal class SpawnedInfo
        {
            [ProtoMember(1)]
            public List<Spawn> Spawns = new List<Spawn>();

            public SpawnedInfo() { }
        }

        const int DefaultRefreshPeriodTicks = 60 * 60 * 15; //15 mins

        private int nextRefreshFrame = DefaultRefreshPeriodTicks; // frame counter should be 0 at startup
        private ZoneTable subZoneTable;
        private List<PrefabInfoInternal> prefabs;
        private bool updateSpawns;
        private SpawnedInfo spawnedInfo = new SpawnedInfo();
        private int nextSpawnIndex = 0;

        public ZoneSpawner(ZonesConfig config, ZoneTable subZoneTable)
        {
            this.subZoneTable = subZoneTable;
            prefabs = new List<PrefabInfoInternal>();

            double totalWeighting = 0;

            //Read Config
            foreach (var sector in config.Spawner.Sectors)
            {
                SectorInfoInternal sectorInfo = new SectorInfoInternal(sector);
                foreach (var prefab in sector.Prefabs)
                {
                    PrefabInfoInternal prefabInfo = new PrefabInfoInternal(prefab, sectorInfo);
                    prefabs.Add(prefabInfo);
                    totalWeighting += prefabInfo.Weighting;
                }
            }

            foreach (PrefabInfoInternal pi in prefabs)
            {
                pi.WeightNorm = pi.Weighting / totalWeighting;
                Log.Msg($"Prefab loaded {pi.Subtype} Sector={pi.SectorInfo.UniqueName} WeightNorm={pi.WeightNorm}");
            }
        }


        internal void Update()
        {
            if (updateSpawns)
            {
                Log.Msg("Updating spawns.");
                //do the loop
                if (nextSpawnIndex < spawnedInfo.Spawns.Count)
                {// update spawns
                    Spawn spawn = spawnedInfo.Spawns[nextSpawnIndex];
                    //remove if too old
                    long now = DateTime.Now.Ticks;
                    if (spawn.RemoveAt < now)
                        RemoveSpawn(spawn);

                    //spawn some more?


                    nextSpawnIndex++;
                    return;
                }

                //All done
                nextSpawnIndex = 0;
                updateSpawns = false;
            }

            var currentFrame = MyAPIGateway.Session.GameplayFrameCounter;
            if (currentFrame < nextRefreshFrame)
                return;
            nextRefreshFrame = currentFrame + DefaultRefreshPeriodTicks;
            updateSpawns = true;

        }

        private void RemoveSpawn(Spawn spawn)
        {
            Log.Msg("TODO REMOVE SPAWN");
            spawnedInfo.Spawns.Remove(spawn);
        }
    }
}
