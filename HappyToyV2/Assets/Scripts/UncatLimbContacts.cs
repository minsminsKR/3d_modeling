using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Exact imported-weight anatomical masks. No rig/importer mutation and no
    // extra frame bake: V1MonsterMotion uses these in its existing vertex pass.
    internal sealed class UncatLimbContacts
    {
        readonly bool[][] masks;
        public bool[] ForSkin(int index) => masks[index];
        public UncatLimbContacts(SkinnedMeshRenderer[] skins)
        {
            masks=new bool[skins.Length][];int total=0;
            for(int index=0;index<skins.Length;index++)
            {
                var skin=skins[index];var mesh=skin.sharedMesh;
                var counts=mesh.GetBonesPerVertex();var weights=mesh.GetAllBoneWeights();
                if(counts.Length!=mesh.vertexCount||weights.Length==0)
                    throw new InvalidOperationException("Uncat lacks imported contact weights");
                var groups=new byte[skin.bones.Length];
                for(int bone=0;bone<groups.Length;bone++)groups[bone]=Group(skin.bones[bone].name);
                var mask=new bool[mesh.vertexCount];int cursor=0;
                for(int vertex=0;vertex<mesh.vertexCount;vertex++)
                {
                    float leftFoot=0,rightFoot=0,leftHand=0,rightHand=0;
                    for(int influence=0;influence<counts[vertex];influence++)
                    {
                        var weight=weights[cursor++];
                        if(weight.boneIndex<0||weight.boneIndex>=groups.Length)
                            throw new InvalidOperationException("Uncat contact bone index is invalid");
                        switch(groups[weight.boneIndex])
                        {case 1:leftFoot+=weight.weight;break;case 2:rightFoot+=weight.weight;break;
                            case 4:leftHand+=weight.weight;break;case 8:rightHand+=weight.weight;break;}
                    }
                    // A weak influence on an unrelated body/tail vertex is not
                    // a hand/foot plant. Keep each anatomical group's majority.
                    mask[vertex]=leftFoot>=.5f||rightFoot>=.5f||leftHand>=.5f||rightHand>=.5f;
                    if(mask[vertex])total++;
                }
                if(cursor!=weights.Length)throw new InvalidOperationException("Uncat contact vertex order is invalid");
                masks[index]=mask;
                // These NativeArrays are borrowed Mesh-owned streams (Allocator.None).
                // Only the managed mask is retained; the streams are never disposed.
            }
            if(total==0)throw new InvalidOperationException("Uncat has no weighted hand/foot surface");
        }
        static byte Group(string name)
        {
            if(name.StartsWith("mixamorig:LeftFoot",StringComparison.Ordinal)||name.StartsWith("mixamorig:LeftToe",StringComparison.Ordinal))return 1;
            if(name.StartsWith("mixamorig:RightFoot",StringComparison.Ordinal)||name.StartsWith("mixamorig:RightToe",StringComparison.Ordinal))return 2;
            if(name.StartsWith("mixamorig:LeftHand",StringComparison.Ordinal))return 4;
            if(name.StartsWith("mixamorig:RightHand",StringComparison.Ordinal))return 8;
            return 0;
        }
    }
}
