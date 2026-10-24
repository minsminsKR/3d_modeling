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
        public string RoomId {get;private set;}
        public Transform Visual {get;private set;}
        public static SchoolRoomSign Build(SchoolLoopWorld world,Transform section,SchoolLoopPlan.Room room)
        {
            if(!world||!section)throw new ArgumentNullException("School sign requires its world and repeated section");
            var sign=new GameObject("Projecting Korean school room sign — "+room.Id).AddComponent<SchoolRoomSign>();
            sign.RoomId=room.Id;sign.transform.SetParent(section,false);
            // The -X mounting plate meets the projecting window-side pilaster's
            // east face. The sign bottom clears the physical door header by 5cm.
            sign.transform.localPosition=new Vector3(-1.675f,2.72f,room.DoorZ+.73f);
            sign.Visual=SchoolRenewalModels.Attach("room-sign",sign.transform,
                new Bounds(Vector3.zero,new Vector3(.66f,.34f,.08f)));
            foreach(float side in new[]{-1f,1f})
            {
                var face=new GameObject(side<0?"Approaching room name face":"Returning room name face").transform;
                // Measured finished broad faces lie at about +/-3.83cm after
                // fitting. Keep ink ~2.7mm above them, centred on the panel
                // rather than the left-hand mounting bracket's whole envelope.
                face.SetParent(sign.transform,false);face.localPosition=new Vector3(.06f,0,side*.041f);
                face.localRotation=Quaternion.Euler(0,side<0?0:180,0);
                world.Label(face,room.Label,Vector3.zero,Quaternion.identity,new Vector2(.50f,.22f),new Color(.045f,.057f,.052f));
                var renderer=face.GetComponentInChildren<MeshRenderer>();
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
            foreach(var child in sign.GetComponentsInChildren<Transform>(true))child.gameObject.layer=8;
            return sign;
        }
    }
}
