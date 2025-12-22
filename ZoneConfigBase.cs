using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using VRageMath;
using static ZoneControl.ZonesConfig;

namespace ZoneControl
{
    public class ZoneConfigBase
    {
        internal const string configFilename = "Config-ZoneControl.xml";

        [XmlIgnore]
        public bool ConfigLoaded;
        [XmlIgnore]
        private static HashSet<string> fonts = new HashSet<string>() {"Debug","Red","Green","Blue", "White","DarkBlue","UrlNormal","UrlHighlight","ErrorMessageBoxCaption","ErrorMessageBoxText",
            "InfoMessageBoxCaption","InfoMessageBoxText","ScreenCaption","GameCredits","LoadingScreen","BuildInfo","BuildInfoHighlight"};

        public class IntruderInfo
        {
            public enum PunishmentType
            {
                None,
                Disable,
                Destroy
            }

            public string ChatSenderName = "DSM";
            public string Message = "You are an Intruder!";
            public string ChatMessagePt1 = "Good news; Intruder";
            public string ChatMessagePt2 = "did not read the rules and is now being punished.";
            public string Colour = "Red";
            public string PunishmentMsg = "Your Gyros and JumpDrives are disabled for 20 minutes. Ask an admin for help.";
            public int AlertTimeMs = 9000;
            public PunishmentType Punishment = PunishmentType.Disable;
            public string AdminTestFactionTag = "DSM";
        }

        public class InfoCommon
        {
            public double AlertRadius = 0;
            public string AlertMessageEnter = "Enter";
            public string ColourEnter = "Red";
            public string AlertMessageLeave = "Leave";
            public string ColourLeave = "Green";
            public int AlertTimeMs = 9000;
            public string FactionTag = "";

            public InfoCommon() { }
            protected void Set(InfoCommon info)
            {
                AlertRadius = info.AlertRadius;
                AlertMessageEnter = info.AlertMessageEnter ?? "";
                ColourEnter = CheckFontColour(info.ColourEnter);
                AlertMessageLeave = info.AlertMessageLeave ?? "";
                ColourLeave = CheckFontColour(info.ColourLeave);
                AlertTimeMs = info.AlertTimeMs;
                FactionTag = info.FactionTag != null ? info.FactionTag.Trim() : "";
            }
        }

        private static string CheckFontColour(string font)
        {
            if (fonts.Contains(font))
                return font;

            Log.Msg($"Invalid colour in config: {font}");
            return "White";
        }

        public static ZonesConfig LoadConfig()
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
            defaultSettings.Zones.Add(new ZoneInfo()
            {
                UniqueName = "Example1",
                GPS = "GPS:Anything:0:0:0:Anything:",
                Info = (new InfoCommon() { FactionTag = "ABC", AlertRadius = 100 })
            });
            defaultSettings.Wormholes.Add(new WormholeInfo()
            {
                UniqueName = "ExampleWormhole",
                GPS = "GPS:Anything:0:0:0:Anything:",
                Info = (new InfoCommon() { AlertRadius = 100 }),
                Locations = new List<string>() { "GPS:TargetName1:0:0:0:Anything:", "GPS:TargetName2:0:0:0:Anything:" }
            });
            defaultSettings.Planets.Add(new PlanetInfo()
            {
                PlanetName = "EarthLike-12345d120000",
                Info = (new InfoCommon() { AlertMessageEnter = "Entering EarthLike", AlertMessageLeave = "Leaving EarthLike", AlertRadius = 70000 })
            });

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

        [ProtoContract]
        public class GPSposition
        {
            [ProtoMember(1)]
            public string Name = "Error";
            [ProtoMember(2)]
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

                Name = tmp[1];
                Position = new Vector3D(x, y, z);
            }
        }
    }
}
