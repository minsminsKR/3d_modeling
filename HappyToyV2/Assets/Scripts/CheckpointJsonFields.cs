using System;
using UnityEngine;

namespace HappyToy.V2
{
    // JsonUtility materializes omitted inline classes. Preserve root-field
    // presence at the JSON boundary, without accepting malformed present data.
    internal static class CheckpointJsonFields
    {
        [Serializable] sealed class PropertyName { public string name; }
        static int White(string json,int at) { while(at<json.Length && char.IsWhiteSpace(json[at])) at++; return at; }
        static int StringEnd(string json,int start)
        {
            for(int at=start+1;at<json.Length;at++)
            {
                if(json[at]=='\\') { at++; continue; }
                if(json[at]=='"') return at+1;
            }
            throw new ArgumentException("Unterminated checkpoint JSON string");
        }
        static int ValueEnd(string json,int start)
        {
            if(start>=json.Length) throw new ArgumentException("Missing checkpoint JSON value");
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
            throw new ArgumentException("Unterminated checkpoint JSON object");
        }
        static bool Field(string json,string field,out int fieldStart,out int valueStart,out int valueEnd)
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
                        if(name==field)
                        {
                            if(found) throw new ArgumentException("Duplicate checkpoint field "+field);
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
        internal static bool AbsentOrNull(string json,string field) =>
            !Field(json,field,out _,out int start,out int end) || json.Substring(start,end-start).Trim()=="null";
        internal static bool AbsentOrNullInObject(string json,string parent,string field)
        {
            if(!Field(json,parent,out _,out int start,out int end))return true;
            string value=json.Substring(start,end-start).Trim();
            if(value=="null")return true;
            if(value.Length==0||value[0]!='{')throw new ArgumentException("Checkpoint parent is not an object: "+parent);
            return AbsentOrNull(value,field);
        }
        internal static string OmitInObject(string json,string parent,string field)
        {
            if(!Field(json,parent,out _,out int start,out int end))return json;
            string value=json.Substring(start,end-start).Trim();
            if(value=="null")return json;
            if(value.Length==0||value[0]!='{')throw new ArgumentException("Checkpoint parent is not an object: "+parent);
            return json.Substring(0,start)+Omit(value,field)+json.Substring(end);
        }
        internal static string Omit(string json,string field)
        {
            if(!Field(json,field,out int start,out _,out int end)) return json;
            int after=White(json,end);
            if(after<json.Length && json[after]==',') return json.Remove(start,after+1-start);
            int before=start-1; while(before>=0 && char.IsWhiteSpace(json[before])) before--;
            return before>=0 && json[before]==',' ? json.Remove(before,end-before) : json.Remove(start,end-start);
        }
    }
}
