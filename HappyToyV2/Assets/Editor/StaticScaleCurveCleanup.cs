using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // A scale can leave a clip only when it is the same across every action.
    // The complete native first pose owns those values in the prefab.
    public static class StaticScaleCurveCleanup
    {
        public static HashSet<string> InvariantBindings(AnimationClip[] clips)
        {
            var result=new HashSet<string>();
            if(clips.Length==0)return result;
            foreach(var binding in AnimationUtility.GetCurveBindings(clips[0]))
            {
                if(!binding.propertyName.StartsWith("m_LocalScale.",StringComparison.Ordinal))continue;
                var reference=AnimationUtility.GetEditorCurve(clips[0],binding);
                if(reference==null||reference.length==0)continue;
                float value=reference.keys[0].value;
                if(clips.All(clip=>{
                    var curve=AnimationUtility.GetEditorCurve(clip,binding);
                    return curve!=null&&curve.length>0&&curve.keys.All(key=>Mathf.Abs(key.value-value)<.00001f);
                }))result.Add(Key(binding));
            }
            return result;
        }
        public static string Key(EditorCurveBinding binding)=>binding.path+"\n"+binding.propertyName;
    }
}
