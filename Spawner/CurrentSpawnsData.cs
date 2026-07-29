using ProtoBuf;
using System.Collections.Generic;
using VRageMath;

namespace ZoneControl.Spawner
{
    public enum SpawnType
    {
        Wreck,
        Pirate
    }

    [ProtoContract]
    public class SpawnInfo
    {
        [ProtoMember(1)]
        public string Name = "";
        [ProtoMember(2)]
        public Vector3D Position = Vector3D.MaxValue;
        [ProtoMember(3)]
        public long RemoveAt = 0; //system DateTime ticks
        [ProtoMember(4)]
        public Vector3D SubZonePosition = Vector3D.MaxValue;
        [ProtoMember(5)]
        public long EntityId = 0;
        [ProtoMember(6)]
        public int ZoneId = -1;
        [ProtoMember(7)]
        public long AnomalyId = 0;
        [ProtoMember(8)]
        public string PrefabName = "";
        [ProtoMember(9)]
        public int GroupId = -1;
        [ProtoMember(10)]
        public SpawnType Type = SpawnType.Wreck;
        [ProtoMember(11)]
        public string PirateAntenna = "";
        [ProtoMember(12)]
        public bool PiratesEnabled = false;

        public SpawnInfo() { }
    }

    [ProtoContract]
    public class CurrentSpawnsData
    {
        [ProtoMember(1)]
        public List<SpawnInfo> Spawns = new List<SpawnInfo>();
        [ProtoMember(2)]
        public int SpawnCounter = 0;

        public CurrentSpawnsData() { }

        public bool HasGroupId(int groupId)
        {
            if (groupId < 0)
                return false;

            for (int i = 0; i < Spawns.Count; i++)
            {
                if (Spawns[i].GroupId == groupId)
                    return true;
            }
            return false;
        }

        public SpawnInfo FindZoneId(int zoneId)
        {
            if (zoneId < 0)
                return null;

            for (int i = 0; i < Spawns.Count; i++)
            {
                if (Spawns[i].ZoneId == zoneId)
                    return Spawns[i];
            }
            return null;
        }

        public int Count(SpawnType type)
        {
            int count = 0;
            for (int i = 0; i < Spawns.Count; i++)
            {
                if (type == Spawns[i].Type) count++;
            }
            return count;
        }

    }

}
