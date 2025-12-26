using System;
using System.Collections.Generic;
using VRageMath;
using static ZoneControl.ZonesConfigBase;
using static ZoneControl.ZonesConfig;

namespace ZoneControl
{
    public class ZoneInfoInternal : InfoCommon
    {
        public bool NoIntruders = false;
        public long Id = -1;
        public string UniqueName = "";
        public Vector3D Position;
        public ZoneType Type = ZoneType.Zone;
        public double AlertRadiusSqrd;
        public List<GPSposition> Targets = new List<GPSposition>();

        [Flags]
        public enum ZoneType
        {
            Zone,
            Wormhole,
            Anomaly
        }
        public ZoneInfoInternal()
        {
        }

        public ZoneInfoInternal(long id, ZoneInfo info)
        {
            Set(info.Info);
            NoIntruders = info.NoIntruders;
            Id = id;
            UniqueName = info.UniqueName;
            GPSposition gp = new GPSposition(info.GPS);
            Position = gp.Position;
            AlertRadiusSqrd = AlertRadius * AlertRadius;
        }

        public ZoneInfoInternal(long id, ZoneType type, SubZoneInfo info)
        {
            Set(info.Info);
            Id = id;
            UniqueName = info.UniqueName;
            GPSposition gp = new GPSposition(info.GPS);
            Position = gp.Position;
            AlertRadiusSqrd = AlertRadius * AlertRadius;
            Type = type;
        }

        public ZoneInfoInternal(long id, WormholeInfo info)
        {
            Set(info.Info);
            Id = id;
            UniqueName = info.UniqueName;
            GPSposition gp = new GPSposition(info.GPS);
            Position = gp.Position;
            AlertRadiusSqrd = AlertRadius * AlertRadius;
            Type = ZoneType.Wormhole;
            foreach (string location in info.Locations)
            {
                Targets.Add(new GPSposition(location));
            }
        }

        public ZoneInfoInternal(long id, PlanetInfo info, Vector3D position)
        {
            Set(info.Info);
            NoIntruders = info.NoIntruders;
            Id = id;
            UniqueName = info.PlanetName;
            Position = position;
            AlertRadiusSqrd = AlertRadius * AlertRadius;
        }

        public bool InZone(Vector3D position)
        {
            return Vector3D.DistanceSquared(position, Position) < AlertRadiusSqrd;
        }
    }
}
