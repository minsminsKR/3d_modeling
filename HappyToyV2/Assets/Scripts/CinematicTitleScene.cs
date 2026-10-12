using System;
using System.Collections.Generic;
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
        AnimationClip pose;
        Shader titleShader;
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
            Image = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                { name = "Baeksugwi red corridor title", antiAliasing = 1 };
            Image.Create();
            set = new GameObject("Owned title corridor set");
            set.transform.position = new Vector3(0, -3000, 0);
            var wall = Material(new Color(.62f, .38f, .28f), "GraphicsPbr/plaster-damp/albedo");
            var floor = Material(new Color(.45f, .29f, .22f), "Corridor/aged-floor-v2");
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
            if (!pose) throw new InvalidOperationException("Missing real Baeksugwi pose");
            pose.SampleAnimation(creature, .18f);
            Bounds bounds = default; bool first = true;
            foreach (var skin in creature.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.updateWhenOffscreen = true;
                if (first) { bounds = skin.bounds; first = false; } else bounds.Encapsulate(skin.bounds);
                var copies = new Material[skin.sharedMaterials.Length];
                for (int i = 0; i < copies.Length; i++)
                {
                    copies[i] = new Material(titleShader); materials.Add(copies[i]);
                    copies[i].SetTexture("_BaseMap", skin.sharedMaterials[i].GetTexture("_BaseMap"));
                    copies[i].SetFloat("_Creature", 1);
                    copies[i].SetVector("_StageOrigin", set.transform.position);
                }
                skin.sharedMaterials = copies;
            }
            if (first || bounds.size.x < .01f) throw new InvalidOperationException("Empty title creature geometry");
            float scale = 7.1f / bounds.size.x;
            pivot.transform.localScale = Vector3.one * scale;
            pivot.transform.localRotation = Quaternion.Euler(0, 180, 0);
            Vector3 offset = Quaternion.Euler(0, 180, 0) * new Vector3(bounds.center.x, bounds.min.y - set.transform.position.y, bounds.center.z) * scale;
            pivot.transform.localPosition = new Vector3(2.65f, .03f, 1.8f) - offset;
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
            if (!visible || !Image || !camera || Time.frameCount < 3 || Time.unscaledTime < nextFrame) return;
            float phase = shell && shell.ReducedMotion ? .18f : .18f + Mathf.Sin(Time.unscaledTime * .18f) * .035f;
            pose.SampleAnimation(creature, phase);
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = Image });
            nextFrame = Time.unscaledTime + (shell && shell.ReducedMotion ? 1 : 1f / 15);
        }
        void OnDestroy()
        {
            if (set) Destroy(set);
            foreach (var material in materials) if (material) Destroy(material);
            if (Image) { Image.Release(); Destroy(Image); }
        }
    }
}
