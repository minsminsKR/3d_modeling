using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HappyToy.V2
{
    // Finished school results never share a file with corridor records or saves.
    public sealed class ChapterRecords
    {
        public const int RecentCapacity=8;
        const int ReceiptCapacity=64, MaximumAttempts=1000000;
        [Serializable] public sealed class Summary
        {
            public int version=1, attempts, escapes, bestRecovered;
            public float bestEscapeSeconds;
            public ChapterRunReport[] recent=Array.Empty<ChapterRunReport>();
            public string[] receipts=Array.Empty<string>();
        }
        readonly string path;
        readonly bool allowWrite;
        Summary state;
        bool protectedProfile, preserveBadBackup;
        public bool PersistenceEnabled => allowWrite;
        public bool Writable => allowWrite && !protectedProfile && state.attempts<MaximumAttempts;
        public string Status { get; private set; }="";
        public Summary Snapshot => JsonUtility.FromJson<Summary>(JsonUtility.ToJson(state));
        public ChapterRecords(string directory,bool write)
        {
            path=Path.Combine(Path.GetFullPath(directory),"school-records-v1.json"); allowWrite=write;
            var primary=Read(path,out bool primaryDamaged); var backup=Read(path+".bak",out bool backupDamaged);
            state=primary??backup??new Summary();
            protectedProfile=primaryDamaged || primary==null && backupDamaged;
            preserveBadBackup=primary!=null && backupDamaged;
            if(protectedProfile) Status="학교 탐색 기록을 안전하게 읽을 수 없어 기존 파일을 보존했습니다.";
            else if(state.attempts>=MaximumAttempts) Status="학교 탐색 기록의 저장 한도에 도달했습니다.";
            else if(preserveBadBackup) Status="학교 기록은 정상입니다. 읽을 수 없는 백업은 별도로 보존합니다.";
            else if(primary==null && backup!=null) Status="이전 학교 탐색 기록을 복구했습니다.";
        }
        Summary Read(string file,out bool damaged)
        {
            damaged=false;
            try
            {
                if(!File.Exists(file)) return null;
                if(new FileInfo(file).Length>128*1024) throw new ArgumentException("Oversized school records");
                var data=JsonUtility.FromJson<Summary>(File.ReadAllText(file)); Validate(data); return data;
            }
            catch(Exception error) when(error is ArgumentException || error is IOException || error is UnauthorizedAccessException)
            { damaged=true; return null; }
        }
        static void Validate(Summary data)
        {
            if(data==null || data.version!=1 || data.attempts<1 || data.attempts>MaximumAttempts || data.escapes<0 || data.escapes>data.attempts ||
                data.bestRecovered<0 || data.bestRecovered>5 || !CorridorCheckpoint.Number(data.bestEscapeSeconds,0,1000000000) ||
                (data.escapes==0?data.bestEscapeSeconds!=0:data.bestRecovered!=5) ||
                data.recent==null || data.recent.Length!=Math.Min(data.attempts,RecentCapacity) ||
                data.receipts==null || data.receipts.Length!=Math.Min(data.attempts,ReceiptCapacity))
                throw new ArgumentException("Invalid school record summary");
            var receipts=new HashSet<string>(); var recent=new HashSet<string>();
            foreach(var token in data.receipts)
                if(!Guid.TryParseExact(token,"N",out _) || !receipts.Add(token)) throw new ArgumentException("Invalid school receipts");
            foreach(var run in data.recent)
            {
                if(run==null) throw new ArgumentException("Missing school run"); run.Validate();
                if(!receipts.Contains(run.token) || !recent.Add(run.token) || run.recovered>data.bestRecovered ||
                    run.escaped && (data.escapes==0 || run.seconds<data.bestEscapeSeconds)) throw new ArgumentException("School totals disagree with recent runs");
            }
            if(data.recent.Count(x=>x.escaped)>data.escapes) throw new ArgumentException("Invalid school escape total");
        }
        public bool Record(ChapterRunReport report)
        {
            if(!Writable) return false;
            try { if(report==null) throw new ArgumentException("Missing result"); report.Validate(); }
            catch(ArgumentException) { Status="학교 탐색 결과가 올바르지 않아 기록하지 않았습니다."; return false; }
            string temporary=path+".tmp-"+Guid.NewGuid().ToString("N");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using(var lease=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))
                {
                    // Read current totals under the shared lease; stale game windows
                    // merge their completed run rather than overwriting another result.
                    var fresh=new ChapterRecords(Path.GetDirectoryName(path),true);
                    if(!fresh.Writable) { state=fresh.state; protectedProfile=fresh.protectedProfile; preserveBadBackup=fresh.preserveBadBackup; Status=fresh.Status; return false; }
                    if(fresh.state.receipts.Contains(report.token))
                    { state=fresh.state; Status="이번 학교 탐색 결과는 이미 기록되어 있습니다."; return true; }
                    var next=fresh.Snapshot; next.attempts++; next.bestRecovered=Math.Max(next.bestRecovered,report.recovered);
                    if(report.escaped)
                    {
                        if(next.escapes==0 || report.seconds<next.bestEscapeSeconds) next.bestEscapeSeconds=report.seconds;
                        next.escapes++;
                    }
                    next.recent=new[]{report.Copy()}.Concat(next.recent).Take(RecentCapacity).ToArray();
                    next.receipts=new[]{report.token}.Concat(next.receipts).Take(ReceiptCapacity).ToArray(); Validate(next);
                    using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))
                    using(var writer=new StreamWriter(stream))
                    { writer.Write(JsonUtility.ToJson(next,true)); writer.Flush(); stream.Flush(true); }
                    bool archived=false;
                    if(File.Exists(path) && fresh.preserveBadBackup && File.Exists(path+".bak"))
                    {
                        // A verified primary remains writable. Preserve unknown backup
                        // bytes by atomic rename, then create a fresh normal backup of
                        // the verified primary. Never overwrite or read oversized bytes.
                        File.Move(path+".bak",path+".bak-preserved-"+Guid.NewGuid().ToString("N")); archived=true;
                    }
                    if(File.Exists(path)) File.Replace(temporary,path,path+".bak"); else File.Move(temporary,path);
                    state=next; preserveBadBackup=false;
                    Status=archived?"학교 결과를 기록했습니다. 읽을 수 없는 백업은 별도로 보존했습니다.":"이번 학교 탐색 결과를 기록했습니다."; return true;
                }
            }
            catch(Exception error) when(error is IOException || error is UnauthorizedAccessException || error is NotSupportedException)
            { Status="학교 결과를 기기에 기록하지 못했습니다.\n다시 저장하거나 이 화면에서 결과를 확인하세요."; return false; }
            finally
            {
                try { if(File.Exists(temporary)) File.Delete(temporary); }
                catch(Exception error) when(error is IOException || error is UnauthorizedAccessException) { }
            }
        }
    }
}
