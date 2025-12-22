using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using VRageMath;

// GPS format
// GPS:Wormhole:76263.33:-78030.57:-35966.69:#FF75C9F1:
// GPS:Name:X:Y:Z:Colour:
// Colour not used.

namespace ZoneControl
{
    public class ZonesConfig : ZoneConfigBase
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

        public class ZoneInfo
        {
            public string UniqueName = "";
            public string GPS = "";
            public bool NoIntruders = false;
            public InfoCommon Info = new InfoCommon();

            public ZoneInfo() { }
        }

        public class SubZoneInfo
        {
            public string UniqueName = "";
            public string GPS = "";
            public InfoCommon Info = new InfoCommon();

            public SubZoneInfo() { }
        }

        public class WormholeInfo : SubZoneInfo
        {
            [XmlArray]
            [XmlArrayItem(ElementName = "GPS")]
            public List<string> Locations = new List<string>();

            public WormholeInfo() { }
        }

        public class PlanetInfo
        {
            public string PlanetName = "";
            public bool NoIntruders = false;
            public InfoCommon Info = new InfoCommon();

            public PlanetInfo() { }
        }

        public IntruderInfo Intruder = new IntruderInfo();
        public List<ZoneInfo> Zones;
        public List<PlanetInfo> Planets;
        public List<WormholeInfo> Wormholes;
        public List<SubZoneInfo> Anomalies;

        public ZonesConfig()
        {
            Zones = new List<ZoneInfo>();
            Wormholes = new List<WormholeInfo>();
            Planets = new List<PlanetInfo>();
            Anomalies = new List<SubZoneInfo>();
        }

        internal static List<ZoneInfoInternal> NewZoneList(ZonesConfig config)
        {
            List<ZoneInfoInternal> zones = new List<ZoneInfoInternal>();
            Dictionary<string, Vector3D> planetPositions = GetPlanetPositions();
            long zoneId = 0;
            foreach (var info in config.Zones)
            {
                var zone = new ZoneInfoInternal(zoneId, info);
                zones.Add(zone);
                Log.Msg($"Adding {zone.Type} {info.UniqueName} zoneId={zoneId} to Zones list");
                ++zoneId;
            }

            Vector3D planetPosition;
            foreach (var info in config.Planets)
            {
                if (planetPositions.TryGetValue(info.PlanetName, out planetPosition))
                { // Planets cant be wormholes so no targets.
                    var zone = new ZoneInfoInternal(zoneId, info, planetPosition);
                    zones.Add(zone);
                    Log.Msg($"Adding Planet {zone.Type} {info.PlanetName} zoneId={zoneId} to Zones list");
                    ++zoneId;
                }
            }

            foreach (var info in config.Wormholes)
            {
                var zone = new ZoneInfoInternal(zoneId, info);
                zones.Add(zone);
                Log.Msg($"Adding {zone.Type} {info.UniqueName} zoneId={zoneId}  targets.Count={zone.Targets.Count} to Zones list");
                ++zoneId;
            }

            foreach (var info in config.Anomalies)
            {
                var zone = new ZoneInfoInternal(zoneId, ZoneInfoInternal.ZoneType.Anomaly, info);
                zones.Add(zone);
                Log.Msg($"Adding Zone {info.UniqueName} zoneId={zoneId} to Zones list");
                ++zoneId;
            }

            zones = zones.OrderBy(x => x.AlertRadius).ToList();
            //foreach (var zone in zones) Log.Msg($"Zone {zone.UniqueName} radius {zone.AlertRadius}");
            return zones;
        }


        private static Dictionary<string, Vector3D> GetPlanetPositions()
        {
            Dictionary<string, Vector3D> planetPositions = new Dictionary<string, Vector3D>();
            MyAPIGateway.Entities.GetEntities(null, e =>
            {
                if (e is MyPlanet)
                {
                    var planet = e as MyPlanet;
                    if (planetPositions.ContainsKey(planet.StorageName))
                    {
                        Log.Msg($"Error duplicate planet name found: {planet.StorageName}");
                        return false;
                    }
                    Log.Msg($"Planet Found {planet.StorageName}");
                    planetPositions.Add(planet.StorageName, planet.WorldMatrix.Translation);
                }
                return false;
            });
            return planetPositions;
        }
    }
}
