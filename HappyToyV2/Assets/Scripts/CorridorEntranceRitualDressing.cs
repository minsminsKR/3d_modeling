using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Fixed ritual art, lights and wet accents. No interaction, collision, pickup
    // or AI state. Only this owner's batches and cloned materials are released.
    public sealed class CorridorEntranceRitualDressing : MonoBehaviour
    {
        readonly GraphicsSurfaceLibrary.Pool surfaces=new GraphicsSurfaceLibrary.Pool();
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly List<Light> lanterns=new List<Light>();
        readonly List<Vector3> incenseSources=new List<Vector3>();
        readonly List<Transform> toBatch=new List<Transform>();
        public IReadOnlyList<Light> Lanterns=>lanterns;
        // Gain for the firelight rig. The old ceiling-fill rig has been retired.
        public float LightMultiplier=>.45f;
        public IReadOnlyList<Vector3> IncenseSources=>incenseSources;
        public int CandleCount {get;private set;}
        public int SealCount {get;private set;}
        public int GuardianCount {get;private set;}
        public int VisualBatches {get;private set;}
        public bool UsesPendingGuardianFallback {get;private set;}
        public int OwnedResourceCount=>owned.Count+surfaces.MaterialCount+surfaces.MeshCount;
        Material rope,seal,ink,sealInk,wet;
        Mesh sharedSealInk;

        public static CorridorEntranceRitualDressing Attach(Transform parent)
        {
            var root=CorridorEntranceModels.Group(parent,"Owned entrance ritual dressing",Vector3.zero);
            var result=root.gameObject.AddComponent<CorridorEntranceRitualDressing>();result.Build();return result;
        }
        Transform Group(string name,Vector3 at)=>CorridorEntranceModels.Group(transform,name,at);
        void Build()
        {
            rope=surfaces.Get("cloth-charred",new Color(.23f,.20f,.15f));
            ink=surfaces.Get("painted-metal",new Color(.14f,.025f,.015f));
            sealInk=new Material(surfaces.Get("cloth-charred",new Color(.045f,.027f,.017f))) {name="Owned curved talisman ink"};
            seal=new Material(surfaces.Get("paper-aged",new Color(.78f,.65f,.43f))) {name="Owned aged talisman paper"};
            owned.Add(seal);owned.Add(sealInk);
            Ropes();Guardians();OnwardPassage();Candles();LanternsAndLight();Incense();GroundMarks();WetAccents();BatchStaticDetails();
        }
        void OnwardPassage()
        {
            // Reuse authored Korean props and timber joinery. All dressing stays
            // outside the original capsule opening and owns no collision.
            foreach(int side in new[]{-1,1})
            {
                ReusedProp("KoreanCorridorProps/jangseung","Onward passage carved jangseung",
                    new Vector3(side*4.1f,1.55f,10.6f),new Vector3(.80f,3.1f,.66f),Quaternion.Euler(0,180-side*12,side*2));
                ReusedProp("KoreanCorridorProps/onggi","Abandoned passage offering jar",
                    new Vector3(side*3.15f,.32f,12.4f),new Vector3(.60f,.64f,.60f),Quaternion.Euler(0,side*17,0));
                ReusedProp("CorridorArchitecture/door-post-lod0","Smoke darkened cave threshold post",
                    new Vector3(side*2.33f,1.95f,14.65f),new Vector3(.23f,3.9f,.30f),Quaternion.identity,true);
            }
            ReusedProp("CorridorArchitecture/door-lintel-lod0","Heavy dark cave threshold lintel",
                new Vector3(0,3.75f,14.65f),new Vector3(5.05f,.42f,.44f),Quaternion.identity,true);
            // Cover the bright flat fascia at the maze mouth with retained dark
            // joinery; preserve the original 2.58m physical opening below it.
            ReusedProp("CorridorArchitecture/door-lintel-lod0","Receding smoke stained passage lintel",
                new Vector3(0,2.72f,18.72f),new Vector3(2.94f,.56f,.36f),Quaternion.identity,true);
            SaggingRope(new Vector3(-5.5f,4.2f,10.8f),new Vector3(5.5f,4,11.5f),.65f,15);
            SaggingRope(new Vector3(-2.3f,3.6f,14.3f),new Vector3(2.3f,3.55f,14.3f),.25f,7);
            CandleCluster(new Vector3(-2.9f,0,8.4f),7);CandleCluster(new Vector3(3.1f,0,11.2f),6);
        }
        void ReusedProp(string key,string name,Vector3 centre,Vector3 size,Quaternion rotation,bool timber=false)
        {
            var source=Resources.Load<GameObject>(key);
            if(!source)throw new InvalidOperationException("Retained passage dressing missing: "+key);
            var holder=Group(name,Vector3.zero);var model=Instantiate(source,holder,false).transform;
            model.localRotation=rotation;
            GraphicsPropLibrary.Fit(holder,new Bounds(centre,size));
            foreach(var child in holder.GetComponentsInChildren<Transform>(true))child.gameObject.layer=8;
            if(holder.GetComponentsInChildren<Collider>(true).Length!=0)throw new InvalidOperationException("Passage art must not own physics");
            if(timber)foreach(var renderer in holder.GetComponentsInChildren<MeshRenderer>(true))
            {
                var slots=renderer.sharedMaterials;
                for(int i=0;i<slots.Length;i++)slots[i]=surfaces.Get("wood-aged",new Color(.19f,.115f,.067f));
                renderer.sharedMaterials=slots;
            }
            toBatch.Add(holder);
        }
        void Guardians()
        {
            foreach(int side in new[]{-1,1})
            {
                var statue=CorridorEntranceModels.Pending("guardian-statue",transform,"Weathered stone sanctuary guardian",Vector3.zero,out bool fallback);
                UsesPendingGuardianFallback|=fallback;
                CorridorEntranceModels.GroundAtHeight(statue,new Vector3(side*10.1f,.012f,-10.6f),3.5f,Quaternion.Euler(0,-side*16,0));
                GuardianCount++;
            }
        }
        void Candles()
        {
            // Four pools mark the sides of the route, never its walking centre.
            CandleCluster(new Vector3(-3.4f,0,-7.2f),7,true);CandleCluster(new Vector3(3.4f,0,-6.6f),6,true);
            CandleCluster(new Vector3(-3.6f,0,1.2f),6,true);CandleCluster(new Vector3(3.3f,0,8.4f),6,true);
            CandleCluster(new Vector3(-10,0,-10.5f),6);CandleCluster(new Vector3(10,0,-10.5f),6);
            CandleCluster(new Vector3(-10,0,-3),4);CandleCluster(new Vector3(10,0,3),4);
        }
        void CandleCluster(Vector3 centre,int count,bool illuminate=false)
        {
            var cluster=Group("Offerings of gathered beeswax candles",centre);
            for(int i=0;i<count;i++)
            {
                float angle=i*2.399963f,spread=.14f+Mathf.Sqrt(i)*.17f,height=.12f+(i%4)*.065f;
                var at=centre+new Vector3(Mathf.Cos(angle)*spread,.026f,Mathf.Sin(angle)*spread);
                var holder=Group("Gathered worn ritual candle",at);holder.SetParent(cluster,true);
                var candle=GraphicsPropLibrary.Attach("candle-waymark",holder,surfaces.Resolve);
                GraphicsPropLibrary.Fit(candle,new Bounds(new Vector3(0,height*.5f,0),new Vector3(.085f,height,.085f)));
                var flame=GraphicsPropLibrary.CreateFlame(holder,owned);
                GraphicsPropLibrary.Fit(flame,new Bounds(new Vector3(0,height+.043f,0),new Vector3(.032f,.105f,.032f)));
                toBatch.Add(holder);CandleCount++;
            }
            if(illuminate)Lamp("Candle gathering firelight",centre+Vector3.up*.29f,new Color(1,.43f,.13f),6.8f,4.7f,false,cluster);
        }
        void Ropes()
        {
            SaggingRope(new Vector3(-12,6.8f,-5),new Vector3(12,6.3f,-7),1.5f,17);
            SaggingRope(new Vector3(-12,8.8f,-11),new Vector3(12,8.2f,-12),1.2f,17);
            SaggingRope(new Vector3(-12,7,4),new Vector3(-8,4.8f,-11),.8f,11);
        }
        static Vector3 Sag(Vector3 a,Vector3 b,float depth,float t)=>Vector3.Lerp(a,b,t)-Vector3.up*(4*depth*t*(1-t));
        void SaggingRope(Vector3 a,Vector3 b,float depth,int papers)
        {
            const int steps=36;var geometry=new GraphicsSurfaceLibrary.Geometry(Vector3.zero,.15f);
            for(int i=0;i<steps;i++)Tube(geometry,Sag(a,b,depth,i/(float)steps),Sag(a,b,depth,(i+1f)/steps),.017f);
            MeshPart("Sagging weathered ritual cord",geometry.Mesh("Owned sagging ritual cord"),rope);
            for(int i=0;i<papers;i++)
            {
                var holder=Group("Torn inked paper seal",Sag(a,b,depth,(i+.5f)/papers));
                holder.localRotation=Quaternion.Euler((i%3-1)*7,(i%5-2)*8,(i%7-3)*3);
                holder.localScale=new Vector3(.17f+(i%3)*.035f,.65f+(i%5)*.13f,1);
                var paper=CorridorDetailLibrary.Attach("hanging-seal",holder,slot=>slot=="CD_paper"?seal:slot=="CD_ink"?ink:rope,1);
                if(!sharedSealInk){sharedSealInk=CorridorEntranceSealInk.Create(paper,holder);owned.Add(sharedSealInk);}
                var marks=new GameObject("Curved brush ink on hanging paper");marks.transform.SetParent(holder,false);
                marks.AddComponent<MeshFilter>().sharedMesh=sharedSealInk;
                var renderer=marks.AddComponent<MeshRenderer>();renderer.sharedMaterial=sealInk;
                renderer.shadowCastingMode=ShadowCastingMode.Off;
                toBatch.Add(holder);SealCount++;
            }
        }
        static void Tube(GraphicsSurfaceLibrary.Geometry geometry,Vector3 a,Vector3 b,float radius)
        {
            var direction=(b-a).normalized;var across=Vector3.Cross(direction,Vector3.up).normalized;
            if(across.sqrMagnitude<.01f)across=Vector3.right;
            var other=Vector3.Cross(direction,across).normalized;
            for(int n=0;n<6;n++)
            {
                float u=n*Mathf.PI/3,v=(n+1)*Mathf.PI/3;
                var p=(across*Mathf.Cos(u)+other*Mathf.Sin(u))*radius;
                var q=(across*Mathf.Cos(v)+other*Mathf.Sin(v))*radius;geometry.Quad(a+p,b+p,b+q,a+q);
            }
        }
        void LanternsAndLight()
        {
            // Every light belongs to a visible flame or a candle-lit paper shade.
            // Their short ranges leave the high vault and gaps between pools dark.
            Lantern(new Vector3(3.8f,2.05f,-4.8f),1,8.5f,7,true,1.05f);
            Lantern(new Vector3(-4.4f,2.1f,6.8f),1,7,6,false,.95f);
            Lantern(new Vector3(2.8f,1.9f,11.3f),1,6.5f,5.5f,false,1.15f);
            Lantern(new Vector3(-7.8f,2.2f,-11.2f),1,8,6,true,1.05f);
            Lantern(new Vector3(7.8f,2.4f,-11.2f),1,7.2f,6,false,1.05f);
        }
        void Lantern(Vector3 at,float scale,float intensity,float range,bool shadow,float fittedHeight=0)
        {
            var holder=Group("Old inked temple paper lantern",at);holder.localScale=Vector3.one*scale;
            var paper=surfaces.Get("paper-aged",new Color(.48f,.32f,.17f),new Color(.22f,.09f,.025f));
            var lantern=GraphicsPropLibrary.Attach("paper-lantern",holder,slot=>slot=="GU_washi"?paper:surfaces.Resolve(slot));
            lantern.localRotation=Quaternion.Euler(0,at.x<0?14:-12,at.x<0?-4:3);
            var bounds=GraphicsPropLibrary.LocalBounds(holder);
            if(fittedHeight>0)holder.localScale=Vector3.one*(fittedHeight/Mathf.Max(.001f,bounds.size.y));
            float top=bounds.max.y*holder.localScale.y;
            if(fittedHeight>0)BuddhistLanternInk(holder,bounds);
            foreach(var renderer in holder.GetComponentsInChildren<MeshRenderer>(true))renderer.shadowCastingMode=ShadowCastingMode.Off;
            toBatch.Add(holder);var cord=new GraphicsSurfaceLibrary.Geometry(Vector3.zero,.15f);
            Tube(cord,at+Vector3.up*top,new Vector3(at.x,11.9f,at.z),.012f);
            MeshPart("Old lantern suspension cord",cord.Mesh("Owned lantern suspension"),rope);
            var fire=Group("Candle flame inside the Buddhist lantern",Vector3.zero);fire.SetParent(holder,false);
            fire.localPosition=bounds.center;
            var flame=GraphicsPropLibrary.CreateFlame(fire,owned);
            GraphicsPropLibrary.Fit(flame,new Bounds(Vector3.zero,new Vector3(.05f,.13f,.05f)/holder.localScale.x));
            var sourceAt=transform.InverseTransformPoint(fire.position);
            Lamp("Candle-lit Buddhist paper lantern",sourceAt,new Color(1,.54f,.22f),intensity,range,shadow,holder);
        }
        void BuddhistLanternInk(Transform holder,Bounds bounds)
        {
            var viewer=transform.TransformPoint(new Vector3(0,1.6f,-9.2f));
            var outward=Vector3.ProjectOnPlane(viewer-holder.position,Vector3.up).normalized;
            float radius=Mathf.Min(bounds.extents.x,bounds.extents.z)*holder.localScale.x;
            var glyph=Group("Weathered Buddhist lantern ink",Vector3.zero);
            glyph.SetParent(holder,false);
            glyph.position=holder.TransformPoint(bounds.center)+outward*(radius+.006f);
            glyph.rotation=Quaternion.LookRotation(-outward,Vector3.up);
            glyph.localScale=Vector3.one/holder.localScale.x;
            var text=glyph.gameObject.AddComponent<TextMesh>();
            text.text="佛";text.fontSize=80;text.characterSize=.065f;
            text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
            text.color=new Color(.055f,.025f,.012f);
            glyph.gameObject.AddComponent<AnnexSignFont>().Apply();
            var renderer=glyph.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        void Lamp(string name,Vector3 at,Color colour,float intensity,float range,bool shadow,Transform fixture)
        {
            var light=Group(name,at).gameObject.AddComponent<Light>();light.type=LightType.Point;
            light.transform.SetParent(fixture,true);
            light.color=colour;light.intensity=intensity*LightMultiplier;light.range=range;
            light.shadows=shadow?LightShadows.Soft:LightShadows.None;lanterns.Add(light);
            light.gameObject.AddComponent<EntranceFirelight>().Configure(light,fixture,at.x*.19f+at.z*.11f+21);
        }
        void Incense()
        {
            foreach(var at in new[]{new Vector3(-7.4f,.027f,-3.25f),new Vector3(5.2f,.027f,-10.7f)})
            {
                var holder=Group("Ritual incense vessel",at);var source=Resources.Load<GameObject>("KoreanCorridorProps/onggi");
                if(!source)throw new InvalidOperationException("Retained generated incense vessel missing");
                var vessel=Instantiate(source,holder,false).transform;
                GraphicsPropLibrary.Fit(vessel,new Bounds(new Vector3(0,.18f,0),new Vector3(.42f,.36f,.42f)));
                for(int i=0;i<3;i++)
                {
                    var stick=PlayerItemVisuals.Attach("incense",holder);
                    GraphicsPropLibrary.Fit(stick,new Bounds(new Vector3((i-1)*.035f,.48f,0),new Vector3(.014f,.43f,.014f)));
                    stick.localRotation=Quaternion.Euler(0,0,(i-1)*5);
                }
                foreach(var child in holder.GetComponentsInChildren<Transform>(true))child.gameObject.layer=8;
                toBatch.Add(holder);incenseSources.Add(at+Vector3.up*.70f);
            }
        }
        void GroundMarks()
        {
            var geometry=new GraphicsSurfaceLibrary.Geometry(Vector3.zero,.6f);var centre=new Vector3(0,.027f,-3);
            for(int ring=0;ring<2;ring++)for(int i=0;i<96;i++)
            {
                if((i+ring*11)%23>19)continue;
                float a=i*Mathf.PI*2/96,b=(i+1)*Mathf.PI*2/96,r=2.6f+ring*.30f;
                GroundRibbon(geometry,centre+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r),centre+new Vector3(Mathf.Cos(b)*r,0,Mathf.Sin(b)*r),.035f);
            }
            for(int petal=0;petal<8;petal++)for(int n=0;n<24;n++)
                GroundRibbon(geometry,Petal(centre,petal*Mathf.PI/4,n/24f),Petal(centre,petal*Mathf.PI/4,(n+1)/24f),.025f);
            MeshPart("Faded red lotus ritual trace",geometry.Mesh("Owned worn ritual floor ink"),ink,false);
        }
        static Vector3 Petal(Vector3 centre,float rotation,float t)
        {
            float r=.28f+2.1f*Mathf.Sin(t*Mathf.PI),angle=rotation+Mathf.Sin(t*Mathf.PI*2)*.21f;
            return centre+new Vector3(Mathf.Cos(angle)*r,0,Mathf.Sin(angle)*r);
        }
        static void GroundRibbon(GraphicsSurfaceLibrary.Geometry g,Vector3 a,Vector3 b,float halfWidth)
        {
            var side=Vector3.Cross((b-a).normalized,Vector3.up)*halfWidth;g.Quad(a+side,b+side,b-side,a-side);
        }
        void WetAccents()
        {
            var template=Resources.Load<Material>("CorridorEntrance/ritual-floor/CE_ritual-floor_0");
            if(!template&&CorridorEntranceModels.PendingAssetFallback)template=Resources.Load<Material>("CorridorEntrance/cave-rock/CE_cave-rock_0");
            if(!template)throw new InvalidOperationException("Generated wet floor material missing");
            wet=new Material(template){name="Owned shallow cave water sheen"};
            var packed=new Texture2D(1,1,TextureFormat.RGBA32,false,true){name="Owned nonmetal wet roughness",wrapMode=TextureWrapMode.Repeat};
            packed.SetPixel(0,0,new Color(0,0,0,.91f));packed.Apply(false,true);owned.Add(packed);owned.Add(wet);
            wet.SetColor("_BaseColor",new Color(.13f,.12f,.105f));wet.SetTexture("_MetallicGlossMap",packed);wet.SetFloat("_Smoothness",1);wet.SetFloat("_Metallic",1);
            var geometry=new GraphicsSurfaceLibrary.Geometry(Vector3.zero,2);
            for(int p=0;p<6;p++)
            {
                var c=new Vector3(p%2==0?-4.7f:4.9f,.028f,-8.8f+p*2.1f);float rx=1.35f+(p%3)*.18f,rz=.65f+(p%2)*.2f;
                for(int i=0;i<24;i++)
                {
                    float a=i*Mathf.PI*2/24,b=(i+1)*Mathf.PI*2/24;
                    float ra=1+.12f*Mathf.Sin(i*2.17f+p),rb=1+.12f*Mathf.Sin((i+1)*2.17f+p);
                    var q=c+new Vector3(Mathf.Cos(a)*rx*ra,0,Mathf.Sin(a)*rz*ra);
                    var r=c+new Vector3(Mathf.Cos(b)*rx*rb,0,Mathf.Sin(b)*rz*rb);
                    geometry.Quad(Vector3.Lerp(c,q,.001f),Vector3.Lerp(c,r,.001f),r,q);
                }
            }
            MeshPart("Small wet cave floor catchlights",geometry.Mesh("Owned shallow irregular water patches"),wet,false);
        }
        void MeshPart(string name,Mesh mesh,Material material,bool shadow=true)
        {
            owned.Add(mesh);var root=Group(name,Vector3.zero);root.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=root.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=shadow?ShadowCastingMode.On:ShadowCastingMode.Off;toBatch.Add(root);
        }
        void BatchStaticDetails()
        {
            // Four spatial zones retain local culling; generated floor/guardian
            // LOD groups remain independent. This runs once, never in Update.
            var batches=new Dictionary<(Material,bool,int),List<CombineInstance>>();
            foreach(var root in toBatch)foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer=filter.GetComponent<MeshRenderer>();if(!renderer||!filter.sharedMesh)continue;
                if(!filter.sharedMesh.isReadable)throw new InvalidOperationException("Ritual detail requires readable mesh: "+filter.name);
                var materials=renderer.sharedMaterials;var mesh=filter.sharedMesh;
                if(materials.Length!=mesh.subMeshCount)throw new InvalidOperationException("Ritual detail material count mismatch");
                var local=transform.InverseTransformPoint(renderer.bounds.center);int zone=(local.x<0?0:1)+(local.z< -5?0:2);
                for(int slot=0;slot<materials.Length;slot++)
                {
                    var key=(materials[slot],renderer.shadowCastingMode!=ShadowCastingMode.Off,zone);
                    if(!batches.TryGetValue(key,out var list)){list=new List<CombineInstance>();batches.Add(key,list);}
                    list.Add(new CombineInstance {mesh=mesh,subMeshIndex=slot,transform=transform.worldToLocalMatrix*filter.transform.localToWorldMatrix});
                }
                renderer.enabled=false;
            }
            foreach(var entry in batches)
            {
                var mesh=new Mesh {name="Owned ritual detail zone "+entry.Key.Item3,indexFormat=IndexFormat.UInt32};
                mesh.CombineMeshes(entry.Value.ToArray(),true,true);mesh.RecalculateBounds();owned.Add(mesh);
                var root=Group("Batched ritual art zone "+entry.Key.Item3,Vector3.zero);
                root.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=root.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial=entry.Key.Item1;renderer.shadowCastingMode=entry.Key.Item2?ShadowCastingMode.On:ShadowCastingMode.Off;
                renderer.receiveShadows=true;renderer.motionVectorGenerationMode=MotionVectorGenerationMode.ForceNoMotion;VisualBatches++;
            }
            toBatch.Clear();
        }
        void OnDestroy(){surfaces.Dispose();foreach(var resource in owned)GraphicsSurfaceLibrary.DestroyOwned(resource);owned.Clear();}
    }
}
