using Sandbox.Game.Entities;
using Sandbox.ModAPI;
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
            public long Id = -1;
            public string UniqueName = "";
            public Vector3D Position;
            public bool Wormhole = false;
            public double AlertRadiusSqrd;
            public List<GPSposition> Targets = new List<GPSposition>();

            public ZoneInfoInternal()
            {
            }

            public ZoneInfoInternal(long id, PositionInfo info)
            {
                Set(info.Info);
                Id = id;
                UniqueName = info.UniqueName;
                GPSposition gp = new GPSposition(info.GPS);
                Position = gp.Position;
                AlertRadiusSqrd = AlertRadius * AlertRadius;
            }

            public ZoneInfoInternal(long id, WormholeInfo info)
            {
                Set(info.Info);
                Id = id;
                UniqueName = info.UniqueName;
                GPSposition gp = new GPSposition(info.GPS);
                Position = gp.Position;
                AlertRadiusSqrd = AlertRadius * AlertRadius;
                Wormhole = true;
                foreach (string location in info.Locations)
                {
                    Targets.Add(new GPSposition(location));
                }

                Log.Msg($"Zone {UniqueName} Targets.Count={Targets.Count}");
            }

            public ZoneInfoInternal(long id, PlanetInfo info, Vector3D position)
            {
                Set(info.Info);
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

        public class PositionInfo
        {
            public string UniqueName = "";
            public string GPS = "";
            public InfoCommon Info = new InfoCommon();


            public PositionInfo() { }
        }

        public class WormholeInfo : PositionInfo
        {
            [XmlArray]
            [XmlArrayItem(ElementName = "GPS")]
            public List<string> Locations = new List<string>();

            public WormholeInfo() { }
        }

        public class PlanetInfo
        {
            public string PlanetName = "";
            public InfoCommon Info = new InfoCommon();

            public PlanetInfo() { }
        }

        public enum PunishmentType
        {
            None,
            Disable,
            Destroy
        }

        public string ChatSenderName = "DSM";
        public string IntruderMessage = "You are an Intruder!";
        public string IntruderChatMessagePt1 = "Good news; Intruder";
        public string IntruderChatMessagePt2 = "did not read the rules and is now being punished.";
        public string IntruderColour = "Red";
        public string IntruderPunishmentMsg = "Your Gyros and JumpDrives are disabled for 20 minutes. Ask an admin for help.";
        public int IntruderAlertTimeMs = 9000;
        public PunishmentType IntruderPunishment = PunishmentType.Disable;
        public List<PositionInfo> Positions;
        public List<PlanetInfo> Planets;
        public List<WormholeInfo> Wormholes;

        public ZonesConfig()
        {
            Positions = new List<PositionInfo>();
            Wormholes = new List<WormholeInfo>();
            Planets = new List<PlanetInfo>();
        }

        internal static List<ZoneInfoInternal> NewZoneList(ZonesConfig config)
        {
            List<ZoneInfoInternal> zones = new List<ZoneInfoInternal>();
            Dictionary<string, Vector3D> planetPositions = GetPlanetPositions();
            long zoneId = 0;
            foreach (var info in config.Positions)
            {
                var zone = new ZoneInfoInternal(zoneId, info);
                zones.Add(zone);
                Log.Msg($"Adding Zone {info.UniqueName} zoneId={zoneId} to Zones list");
                ++zoneId;
            }

            Vector3D planetPosition;
            foreach (var info in config.Planets)
            {
                if (planetPositions.TryGetValue(info.PlanetName, out planetPosition))
                { // Planets cant be wormholes so no targets.
                    zones.Add(new ZoneInfoInternal(zoneId, info, planetPosition));
                    Log.Msg($"Adding Planet Zone {info.PlanetName} zoneId={zoneId} to Zones list");
                    ++zoneId;
                }
            }

            foreach (var info in config.Wormholes)
            {
                var zone = new ZoneInfoInternal(zoneId, info);
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
