using System;
using System.IO;
using UnityEngine;

namespace HappyToy.V2
{
    // One suspended run. Consumed snapshots are never offered as backups.
    public sealed class CorridorCheckpointStore
    {
        readonly string path;
        readonly bool allowWrite;
        CorridorCheckpoint state;
        bool protectedFile;
        public bool HasRun => state != null;
        public bool Writable => allowWrite && !protectedFile;
        public bool Conflict { get; private set; }
        public string Status { get; private set; } = "";
        public CorridorCheckpoint Snapshot => state == null ? null : JsonUtility.FromJson<CorridorCheckpoint>(JsonUtility.ToJson(state));
        public CorridorCheckpointStore(string directory,bool write)
        {
            path=Path.Combine(Path.GetFullPath(directory),"corridor-suspend-v1.json"); allowWrite=write;
            try
            {
                if(!File.Exists(path)) return;
                if(new FileInfo(path).Length>128*1024) throw new ArgumentException("Oversized save");
                var data=JsonUtility.FromJson<CorridorCheckpoint>(File.ReadAllText(path)); data?.Validate();
                if(data==null) throw new ArgumentException("Empty save"); state=data;
            }
            catch(Exception error) when(error is ArgumentException || error is IOException || error is UnauthorizedAccessException)
            { protectedFile=true; Status="중단 기록을 안전하게 읽을 수 없어 보존했습니다. 새 탐색은 시작할 수 있습니다."; }
        }
        bool Same(CorridorCheckpointStore fresh) => !fresh.protectedFile && fresh.state?.token==state?.token;
        public bool Save(CorridorCheckpoint data)
        {
            if(!Writable) return false;
            if(data==null) { Status="올바르지 않은 탐색 상태는 저장하지 않았습니다."; return false; }
            try { data.Validate(); } catch(ArgumentException) { Status="올바르지 않은 탐색 상태는 저장하지 않았습니다."; return false; }
            string temporary=path+".tmp-"+Guid.NewGuid().ToString("N");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using(var lease=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
                {
                    var fresh=new CorridorCheckpointStore(Path.GetDirectoryName(path),true);
                    if(!Same(fresh)) { Status="다른 실행에서 중단 기록이 변경되어 저장하지 않았습니다."; return false; }
                    using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                    using(var writer=new StreamWriter(stream))
                    { writer.Write(JsonUtility.ToJson(data,true)); writer.Flush(); stream.Flush(true); }
                    if(File.Exists(path)) File.Replace(temporary,path,path+".bak"); else File.Move(temporary,path);
                    state=JsonUtility.FromJson<CorridorCheckpoint>(JsonUtility.ToJson(data));
                    Status="탐색을 중단 저장했습니다."; return true;
                }
            }
            catch(Exception error) when(error is IOException || error is UnauthorizedAccessException || error is NotSupportedException)
            { Status="중단 기록을 저장하지 못했습니다. 현재 탐색은 그대로 유지됩니다."; return false; }
            finally
            {
                try { if(File.Exists(temporary)) File.Delete(temporary); }
                catch(Exception error) when(error is IOException || error is UnauthorizedAccessException) { }
            }
        }
        public bool Consume(string token)
        {
            Conflict=false;
            if(!Writable || state==null || state.token!=token) return false;
            try
            {
                using(var lease=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
                {
                    var fresh=new CorridorCheckpointStore(Path.GetDirectoryName(path),true);
                    if(!Same(fresh)) { Conflict=true; Status="중단 기록이 다른 실행에서 변경되었습니다. 다시 시작 화면을 열어 주세요."; return false; }
                    // Atomic move out of the readable slot. Keep used bytes for recovery,
                    // but never fall back to .used/.bak and revive a consumed run.
                    if(File.Exists(path+".used")) File.Replace(path,path+".used",null);
                    else File.Move(path,path+".used");
                    state=null; Status="중단 기록을 이어받았습니다."; return true;
                }
            }
            catch(Exception error) when(error is IOException || error is UnauthorizedAccessException || error is NotSupportedException)
            { Status="중단 기록을 이어받지 못했습니다. 파일을 보존했습니다."; return false; }
        }
    }
}
