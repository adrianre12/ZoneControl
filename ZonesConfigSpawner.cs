// GPS format
// GPS:Wormhole:76263.33:-78030.57:-35966.69:#FF75C9F1:
// GPS:Name:X:Y:Z:Colour:
// Colour not used.

using System.Collections.Generic;
using VRageMath;

namespace ZoneControl
{
    public partial class ZonesConfig
    {
        public class SpawnerInfo
        {
            public bool Enabled = false;
            public int MaxSpawns = 10;
            public float SpawnProbabilityPerHour = 1.0f;
            public double SubZoneRadius = 2000;
            public List<SpawningSector> Sectors = new List<SpawningSector>();
        }

        public class SpawningSector
        {
            public string UniqueName;
            public string GPS;
            public float Radius = 100000;

            public List<PrefabInfo> Prefabs = new List<PrefabInfo>();
        }

        public class PrefabInfo
        {
            public string Subtype;
            public float Weighting = 1.0f;
            public int LifetimeMin = 12;
            public int LifetimeMax = 48;
        }
    }


    internal class SectorInfoInternal
    {
        public string UniqueName = "";
        public Vector3D Position = Vector3D.MinValue;
        public float Radius = 100000;

        public SectorInfoInternal()
        {
        }

        public SectorInfoInternal(ZonesConfig.SpawningSector sector)
        {
            UniqueName = sector.UniqueName;
            string tmp;
            ZonesConfigBase.TryParseGPSstring(sector.GPS, out tmp, out Position);
            Radius = sector.Radius;
        }
    }

    internal class PrefabInfoInternal
    {
        public string Subtype;
        public float Weighting = 1.0f;
        public double WeightNorm = 0;
        public int LifetimeMin = 12;
        public int LifetimeMax = 48;
        public SectorInfoInternal SectorInfo = new SectorInfoInternal();

        public PrefabInfoInternal(ZonesConfig.PrefabInfo prefab, SectorInfoInternal sectorInfo)
        {
            Subtype = prefab.Subtype;
            Weighting = prefab.Weighting;
            LifetimeMin = prefab.LifetimeMin;
            LifetimeMax = prefab.LifetimeMax;
            SectorInfo = sectorInfo;
        }
    }

}
