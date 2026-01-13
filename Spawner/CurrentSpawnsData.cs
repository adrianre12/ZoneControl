using ProtoBuf;
using System.Collections.Generic;
using VRageMath;

namespace ZoneControl.Spawner
{
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
        /*
        [ProtoMember(8)]
        public string DPname = "";
        [ProtoMember(9)]
        public string DPdata = "";*/


        public SpawnInfo() { }

        public SpawnInfo(SpawnInfo spawnInfo)
        {
            Name = spawnInfo.Name;
            Position = new Vector3D(spawnInfo.Position);
            RemoveAt = spawnInfo.RemoveAt;
            SubZonePosition = new Vector3D(spawnInfo.SubZonePosition);
            EntityId = spawnInfo.EntityId;
            ZoneId = spawnInfo.ZoneId;
            AnomalyId = spawnInfo.AnomalyId;
            //DPname = spawnInfo.DPname;
            //DPdata = spawnInfo.DPdata;

        }
    }

    [ProtoContract]
    public class CurrentSpawnsData
    {
        [ProtoMember(1)]
        public List<SpawnInfo> Spawns = new List<SpawnInfo>();
        [ProtoMember(2)]
        public int SpawnCounter = 0;

        public CurrentSpawnsData() { }

    }

}
