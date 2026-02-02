using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using VRage.Game.ModAPI;
using VRageMath;

namespace ZoneControl
{
    internal class Utils
    {
        const string regxArgs = " (?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))";
        public enum Fonts { Red, Green, Blue, White, DarkBlue };


        internal static string CheckFontColour(string font)
        {
            if (Enum.IsDefined(typeof(Fonts), font))
                return font;

            Log.Msg($"Invalid colour in config: {font}");
            return "White";
        }
        internal static string CheckColour(string colour)
        {
            if (colour == null || colour.Trim().Length == 0)
                return "White";
            return colour.Trim();
        }

        internal static string TextReplace(string text, string key1, string value1, string key2 = null, string value2 = null)
        {
            if (text == null)
                return "";
            var sb = new StringBuilder(text.Trim());
            sb.Replace(key1, value1);
            if (key2 == null || value2 == null)
                return sb.ToString();
            sb.Replace(key2, value2);
            return sb.ToString();
        }
        internal static bool TryParseGPSstring(string gps, out string name, out Vector3D position)
        {
            name = "Error";
            position = Vector3D.MinValue;
            string[] tmp = gps.ToLower().Split(':');
            if (tmp[0] != "gps" || tmp.Length < 5)
            {
                Log.Msg($"Invalid GPS, does not start with GPS or is too short '{gps}'");
                return false;
            }

            double x;
            double y;
            double z;
            if (!double.TryParse(tmp[2], out x) || !double.TryParse(tmp[3], out y) || !double.TryParse(tmp[4], out z))
            {
                Log.Msg($"Invalid GPS, failed to parse X,Y,Z '{gps}'");
                return false;
            }

            name = tmp[1];
            position = new Vector3D(x, y, z);
            return true;
        }

        internal static string VectorToGPS(string name, Vector3D position, string colour = "#FFFFFFFF") //#FF00FF8C pale blue
        {
            return $"GPS:{name}:{position.X:0.00}:{position.Y:0.00}:{position.Z:0.00}:{colour}:";
        }

        internal static long FindFactionId(string tag)
        {
            IMyFaction faction = null;
            if (tag != null)
                faction = MyAPIGateway.Session.Factions.TryGetFactionByTag(tag.Trim());
            if (faction != null)
            {
                Log.Msg($"Spawnwer using faction {tag}");
                return faction.FounderId;
            }
            faction = MyAPIGateway.Session.Factions.TryGetFactionByTag("UNKN");
            if (faction != null)
            {
                Log.Msg($"Spawnwer using default faction UNKN");
                return faction.FounderId;
            }
            Log.Msg($"Spawnwer UNKN not found using NOBODY");
            return 0;
        }

        internal static List<string> GetArgs(string msg)
        {
            var parts = Regex.Split(msg, regxArgs);
            List<string> args = new List<string>();
            foreach (var part in parts)
            {
                string arg = part.Trim(new char[] { ' ', '"' });
                if (arg.Length == 0)
                    continue;

                args.Add(arg);
            }
            return args;
        }
    }
}
