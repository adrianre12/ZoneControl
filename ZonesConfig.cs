using System.Collections.Generic;
using System.Xml.Serialization;

// GPS format
// GPS:Wormhole:76263.33:-78030.57:-35966.69:#FF75C9F1:
// GPS:Name:X:Y:Z:Colour:
// Colour not used.

namespace ZoneControl
{
    public class ZonesConfig : ZoneConfigBase
    {


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


    }
}
