using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    // Isolated, visual-only title set. It reuses the shipped Baeksugwi prefab
    // and owns every temporary object/material/target; no gameplay actor is moved.
    public sealed class CinematicTitleScene : MonoBehaviour
    {
        public const string CreatureResource = "Baeksugwi/Baeksugwi";
        const int Layer = 29;
        readonly List<Material> materials = new List<Material>();
        GameObject set, creature;
        Camera camera;
        AnimationClip pose,headMotion;
        float headClock;
        public float HeadMotionTime=>headClock;
        public AnimationClip HeadMotionClip=>headMotion;
        Shader titleShader;
        Transform faceJoint;
        SkinnedMeshRenderer[] skins;
        readonly List<Material> creatureMaterials = new List<Material>();
        Material floorMaterial;
        List<Support> plantedSupports;
        public float MaximumSupportFloorGap { get; private set; }
        const float FloorHeight = -.005f;
        // Raycast against the original mask eye slits (TitlePoseReview.Inspect), in Head-local metres.
        public static readonly Vector3 LeftEyeSurface = new Vector3(.047255f, -.044846f, -.038783f);
        public static readonly Vector3 RightEyeSurface = new Vector3(-.020251f, -.044857f, -.042935f);
        public int PlantedLimbCount { get; private set; }
        public float SurfaceFloorGap => EnemyStudioRig.PosedBounds(skins).min.y - set.transform.position.y - FloorHeight;
        public bool FaceLightReady => faceJoint && creatureMaterials.Count > 0;
        public bool EyesReady => FaceLightReady && creatureMaterials.All(m => m.GetFloat("_EyeGlow") > 0);
        GameShell shell;
        float nextFrame;
        bool visible;
        public RenderTexture Image { get; private set; }
        public bool UsesBaeksugwi => creature && creature.GetComponentsInChildren<SkinnedMeshRenderer>().Length > 0;

        public void Initialize(GameShell owner)
        {
            shell = owner;
            titleShader = Resources.Load<Shader>("TitleAtmosphere");
            if (!titleShader || !titleShader.isSupported) throw new InvalidOperationException("Missing or unsupported title atmosphere shader");
            Image = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                { name = "Baeksugwi red corridor title", antiAliasing = 1 };
            Image.Create();
            set = new GameObject("Owned title corridor set");
            set.transform.position = new Vector3(0, -3000, 0);
            var wall = Material(new Color(.62f, .38f, .28f), "GraphicsPbr/plaster-damp/albedo");
            var floor = floorMaterial = Material(new Color(.45f, .29f, .22f), "Corridor/aged-floor-v2");
            var iron = Material(new Color(.24f, .16f, .12f), "GraphicsPbr/metal-rust/albedo");
            Box("floor", new Vector3(0, -.13f, 3), new Vector3(13, .25f, 20), floor);
            Box("left wall", new Vector3(-4.1f, 2, 3), new Vector3(.25f, 4, 20), wall);
            Box("right wall", new Vector3(4.5f, 2, 3), new Vector3(.25f, 4, 20), wall);
            Box("ceiling", new Vector3(0, 4.05f, 3), new Vector3(9, .25f, 20), iron);
            for (int i = 0; i < 6; i++)
            {
                float z = -3 + i * 2.7f;
                Box("wall column", new Vector3(-3.92f, 1.95f, z), new Vector3(.26f, 3.9f, .21f), iron);
                Box("wall column", new Vector3(4.3f, 1.95f, z), new Vector3(.26f, 3.9f, .21f), iron);
                Box("ceiling rib", new Vector3(.1f, 3.85f, z), new Vector3(8.3f, .23f, .2f), iron);
            }
            Box("door left jamb", new Vector3(-.48f, 1.9f, 3.4f), new Vector3(.34f, 3.8f, .55f), iron);
            Box("door right jamb", new Vector3(3.64f, 1.9f, 3.4f), new Vector3(.34f, 3.8f, .55f), iron);
            Box("door lintel", new Vector3(1.58f, 3.8f, 3.4f), new Vector3(4.46f, .26f, .55f), iron);
            Box("dark doorway wall", new Vector3(-2.5f, 1.95f, 3.5f), new Vector3(3.75f, 3.9f, .3f), wall);
            Box("rear wall", new Vector3(0, 2, 11.5f), new Vector3(9, 4, .3f), wall);
            for (int i = 0; i < 8; i++)
                Box("floor debris", new Vector3(-2.7f + i * .36f, .04f, -1.8f + i * .14f), new Vector3(.17f, .055f, .4f), iron);
            var source = Resources.Load<GameObject>(CreatureResource);
            if (!source) throw new InvalidOperationException("Missing real title Baeksugwi prefab");
            var pivot = new GameObject("Baeksugwi title pose"); pivot.transform.SetParent(set.transform, false);
            creature = Instantiate(source, pivot.transform, false); creature.name = "Actual Baeksugwi source clone";
            foreach (var behaviour in creature.GetComponentsInChildren<Behaviour>(true)) behaviour.enabled = false;
            foreach (var collider in creature.GetComponentsInChildren<Collider>(true)) Destroy(collider);
            pose = Resources.Load<AnimationClip>("Baeksugwi/Run_InPlace");
            headMotion=Resources.Load<AnimationClip>("Baeksugwi/Head_Writhe");
            if (!pose || !headMotion) throw new InvalidOperationException("Missing real Baeksugwi pose/head action");
            // Hold a real planted gait frame instead of walking in place above the title floor.
            skins = creature.GetComponentsInChildren<SkinnedMeshRenderer>();
            PlantTitlePose();
            faceJoint = creature.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Head");
            Bounds bounds = EnemyStudioRig.PosedBounds(skins);
            foreach (var skin in skins)
            {
                skin.updateWhenOffscreen = true;
                var copies = new Material[skin.sharedMaterials.Length];
                for (int i = 0; i < copies.Length; i++)
                {
                    copies[i] = new Material(titleShader); materials.Add(copies[i]); creatureMaterials.Add(copies[i]);
                    copies[i].SetTexture("_BaseMap", skin.sharedMaterials[i].GetTexture("_BaseMap"));
                    copies[i].SetFloat("_Creature", 1);
                    copies[i].SetVector("_StageOrigin", set.transform.position);
                }
                skin.sharedMaterials = copies;
            }
            if (bounds.size.x < .01f) throw new InvalidOperationException("Empty title creature geometry");
            float scale = 4.8f / bounds.size.x;
            pivot.transform.localScale = Vector3.one * scale;
            pivot.transform.localRotation = Quaternion.Euler(0, 180, 0);
            bounds = EnemyStudioRig.PosedBounds(skins);
            // Renderer.bounds includes a conservative animation envelope, not the current foot surface.
            pivot.transform.position += new Vector3(2.0f - bounds.center.x,
                set.transform.position.y + FloorHeight + .003f - bounds.min.y,
                3.6f - bounds.center.z);
            UpdateContactShadows();
            UpdateFaceLighting();
            PaintLayer(set.transform);
            var eye = new GameObject("Title-only camera"); eye.transform.SetParent(set.transform, false);
            camera = eye.AddComponent<Camera>(); camera.enabled = false;
            camera.cullingMask = 1 << Layer; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.008f, .002f, .002f); camera.fieldOfView = 57;
            camera.nearClipPlane = .08f; camera.farClipPlane = 30; camera.aspect = 16f / 9f;
            camera.transform.localPosition = new Vector3(-1.2f, .72f, -4.9f);
            camera.transform.LookAt(set.transform.position + new Vector3(.35f, 1.25f, 4.1f));
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            set.SetActive(false);
        }
        Material Material(Color colour, string texture)
        {
            var value = new Material(titleShader); materials.Add(value);
            value.SetColor("_BaseColor", colour);
            var surface = Resources.Load<Texture2D>(texture);
            if (!surface) throw new InvalidOperationException("Missing title surface: " + texture);
            value.SetTexture("_BaseMap", surface);
            value.SetVector("_StageOrigin", set.transform.position); return value;
        }
        void Box(string name, Vector3 at, Vector3 size, Material material)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube); item.name = name;
            item.transform.SetParent(set.transform, false); item.transform.localPosition = at; item.transform.localScale = size;
            item.GetComponent<MeshRenderer>().sharedMaterial = material; Destroy(item.GetComponent<Collider>());
        }
        static void PaintLayer(Transform root) { root.gameObject.layer = Layer; foreach (Transform child in root) PaintLayer(child); }
        public void SetVisible(bool value) { visible = value; if (set) set.SetActive(value); if (value) nextFrame = 0; }
        void LateUpdate()
        {
            if (!visible || !Image || !camera || Time.frameCount < 3) return;
            // Title time is unscaled; the game intentionally pauses on this page.
            // Sampling only Head leaves every planted limb and body pose fixed.
            if(!shell || !shell.ReducedMotion)headClock+=Time.unscaledDeltaTime;
            if(Time.unscaledTime < nextFrame)return;
            headMotion.SampleAnimation(creature,shell && shell.ReducedMotion ? 0 : Mathf.Repeat(headClock,headMotion.length));
            UpdateFaceLighting();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = Image });
            nextFrame = Time.unscaledTime + (shell && shell.ReducedMotion ? 1 : 1f / 15);
        }
        struct JointPose
        {
            public Transform joint; public Vector3 position; public Quaternion rotation;
            public JointPose(Transform t) { joint=t; position=t.localPosition; rotation=t.localRotation; }
            public void Apply() { joint.localPosition=position; joint.localRotation=rotation; }
        }
        sealed class Support
        {
            public Transform shoulder; public Transform[] joints; public int[][] vertices;
            public JointPose[] planted; public float minimum=float.PositiveInfinity;
        }
        void PlantTitlePose()
        {
            var nodes=creature.GetComponentsInChildren<Transform>(true);
            var supports=plantedSupports=new List<Support>();
            for(int limb=1;limb<=6;limb++) foreach(string side in new[]{"L","R"})
            {
                string prefix="Arm"+limb+side;
                var joints=new[]{"Shoulder","Elbow","Wrist"}.Select(part=>nodes.Single(t=>t.name==prefix+"_"+part)).ToArray();
                var indices=new int[skins.Length][];
                for(int k=0;k<skins.Length;k++)
                {
                    int bone=Array.IndexOf(skins[k].bones,joints[2]);
                    indices[k]=skins[k].sharedMesh.boneWeights.Select((w,i)=>new {w,i}).Where(v=>
                        v.w.boneIndex0==bone && v.w.weight0>.5f || v.w.boneIndex1==bone && v.w.weight1>.5f ||
                        v.w.boneIndex2==bone && v.w.weight2>.5f || v.w.boneIndex3==bone && v.w.weight3>.5f).Select(v=>v.i).ToArray();
                }
                if(!indices.Any(v=>v.Length>0)) throw new InvalidOperationException("Missing title support surface: "+prefix);
                supports.Add(new Support{shoulder=joints[0],joints=joints,vertices=indices});
            }
            // Borrow each limb's planted portion of the original authored clip; no base mesh/skin edits.
            foreach(float phase in new[]{0f,.18f,.36f,.54f})
            {
                pose.SampleAnimation(creature,phase);
                var posed=skins.Select(EnemyStudioRig.PosedVertices).ToArray();
                foreach(var support in supports)
                {
                    float low=float.PositiveInfinity;
                    for(int k=0;k<skins.Length;k++)foreach(int index in support.vertices[k]) low=Mathf.Min(low,posed[k][index].y);
                    if(low<support.minimum) {support.minimum=low;support.planted=support.joints.Select(t=>new JointPose(t)).ToArray();}
                }
            }
            pose.SampleAnimation(creature,0);
            foreach(var support in supports)foreach(var joint in support.planted)joint.Apply();
            var points=skins.Select(EnemyStudioRig.PosedVertices).ToArray();
            // Freeze body sway, then compensate its tiny phase difference at the shoulder attachment.
            foreach(var support in supports)
            {
                float low=float.PositiveInfinity;
                for(int k=0;k<skins.Length;k++)foreach(int index in support.vertices[k])low=Mathf.Min(low,points[k][index].y);
                support.shoulder.position += Vector3.up*(set.transform.position.y-low);
            }
            points=skins.Select(EnemyStudioRig.PosedVertices).ToArray();
            foreach(var support in supports)
            {
                float low=float.PositiveInfinity;
                for(int k=0;k<skins.Length;k++)foreach(int index in support.vertices[k])low=Mathf.Min(low,points[k][index].y);
                if(Mathf.Abs(low-set.transform.position.y)<.003f)PlantedLimbCount++;
            }
            if(PlantedLimbCount!=12)throw new InvalidOperationException("Title stance did not plant all twelve support limbs");
        }
        void UpdateContactShadows()
        {
            var points=skins.Select(EnemyStudioRig.PosedVertices).ToArray();
            var contacts=new Vector4[plantedSupports.Count];
            for(int limb=0;limb<plantedSupports.Count;limb++)
            {
                var support=plantedSupports[limb];float low=float.PositiveInfinity;
                for(int k=0;k<skins.Length;k++)foreach(int index in support.vertices[k])low=Mathf.Min(low,points[k][index].y);
                MaximumSupportFloorGap=Mathf.Max(MaximumSupportFloorGap,Mathf.Abs(low-set.transform.position.y-FloorHeight));
                bool first=true;Bounds footprint=default;
                for(int k=0;k<skins.Length;k++)foreach(int index in support.vertices[k])if(points[k][index].y<low+.06f)
                {
                    if(first){footprint=new Bounds(points[k][index],Vector3.zero);first=false;}else footprint.Encapsulate(points[k][index]);
                }
                var centre=footprint.center-set.transform.position;
                contacts[limb]=new Vector4(centre.x,0,centre.z,Mathf.Clamp((footprint.size.x+footprint.size.z)*.45f,.12f,.32f));
            }
            if(MaximumSupportFloorGap>.008f)throw new InvalidOperationException("Scaled title support contact left the floor");
            floorMaterial.SetVectorArray("_FootContacts",contacts);
        }
        void UpdateFaceLighting()
        {
            if (!faceJoint) return;
            foreach (var material in creatureMaterials)
            {
                material.SetMatrix("_WorldToFace", faceJoint.worldToLocalMatrix);
                material.SetVector("_EyeLeft", LeftEyeSurface);
                material.SetVector("_EyeRight", RightEyeSurface);
                material.SetFloat("_FaceLight", .016f);
                material.SetFloat("_EyeGlow", shell && shell.ReducedMotion ? 1.25f : 1.25f + .04f * Mathf.Sin(Time.unscaledTime * .7f));
            }
        }
        void OnDestroy()
        {
            if (set) Destroy(set);
            foreach (var material in materials) if (material) Destroy(material);
            if (Image) { Image.Release(); Destroy(Image); }
        }
    }
}
