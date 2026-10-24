using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest,Timeout(120000)]
        public IEnumerator StudioDistantSkinsKeepTheOriginalEyeMaskAndFollowTheSameEmissionToggle()
        {
            Call(session,"ConfigureRecordDirectory",NewRecordFixture());
            Call(session,"CreateCorridor",73);Begin();Call(session,"ToggleCheatSafeMode");
            for(int i=0;i<3;i++)Assert.That((bool)Call(session,"CheatCollectNextMemory"),Is.True);
            yield return Delay(.2f);
            var anchor=Components("StalkerBrain").Single(actor=>actor.name.EndsWith("— corridor")&&actor.name.Contains("Cyclopse")).transform.position;
            foreach(string key in new[]{"Baby","Uncat","Cyclopse"})
            {
                var actor=StudioSubject(key,anchor);
                actor.gameObject.SetActive(true);yield return null;
                var model=Get<Transform>(actor.GetComponent(RequireType("V1MonsterMotion")),"model");
                var eyes=model.GetComponentsInChildren(RequireType("MonsterRedEyes"),true).Cast<Component>().Single();
                var mounted=Get<IReadOnlyList<Renderer>>(eyes,"EmissionRenderers");
                var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Assert.That(skins.Length,Is.EqualTo(key=="Uncat"?1:2));
                Assert.That(Get<IReadOnlyList<Transform>>(eyes,"EyeAnchors").Count,Is.EqualTo(key=="Cyclopse"?1:2),"LOD must not duplicate anatomy anchors");
                try
                {
                    foreach(bool enabled in new[]{true,false,true})
                    {
                        Call(eyes,"SetEmissionEnabled",enabled);
                        foreach(var skin in skins)
                        {
                            Assert.That(mounted.Contains(skin),Is.True,key+" distant eye was not mounted");
                            var slots=(int[])Call(eyes,"GetAffectedSlots",skin);Assert.That(slots,Is.Not.Empty);
                            foreach(int slot in slots)
                            {
                                Assert.That(skin.sharedMaterials[slot].GetTexture("_EmissionMap"),Is.SameAs(Resources.Load<Texture2D>("EnemyStudio/"+key+"/eyes-emission")));
                                var block=new MaterialPropertyBlock();skin.GetPropertyBlock(block,slot);
                                float value=block.GetColor("_EmissionColor").maxColorComponent;
                                if(enabled)Assert.That(value,Is.GreaterThan(.1f));else Assert.That(value,Is.Zero);
                            }
                        }
                    }
                }
                finally{Call(eyes,"SetEmissionEnabled",true);}
            }
        }
    }
}
