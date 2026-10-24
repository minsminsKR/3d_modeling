using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // A generated, wall-perpendicular enamel sign identifies each locked room
    // from either corridor approach. Only the exact Korean lettering is native
    // artwork; the casing, bracket and worn finish belong to the authored model.
    [DisallowMultipleComponent]
    public sealed class SchoolRoomSign : MonoBehaviour
    {
        readonly TextMesh[] names=new TextMesh[2];
        readonly Renderer[] ink=new Renderer[2];
        public string RoomId {get;private set;}
        public Transform Visual {get;private set;}
        public static SchoolRoomSign Build(SchoolLoopWorld world,Transform section,SchoolLoopPlan.Room room)
        {
            if(!world||!section)throw new ArgumentNullException("School sign requires its world and repeated section");
            var sign=new GameObject("Projecting Korean school room sign — "+room.Id).AddComponent<SchoolRoomSign>();
            sign.RoomId=room.Id;sign.transform.SetParent(section,false);
            // The sign bottom clears the physical door header by 5cm.
            sign.transform.localPosition=new Vector3(-1.675f,2.72f,room.DoorZ+.73f);
            sign.Visual=SchoolRenewalModels.Attach("room-sign",sign.transform,
                new Bounds(Vector3.zero,new Vector3(.66f,.34f,.08f)));
            int index=0;
            foreach(float side in new[]{-1f,1f})
            {
                var face=new GameObject(side<0?"Approaching room name face":"Returning room name face").transform;
                // Measured finished broad faces lie at about +/-3.83cm after
                // fitting. Keep ink ~2.7mm above them, centred on the panel
                // rather than the left-hand mounting bracket's whole envelope.
                // Imported Unity mesh/UV measurements put the ivory panel at
                // X=-6cm. The +6cm Blender offset is reversed by FBX conversion.
                face.SetParent(sign.transform,false);face.localPosition=new Vector3(-.06f,-.004f,side*.041f);
                face.localRotation=Quaternion.Euler(0,side<0?0:180,0);
                var text=new GameObject("Mounted Korean room lettering").AddComponent<TextMesh>();
                text.transform.SetParent(face,false);text.text=room.Label;text.fontSize=80;text.characterSize=.035f;
                text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=new Color(.045f,.057f,.052f);
                text.gameObject.AddComponent<AnnexSignFont>().Apply();
                var renderer=text.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                sign.names[index]=text;sign.ink[index]=renderer;index++;
            }
            sign.FitNames();
            foreach(var child in sign.GetComponentsInChildren<Transform>(true))child.gameObject.layer=8;
            return sign;
        }
        // Text layout must also settle during loading and pause. Corridor travel
        // gates must not leave the native font's initial-size mesh on the panel.
        // Native mesh bounds can change after the atlas callback, or without one
        // when a requested glyph size is already cached. Check just 18 bounds;
        // unchanged labels do not dirty transforms or request font atlas work.
        void LateUpdate(){FitNames();}
        void FitNames()
        {
            for(int index=0;index<names.Length;index++)
            {
                var text=names[index];if(!text||!ink[index])continue;
                var bounds=ink[index].localBounds;
                if(bounds.size.x<.001f||bounds.size.y<.001f)continue;
                // Common height keeps the two-character shop name consistent
                // with three-character names; the width cap preserves side margins.
                float scale=Mathf.Min(.40f/bounds.size.x,.18f/bounds.size.y);
                var size=Vector3.one*scale;
                var centre=new Vector3(-bounds.center.x*scale,-bounds.center.y*scale,0);
                if(text.transform.localScale!=size)text.transform.localScale=size;
                if(text.transform.localPosition!=centre)text.transform.localPosition=centre;
            }
        }
    }
}
