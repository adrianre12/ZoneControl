using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using VRageMath;

// GPS format
// GPS:Wormhole:76263.33:-78030.57:-35966.69:#FF75C9F1:
// GPS:Name:X:Y:Z:Colour:
// Colour not used.

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
            public string AlertMessageEnter = "Enter";
            public string ColourEnter = "Red";
            public string AlertMessageLeave = "Leave";
            public string ColourLeave = "Green";
            public int AlertTimeMs = 9000;
            public string FactionTag = "";
            public bool NoIntruders = false;

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
            }
        }

        public class ZoneInfo : InfoBase
        {
            public string UniqueName = "";
            public Vector3D Position;
            public bool Wormhole = false;
            public double AlertRadiusSqrd;
            public List<GPSposition> Targets;

            public ZoneInfo()
            {
            }

            public ZoneInfo(PositionInfo info)
            {
                Set(info);
                UniqueName = info.UniqueName;
                GPSposition gp = new GPSposition(info.GPS);
                Position = gp.Position;
                Wormhole = info.Wormhole;
                AlertRadiusSqrd = AlertRadius * AlertRadius;
                Targets = new List<GPSposition>();
                foreach (string location in info.Locations)
                {
                    Targets.Add(new GPSposition(location));
                }

                Log.Msg($"Zone {UniqueName} Targets.Count={Targets.Count}");
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
            }
        }

        public class PositionInfo : InfoBase
        {
            public string UniqueName = "";
            public string GPS = "";
            public bool Wormhole = false;
            public List<string> Locations;

            public PositionInfo() { }
        }

        public class PlanetInfo : InfoBase
        {
            public string PlanetName = "";

            public PlanetInfo() { }
        }

        public string ChatSenderName = "DSM";
        public string IntruderMessage = "You are an Intruder!";
        public string IntruderChatMessagePt1 = "Good news; Intruder";
        public string IntruderChatMessagePt2 = "did not read the rules and is now being punished.";
        public string IntruderColour = "Red";
        public string IntruderPunishmentMsg = "Your Gyros and JumpDrives are disabled for 20 minutes. Ask an admin for help.";
        public int IntruderAlertTimeMs = 9000;
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
            defaultSettings.Positions.Add(new PositionInfo() { UniqueName = "Example1", GPS = "GPS:Anything:0:0:0:Anything:" });
            defaultSettings.Positions.Add(new PositionInfo() { UniqueName = "ExampleWormhole", GPS = "GPS:Anything:0:0:0:Anything:", Wormhole = true, Locations = new List<string>() { "GPS:TargetName1:0:0:0:Anything:", "GPS:TargetName2:0:0:0:Anything:" } });
            defaultSettings.Planets.Add(new PlanetInfo() { PlanetName = "EarthLike-12345d120000", AlertMessageEnter = "Entering EarthLike", AlertMessageLeave = "Leaving EarthLike", AlertRadius = 70000 });

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

        public class GPSposition
        {
            public string Name = "Error";
            public Vector3D Position = Vector3D.Zero;

            public GPSposition() { }

            public GPSposition(string name, Vector3D position)
            {
                Name = name;
                Position = position;
            }

            public GPSposition(string gps)
            {
                string[] tmp = gps.ToLower().Split(':');
                if (tmp[0] != "gps" || tmp.Length < 5)
                {
                    Log.Msg($"Invalid GPS, does not start with GPS or is too short '{gps}'");
                    return;
                }

                double x;
                double y;
                double z;
                if (!double.TryParse(tmp[2], out x) || !double.TryParse(tmp[3], out y) || !double.TryParse(tmp[4], out z))
                {
                    Log.Msg($"Invalid GPS, failed to parse X,Y,Z '{gps}'");
                    return;
                }

                Name = tmp[2];
                Position = new Vector3D(x, y, z);
            }
        }


    }
}
