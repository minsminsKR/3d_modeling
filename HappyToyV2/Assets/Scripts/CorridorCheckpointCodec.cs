using UnityEngine;

namespace HappyToy.V2
{
    public sealed partial class CorridorCheckpoint
    {
        public static CorridorCheckpoint FromJson(string json)
        {
            var data=JsonUtility.FromJson<CorridorCheckpoint>(json);
            if(data!=null)
            {
                if(CheckpointJsonFields.AbsentOrNull(json,"traps")) data.traps=null;
                data.Validate();
            }
            return data;
        }
        public CorridorCheckpoint Copy() => FromJson(ToJson());
        public string ToJson()
        {
            string json=JsonUtility.ToJson(this,true);
            return traps==null ? CheckpointJsonFields.Omit(json,"traps") : json;
        }
    }
}
