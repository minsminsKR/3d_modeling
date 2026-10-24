using UnityEngine;

namespace HappyToy.V2
{
    public sealed partial class ChapterCheckpoint
    {
        public static ChapterCheckpoint FromJson(string json)
        {
            var data=JsonUtility.FromJson<ChapterCheckpoint>(json);
            if(data!=null)
            {
                if(CheckpointJsonFields.AbsentOrNull(json,"runProgress")) data.runProgress=null;
                if(CheckpointJsonFields.AbsentOrNull(json,"traps")) data.traps=null;
                if(CheckpointJsonFields.AbsentOrNull(json,"loop")) data.loop=null;
                if(data.loop!=null&&CheckpointJsonFields.AbsentOrNullInObject(json,"loop","walker"))data.loop.walker=null;
                data.Validate();
            }
            return data;
        }
        public ChapterCheckpoint Copy() => FromJson(ToJson());
        public string ToJson()
        {
            string json=JsonUtility.ToJson(this,true);
            if(runProgress==null) json=CheckpointJsonFields.Omit(json,"runProgress");
            if(traps==null) json=CheckpointJsonFields.Omit(json,"traps");
            if(loop==null) json=CheckpointJsonFields.Omit(json,"loop");
            else if(loop.walker==null)json=CheckpointJsonFields.OmitInObject(json,"loop","walker");
            return json;
        }
    }
}
