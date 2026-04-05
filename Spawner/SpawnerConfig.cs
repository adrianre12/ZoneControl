using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using VRageMath;
using static ZoneControl.Utils;

// GPS format
// GPS:Wormhole:76263.33:-78030.57:-35966.69:#FF75C9F1:
// GPS:Name:X:Y:Z:Colour:
// Colour not used.


namespace ZoneControl
{
    public class SpawnerConfig
    {
        internal const string configFilename = "Config-ZoneSpawner.xml";

        [XmlIgnore]
        public bool ConfigLoaded;

        public string UpdatePeriodMins = null;
        public bool Enabled = false;
        public int MaxSpawns = 10;
        public int SpawnRateMultiplier = 1;
        public float AlertRadius = 2000;
        public string AlertMessageEnter = "Entering [NAME]";
        public string ColourEnter = "Green";
        public string AlertMessageLeave = "Leaving [NAME]";
        public string ColourLeave = "Green";
        public int AlertTimeMs = 9000;
        public string MessageWarn = "Caution: Anomaly is unstable";
        public string MessageUrgent = "Alert: Anomaly collapse started";
        public string MessageColour = "Red";
        public string DataPadTitle = "Bulletin: [NAME]";
        public string DataPadMessage = "An Anomaly has been detected and a navigation warning zone established.\n\nDetails:\n   Identifier: [NAME]\n   Reason: Collision hazard, wreckage detected.\n   Position: [GPS]\n\nNotes:\nThe position is approximate.\nAnomalies collapse is instantaneous, if instabilities are detected attempts will be made to notify Engineers in the vicinity.";
        public string GPScolourHex = "#FFFFFFFF";
        public string FactionTag = "ANOM";
        public string PirateTag = "SPRT";
        public string PiratePrefab = "Sentinel";
        public List<SpawningSector> Sectors = new List<SpawningSector>();

        public void Verify()
        {
            MaxSpawns = MaxSpawns < 0 ? 0 : MaxSpawns;
            SpawnRateMultiplier = SpawnRateMultiplier < 0 ? 0 : SpawnRateMultiplier;
            AlertRadius = AlertRadius < 0 ? 0 : AlertRadius;
            AlertMessageEnter = AlertMessageEnter ?? "";
            ColourEnter = CheckFontColour(ColourEnter);
            AlertMessageLeave = AlertMessageLeave ?? "";
            ColourLeave = CheckFontColour(ColourLeave);
            AlertTimeMs = AlertTimeMs < 0 ? 0 : AlertTimeMs; ;
            MessageWarn = MessageWarn ?? "";
            MessageUrgent = MessageUrgent ?? "";
            MessageColour = CheckFontColour(MessageColour);
            DataPadTitle = DataPadTitle ?? "";
            DataPadMessage = DataPadMessage ?? "";
            GPScolourHex = GPScolourHex ?? "#FFFFFFFF";
            FactionTag = FactionTag ?? "ANOM";
            PirateTag = PirateTag ?? "SPRT";
            PiratePrefab = PiratePrefab ?? "Sentinel";
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
            public int GroupId = -1;
            public float Weighting = 1.0f;
            public float LifetimeMin = 12;
            public float LifetimeMax = 48;
            public float PirateProbability = 0;
            public string PiratePrefab = "";
            public string PirateAntenna = "Scanning Antenna";
            public float PirateDistance = 1000;
        }

        public static SpawnerConfig LoadConfig()
        {
            if (MyAPIGateway.Utilities.FileExistsInWorldStorage(configFilename, typeof(SpawnerConfig)) == true)
            {
                try
                {
                    SpawnerConfig config = null;
                    var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(configFilename, typeof(SpawnerConfig));
                    string configcontents = reader.ReadToEnd();
                    config = MyAPIGateway.Utilities.SerializeFromXML<SpawnerConfig>(configcontents);
                    config.ConfigLoaded = true;
                    Log.Msg($"Loaded Existing Settings From {configFilename}");
                    config.Verify();
                    return config;
                }
                catch (Exception exc)
                {
                    Log.Msg(exc.ToString());
                    Log.Msg($"ERROR: Could Not Load Settings From {configFilename}. Using Empty Configuration.");
                    return new SpawnerConfig();
                }

            }

            Log.Msg($"{configFilename} Doesn't Exist. Creating Default Configuration. ");

            var defaultSettings = new SpawnerConfig();

            defaultSettings.Sectors.Add(new SpawningSector()
            {
                UniqueName = "TestSector",
                GPS = "GPS:Anything:0:0:0:Anything:",
                Prefabs = new List<PrefabInfo>() { new PrefabInfo() { Subtype = "SubtypeName" } }
            });
            try
            {
                using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(configFilename, typeof(ZonesConfig)))
                {
                    writer.Write(MyAPIGateway.Utilities.SerializeToXML<SpawnerConfig>(defaultSettings));
                }
            }
            catch (Exception exc)
            {
                Log.Msg(exc.ToString());
                Log.Msg($"ERROR: Could Not Create {configFilename}. Default Settings Will Be Used.");
            }

            return defaultSettings;
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

        public SectorInfoInternal(SpawnerConfig.SpawningSector sector)
        {
            UniqueName = sector.UniqueName;
            string tmp;
            TryParseGPSstring(sector.GPS, out tmp, out Position);
            Radius = sector.Radius;
        }
    }

    internal class PrefabInfoInternal
    {
        public string Subtype;
        public int GroupId = -1;
        public float Weighting = 1.0f;
        public double WeightNorm = 0;
        public float LifetimeMin = 12;
        public float LifetimeMax = 48;
        public float PirateProbability = 0;
        public string PiratePrefab = "";
        public string PirateAntenna = "Scanning Antenna";
        public float PirateDistance = 1000;
        public SectorInfoInternal SectorInfo = new SectorInfoInternal();

        public PrefabInfoInternal(SpawnerConfig.PrefabInfo prefab, SectorInfoInternal sectorInfo)
        {
            Subtype = prefab.Subtype;
            GroupId = prefab.GroupId;
            Weighting = prefab.Weighting;
            LifetimeMin = prefab.LifetimeMin;
            LifetimeMax = prefab.LifetimeMax;
            PirateProbability = prefab.PirateProbability;
            PiratePrefab = prefab.PiratePrefab ?? "";
            PirateAntenna = prefab.PirateAntenna;
            PirateDistance = prefab.PirateDistance;
            SectorInfo = sectorInfo;
        }
    }

}
