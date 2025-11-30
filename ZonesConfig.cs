using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using VRageMath;


/*Font color string can be one of the following;
    Debug
    Red
    Green
    Blue
    White
    DarkBlue
    UrlNormal
    UrlHighlight
    ErrorMessageBoxCaption
    ErrorMessageBoxText
    InfoMessageBoxCaption
    InfoMessageBoxText
    ScreenCaption
    GameCredits
    LoadingScreen
    BuildInfo
    BuildInfoHighlight
*/

namespace ZoneControl
{
    public class ZonesConfig
    {
        const string configFilename = "Config-ZoneControl.xml";

        [XmlIgnore]
        public bool ConfigLoaded;
        [XmlIgnore]
        private static HashSet<string> fonts = new HashSet<string>() {"Debug","Red","Green","Blue", "White","DarkBlue","UrlNormal","UrlHighlight","ErrorMessageBoxCaption","ErrorMessageBoxText",
            "InfoMessageBoxCaption","InfoMessageBoxText","ScreenCaption","GameCredits","LoadingScreen","BuildInfo","BuildInfoHighlight"};


        public class InfoBase
        {
            public double AlertRadius = 0;
            public string AlertMessageEnter = "";
            public string ColourEnter = "";
            public string AlertMessageLeave = "";
            public string ColourLeave = "";
            public int AlertTimeMs = 0;
            public string FactionTag = "";
            public bool NoIntruders = false;
            public bool Wormhole = false;

            public InfoBase() { }
            protected void Set(InfoBase info)
            {
                AlertRadius = info.AlertRadius;
                AlertMessageEnter = info.AlertMessageEnter;
                ColourEnter = CheckFontColour(info.ColourEnter);
                AlertMessageLeave = info.AlertMessageLeave;
                ColourLeave = CheckFontColour(info.ColourLeave);
                AlertTimeMs = info.AlertTimeMs;
                FactionTag = info.FactionTag;
                NoIntruders = info.NoIntruders;
                Wormhole = info.Wormhole;
            }
        }

        public class ZoneInfo : InfoBase
        {
            public string UniqueName;
            public Vector3D Position;
            public double AlertRadiusSqrd;

            public ZoneInfo()
            {
            }

            public ZoneInfo(PositionInfo info)
            {
                Set(info);
                UniqueName = info.UniqueName;
                Position = info.Position;
                AlertRadiusSqrd = AlertRadius * AlertRadius;
            }

            public ZoneInfo(PlanetInfo info, Vector3D position)
            {
                Set(info);
                UniqueName = info.PlanetName;
                Position = position;
                AlertRadiusSqrd = AlertRadius * AlertRadius;
            }

            public bool InZone(Vector3D position)
            {
                return Vector3D.DistanceSquared(position, Position) < AlertRadiusSqrd;

                /*                var d = Vector3D.DistanceSquared(position, Position);
                                Log.Msg($"Distance={d} AlertRadiusSqrd={AlertRadiusSqrd}");
                                return d < AlertRadiusSqrd;*/
            }
        }

        public class PositionInfo : InfoBase
        {
            public string UniqueName = "";
            public Vector3D Position;

            public PositionInfo() { }
        }

        public class PlanetInfo : InfoBase
        {
            public string PlanetName = "";

            public PlanetInfo() { }
        }

        public string ChatSenderName = "DSM";
        public string IntruderMessage = "You are an Intruder!";
        public string IntruderChatMessagePt1 = "Good news; intruder";
        public string IntruderChatMessagePt2 = "did not read the rules and is now dying in space.";
        public string IntruderColour = "Red";
        public int IntruderAlertTimeMs = 5000;
        public List<PositionInfo> Positions;
        public List<PlanetInfo> Planets;


        public ZonesConfig()
        {
            Positions = new List<PositionInfo>();
            Planets = new List<PlanetInfo>();
        }

        public static ZonesConfig Load()
        {
            if (MyAPIGateway.Utilities.FileExistsInWorldStorage(configFilename, typeof(ZonesConfig)) == true)
            {
                try
                {
                    ZonesConfig config = null;
                    var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(configFilename, typeof(ZonesConfig));
                    string configcontents = reader.ReadToEnd();
                    config = MyAPIGateway.Utilities.SerializeFromXML<ZonesConfig>(configcontents);
                    config.ConfigLoaded = true;
                    Log.Msg($"Loaded Existing Settings From {configFilename}");
                    return config;
                }
                catch (Exception exc)
                {
                    Log.Msg(exc.ToString());
                    Log.Msg($"ERROR: Could Not Load Settings From {configFilename}. Using Empty Configuration.");
                    return new ZonesConfig();
                }

            }

            Log.Msg($"{configFilename} Doesn't Exist. Creating Default Configuration. ");

            var defaultSettings = new ZonesConfig();
            defaultSettings.Positions.Add(new PositionInfo() { UniqueName = "Example1", Position = Vector3D.Zero });
            defaultSettings.Planets.Add(new PlanetInfo() { PlanetName = "EarthLike-12345d120000" });

            try
            {
                using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(configFilename, typeof(ZonesConfig)))
                {
                    writer.Write(MyAPIGateway.Utilities.SerializeToXML<ZonesConfig>(defaultSettings));
                }

            }
            catch (Exception exc)
            {
                Log.Msg(exc.ToString());
                Log.Msg($"ERROR: Could Not Create {configFilename}. Default Settings Will Be Used.");
            }

            return defaultSettings;
        }

        private static string CheckFontColour(string font)
        {
            if (fonts.Contains(font))
                return font;

            Log.Msg($"Invalid colour in config: {font}");
            return "White";
        }
    }
}
