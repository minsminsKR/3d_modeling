using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    /// <summary>Chapter-local Korean school signage. Existing room identity, collision and door geometry remain authoritative.</summary>
    [DisallowMultipleComponent]
    public sealed class SchoolWayfinding : MonoBehaviour
    {
        sealed class LabelState { public TextMesh label; public string text; public Color color; public Vector3 scale; }
        sealed class BackingState { public MeshRenderer renderer; public Material original; }
        sealed class Fit { public TextMesh label; public Vector2 limit; }
        readonly List<LabelState> originalLabels = new List<LabelState>();
        readonly List<BackingState> originalBackings = new List<BackingState>();
        readonly List<Fit> fitted = new List<Fit>();
        readonly List<Object> owned = new List<Object>();
        Transform additions;
        Material enamel, paper, trim;
        int fitFrames;
        bool prepared;
        public int LocalizedRoomSigns { get; private set; }
        public int MountedNotices { get; private set; }
        public Transform AdditionRoot => additions;

        public void Prepare()
        {
            if (prepared) return;
            prepared = true;
            additions = new GameObject("School Korean wayfinding and notices").transform;
            additions.SetParent(transform, false);
            enamel = Surface("Aged school enamel", new Color(.105f,.15f,.125f), .16f);
            paper = Surface("Faded school notice paper", new Color(.60f,.56f,.43f), .03f);
            trim = Surface("Worn sign edge", new Color(.25f,.24f,.18f), .12f);
            var grain = PaperGrain(); owned.Add(grain);
            enamel.SetTexture("_BaseMap",grain);paper.SetTexture("_BaseMap",grain);
            var labels = FindObjectsByType<TextMesh>(FindObjectsInactive.Include)
                .Where(label=>label.gameObject.scene==gameObject.scene).ToArray();
            foreach (var label in labels)
            {
                string translated = label.name.StartsWith("CLASSROOM sign") ? "1학년 2반" :
                    label.name.StartsWith("WASHROOM sign") ? "화장실" :
                    label.name.StartsWith("INFIRMARY sign") ? "보건실" : null;
                if (translated == null) continue;
                originalLabels.Add(new LabelState {label=label,text=label.text,color=label.color,scale=label.transform.localScale});
                label.text=translated;label.color=new Color(.70f,.73f,.64f);
                var font=label.GetComponent<AnnexSignFont>();if(!font)font=label.gameObject.AddComponent<AnnexSignFont>();font.Apply();
                fitted.Add(new Fit {label=label,limit=new Vector2(1.37f,.21f)});LocalizedRoomSigns++;
            }
            foreach(var backing in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include)
                .Where(renderer=>renderer.gameObject.scene==gameObject.scene && renderer.name.EndsWith("sign opaque backing")))
            {
                originalBackings.Add(new BackingState {renderer=backing,original=backing.sharedMaterial});
                backing.sharedMaterial=enamel;
            }
            // Wall-mounted orientation marks follow existing stairs/rooms, never an invented passage.
            Plaque("Ground school directions",new Vector3(1.2f,2.12f,1.445f),0,new Vector2(1.08f,.43f),
                "본관 1층\n별관 · 계단 →",false);
            Plaque("Upper floor identity",new Vector3(25.8f,7.37f,33.85f),0,new Vector2(1.35f,.40f),
                "2층  액자·기록실",false);
            Plaque("Basement floor identity",new Vector3(13.8f,-2.45f,-33.85f),180,new Vector2(1.45f,.48f),
                "지하  인형보관실\n누수 구간 · 전원 차단",false);
            Plaque("School facility notice",new Vector3(1.18f,1.42f,1.447f),0,new Vector2(.72f,.69f),
                "시설 점검 안내\n\n계단 손잡이를 이용할 것\n누수 구간 통행 주의\n\n점검일  ____ / ____",true);
            Plaque("Upper school archive notice",new Vector3(23.445f,6.67f,23.8f),-90,new Vector2(.66f,.72f),
                "교내 기록 보관\n\n출석부 · 합창 명단\n반출 후 제자리에\n\n확인자  __________",true);
            Plaque("Basement water notice",new Vector3(7.56f,-3.17f,-25.3f),-90,new Vector2(.67f,.62f),
                "누수 점검중\n\n수도 · 전원 차단\n보관품을 옮기지 말 것\n\n시설 관리",true);
            fitFrames=4;
        }
        Material Surface(string name,Color color,float smoothness)
        {
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")) {name=name,enableInstancing=true};
            material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",smoothness);owned.Add(material);return material;
        }
        static Texture2D PaperGrain()
        {
            const int width=128,height=64;
            var texture=new Texture2D(width,height,TextureFormat.RGB24,true) {name="Original mottled school sign grain",
                wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new Color[width*height];
            for(int y=0;y<height;y++) for(int x=0;x<width;x++)
            {
                float fine=Mathf.PerlinNoise(x*.39f+2.1f,y*.39f+17.4f), damp=Mathf.PerlinNoise(x*.058f+11,y*.058f+9);
                float edge=Mathf.Clamp01(Mathf.Min(x,width-1-x,y,height-1-y)/8f);
                float value=.63f+.18f*fine+.15f*damp-.14f*(1-edge);
                pixels[y*width+x]=new Color(value,value*.985f,value*.94f);
            }
            texture.SetPixels(pixels);texture.Apply(true,true);return texture;
        }
        void Plaque(string name,Vector3 position,float yaw,Vector2 size,string text,bool notice)
        {
            var root=new GameObject(name).transform;root.SetParent(additions,false);root.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,notice?-1.8f:0));
            var panel=GameObject.CreatePrimitive(PrimitiveType.Cube);panel.name=notice?"Pinned aged paper":"School enamel plaque";
            panel.transform.SetParent(root,false);panel.transform.localScale=new Vector3(size.x,size.y,.015f);
            var collision=panel.GetComponent<Collider>();collision.enabled=false;Destroy(collision);
            panel.GetComponent<MeshRenderer>().sharedMaterial=notice?paper:enamel;
            panel.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            // Small printed rules and fixings give the notice its school stationery scale.
            foreach(float side in new[]{-1f,1f})
            {
                var fixing=GameObject.CreatePrimitive(PrimitiveType.Sphere);fixing.name=notice?"Paper thumbtack":"Plaque fixing";
                fixing.transform.SetParent(root,false);fixing.transform.localPosition=new Vector3(side*(size.x*.5f-.035f),size.y*.5f-.032f,-.012f);
                fixing.transform.localScale=new Vector3(.011f,.011f,.006f);var collider=fixing.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
                fixing.GetComponent<Renderer>().sharedMaterial=trim;
            }
            var label=new GameObject("Korean " + name).AddComponent<TextMesh>();label.transform.SetParent(root,false);
            label.transform.localPosition=new Vector3(0,0,-.011f);label.text=text;label.anchor=TextAnchor.MiddleCenter;
            label.alignment=TextAlignment.Center;label.fontSize=64;label.characterSize=.045f;
            label.color=notice?new Color(.16f,.19f,.15f):new Color(.66f,.70f,.60f);
            label.gameObject.AddComponent<AnnexSignFont>().Apply();
            fitted.Add(new Fit {label=label,limit=size-new Vector2(.11f,.085f)});MountedNotices++;
        }
        void FontRebuilt(Font font) { if(fitted.Any(item=>item.label && item.label.font==font)) fitFrames=4; }
        void OnEnable() {Font.textureRebuilt-=FontRebuilt;Font.textureRebuilt+=FontRebuilt;}
        void OnDisable() {Font.textureRebuilt-=FontRebuilt;}
        void LateUpdate()
        {
            if(fitFrames<=0) return;fitFrames--;
            foreach(var item in fitted)
            {
                if(!item.label) continue;
                var renderer=item.label.GetComponent<MeshRenderer>();var size=renderer.localBounds.size;
                if(size.x<.001f || size.y<.001f) {fitFrames=Mathf.Max(fitFrames,1);continue;}
                float scale=Mathf.Min(1,item.limit.x/size.x,item.limit.y/size.y);
                item.label.transform.localScale=Vector3.one*scale;
            }
        }
        void OnDestroy()
        {
            Font.textureRebuilt-=FontRebuilt;
            foreach(var state in originalLabels) if(state.label)
            {state.label.text=state.text;state.label.color=state.color;state.label.transform.localScale=state.scale;}
            foreach(var state in originalBackings) if(state.renderer && state.renderer.sharedMaterial==enamel)
                state.renderer.sharedMaterial=state.original;
            if(additions) Destroy(additions.gameObject);
            foreach(var resource in owned) if(resource) Destroy(resource);
            originalLabels.Clear();originalBackings.Clear();fitted.Clear();owned.Clear();
        }
    }
}
