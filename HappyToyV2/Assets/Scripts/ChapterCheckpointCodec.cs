using System;
using UnityEngine;

namespace HappyToy.V2
{
    public sealed partial class ChapterCheckpoint
    {
        // JsonUtility materializes absent nested serializable classes as empty
        // objects. Inspect the actual root field, so missing old metrics are
        // distinguished from explicitly supplied malformed metrics.
        [Serializable] sealed class PropertyName { public string name; }
        static int White(string json,int at) { while(at<json.Length && char.IsWhiteSpace(json[at])) at++; return at; }
        static int StringEnd(string json,int start)
        {
            for(int at=start+1;at<json.Length;at++)
            {
                if(json[at]=='\\') { at++; continue; }
                if(json[at]=='"') return at+1;
            }
            throw new ArgumentException("Unterminated school JSON string");
        }
        static int ValueEnd(string json,int start)
        {
            if(start>=json.Length) throw new ArgumentException("Missing school JSON value");
            if(json[start]=='"') return StringEnd(json,start);
            if(json[start]!='{' && json[start]!='[')
            {
                int at=start; while(at<json.Length && json[at]!=',' && json[at]!='}') at++; return at;
            }
            int depth=0;
            for(int at=start;at<json.Length;at++)
            {
                if(json[at]=='"') { at=StringEnd(json,at)-1; continue; }
                if(json[at]=='{' || json[at]=='[') depth++;
                else if(json[at]=='}' || json[at]==']') { if(--depth==0) return at+1; }
            }
            throw new ArgumentException("Unterminated school JSON object");
        }
        static bool MetricsField(string json,out int fieldStart,out int valueStart,out int valueEnd)
        {
            fieldStart=valueStart=valueEnd=-1; int depth=0; bool found=false;
            for(int at=0;at<json.Length;at++)
            {
                char c=json[at];
                if(c=='"')
                {
                    int end=StringEnd(json,at),next=White(json,end);
                    if(depth==1 && next<json.Length && json[next]==':')
                    {
                        string rawName=json.Substring(at,end-at);
                        string name=JsonUtility.FromJson<PropertyName>("{\"name\":"+rawName+"}").name;
                        if(name=="runProgress")
                        {
                            if(found) throw new ArgumentException("Duplicate school metrics field");
                            found=true; fieldStart=at; valueStart=White(json,next+1); valueEnd=ValueEnd(json,valueStart);
                            at=valueEnd-1; continue;
                        }
                    }
                    at=end-1;
                }
                else if(c=='{' || c=='[') depth++;
                else if(c=='}' || c==']') depth--;
            }
            return found;
        }
        public static ChapterCheckpoint FromJson(string json)
        {
            var data=JsonUtility.FromJson<ChapterCheckpoint>(json);
            if(data!=null)
            {
                if(!MetricsField(json,out _,out int start,out int end) || json.Substring(start,end-start).Trim()=="null") data.runProgress=null;
                data.Validate();
            }
            return data;
        }
        public ChapterCheckpoint Copy()
        {
            var copy=JsonUtility.FromJson<ChapterCheckpoint>(JsonUtility.ToJson(this));
            copy.runProgress=runProgress?.Copy(); return copy;
        }
        public string ToJson()
        {
            string json=JsonUtility.ToJson(this,true);
            if(runProgress!=null || !MetricsField(json,out int start,out _,out int end)) return json;
            int after=White(json,end);
            if(after<json.Length && json[after]==',') return json.Remove(start,after+1-start);
            int before=start-1; while(before>=0 && char.IsWhiteSpace(json[before])) before--;
            return before>=0 && json[before]==',' ? json.Remove(before,end-before) : json.Remove(start,end-start);
        }
    }
}
