using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    // V1 MirrorHwacatEvent.snapModelToGround, using the rendered skin rather than bone pivots.
    // This event is a grounded stand/dance, not a jump. Only the visual child moves vertically.
    public sealed class V1RevealGrounding:MonoBehaviour
    {
        public Transform model;
        Vector3 restPosition;
        SkinnedMeshRenderer[] skins;
        Mesh sampledMesh;
        readonly List<Vector3> vertices=new List<Vector3>();
        void LateUpdate()
        {
            if(!model||Time.timeScale==0)return;
            if(!sampledMesh)
            {
                restPosition=model.localPosition;skins=model.GetComponentsInChildren<SkinnedMeshRenderer>();
                sampledMesh=new Mesh{name="Hwacat reveal ground sample"};
            }
            model.localPosition=restPosition;
            if(!Physics.Raycast(transform.position+Vector3.up*.25f,Vector3.down,out var floor,.8f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))return;
            float sole=float.MaxValue;
            foreach(var skin in skins)
            {
                skin.BakeMesh(sampledMesh,true);sampledMesh.GetVertices(vertices);
                foreach(var vertex in vertices)sole=Mathf.Min(sole,skin.transform.TransformPoint(vertex).y);
            }
            if(sole!=float.MaxValue)model.position+=Vector3.up*Mathf.Clamp(floor.point.y-sole,-.5f,.5f);
        }
        void OnDestroy(){if(sampledMesh)Destroy(sampledMesh);}
    }
}
