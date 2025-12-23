using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace ZoneControl
{

    internal class ZoneDictionary
    {
        const double CacheMovementLimitSqrd = 100; //10m

        internal List<ZoneInfoInternal> Zones { get; private set; }

        private struct ZoneCacheItem
        {
            public Vector3D Position;
            public ZoneInfoInternal Zone;
        }
        private Dictionary<long, ZoneCacheItem> cache = new Dictionary<long, ZoneCacheItem>();

        public ZoneDictionary()
        {
            Zones = new List<ZoneInfoInternal>();
        }

        internal static ZoneDictionary NewZoneDictionary(ZonesConfig config)
        {
            Log.Msg("NewZoneDictionary()");
            ZoneDictionary dict = new ZoneDictionary();

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

            long zoneId = 0;
            foreach (var info in config.Zones)
            {
                var zone = new ZoneInfoInternal(zoneId, info);
                dict.Zones.Add(zone);
                Log.Msg($"Adding {zone.Type} {info.UniqueName} zoneId={zoneId} to Zones list");
                ++zoneId;
            }

            Vector3D planetPosition;
            foreach (var info in config.Planets)
            {
                if (planetPositions.TryGetValue(info.PlanetName, out planetPosition))
                { // Planets cant be wormholes so no targets.
                    var zone = new ZoneInfoInternal(zoneId, info, planetPosition);
                    dict.Zones.Add(zone);
                    Log.Msg($"Adding Planet {zone.Type} {info.PlanetName} zoneId={zoneId} to Zones list");
                    ++zoneId;
                }
            }

            dict.Zones = dict.Zones.OrderBy(x => x.AlertRadius).ToList();
            //foreach (var zone in dict.Zones) Log.Msg($"Zone {zone.UniqueName} radius {zone.AlertRadius}");
            return dict;
        }

        internal static ZoneDictionary NewSubZoneDictionary(ZonesConfig config)
        {
            Log.Msg("NewSubZoneDictionary()");
            ZoneDictionary dict = new ZoneDictionary();
            long zoneId = 0;

            foreach (var info in config.Wormholes)
            {
                var zone = new ZoneInfoInternal(zoneId, info);
                dict.Zones.Add(zone);
                Log.Msg($"Adding {zone.Type} {info.UniqueName} zoneId={zoneId}  targets.Count={zone.Targets.Count} to Zones list");
                ++zoneId;
            }

            foreach (var info in config.Anomalies)
            {
                var zone = new ZoneInfoInternal(zoneId, ZoneInfoInternal.ZoneType.Anomaly, info);
                dict.Zones.Add(zone);
                Log.Msg($"Adding Zone {info.UniqueName} zoneId={zoneId} to Zones list");
                ++zoneId;
            }

            dict.Zones = dict.Zones.OrderBy(x => x.AlertRadius).ToList();
            //foreach (var zone in dict.Zones) Log.Msg($"Zone {zone.UniqueName} radius {zone.AlertRadius}");
            return dict;
        }

        public bool GetZone(long Id, Vector3D position, out ZoneInfoInternal foundZone, out ZoneInfoInternal lastZone)
        {
            // check cached
            ZoneCacheItem cacheItem;
            bool cacheHit = cache.TryGetValue(Id, out cacheItem);
            if (cacheHit)
            {
                //Log.Msg("Cache Hit");
                if (Vector3D.DistanceSquared(cacheItem.Position, position) < CacheMovementLimitSqrd)
                {
                    if (cacheItem.Zone == null)
                    {
                        foundZone = null;
                        lastZone = null;
                        return false;
                    }
                    if (cacheItem.Zone.InZone(position)) //double check we have not moved out.
                    {
                        foundZone = cacheItem.Zone;
                        lastZone = cacheItem.Zone;
                        return false;
                    }
                }
                cache.Remove(Id);
            }
            // cache miss find closest
            //Log.Msg("Cache Miss or Moved");

            foundZone = FindClosestZone(position);
            lastZone = cacheItem.Zone;
            cache[Id] = new ZoneCacheItem() { Position = position, Zone = foundZone };
            if (foundZone == lastZone)
                return false;
            return true;
        }

        internal ZoneInfoInternal FindClosestZone(Vector3D position)
        {
            ZoneInfoInternal tmpZone = null;
            ZoneInfoInternal foundZone = null;
            bool zoneFound = false;
            double distance;
            double foundDistance = 0;
            double foundRadius = 0;

            //Log.Msg($"FindClosestZone() position={position} Zones.Count={Zones.Count}");
            for (int i = 0; i < Zones.Count; i++)
            {
                tmpZone = Zones[i];

                if (tmpZone.AlertRadius == 0)
                    continue;
                //Log.Msg($"tmpZone {tmpZone.UniqueName} position={tmpZone.Position} Type={tmpZone.Type}");
                distance = Vector3D.DistanceSquared(position, tmpZone.Position);
                if (zoneFound) //already found a zone, look for more
                {
                    if (foundRadius != tmpZone.AlertRadiusSqrd) //not same size as zone, return zone
                        return foundZone;

                    if (distance > tmpZone.AlertRadiusSqrd || distance >= foundDistance) //It is not closer, look for another
                        continue;

                    foundDistance = distance;
                    foundZone = tmpZone;
                }
                else
                {
                    if (distance < tmpZone.AlertRadiusSqrd) // in the first zone
                    {
                        zoneFound = true;
                        foundZone = tmpZone;
                        foundDistance = distance;
                        foundRadius = tmpZone.AlertRadiusSqrd;
                    }
                }
            }
            if (zoneFound) // catch the last found zone
                return foundZone;

            return null;
        }

    }
}
