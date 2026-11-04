using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Owns the outdoor dressing, its local lights, smoke and recorded cues.
    // All global render settings it changes are restored before the next mode.
    [DisallowMultipleComponent]
    public sealed class TempleProloguePresentation : MonoBehaviour
    {
        readonly List<Material> materials = new List<Material>();
        readonly List<Texture2D> textures = new List<Texture2D>();
        readonly List<Light> lamps = new List<Light>();
        readonly List<Transform> lanterns = new List<Transform>();
        TemplePrologueWorld world;
        Camera camera;
        Transform incense, gate;
        IncenseSmokeTrail smoke;
        AudioSource cry, bell, air, gateSound;
        bool secondDay, captured;
        float pendingIncense = -1, nextBell;
        bool waitingForIncenseEquip;
        PlayerMotor incenseActor;
        bool oldFog;
        FogMode oldFogMode;
        Color oldFogColor, oldAmbient;
        float oldFogDensity, oldAmbientIntensity;
        AmbientMode oldAmbientMode;
        CameraClearFlags oldClearFlags;
        Color oldBackground;
        RoomAmbience schoolAmbience;
        bool oldAmbienceEnabled;
        public bool GateVisible => gate && gate.gameObject.activeSelf;
        public bool IncensePlaced => incense;
        public bool IncensePending => waitingForIncenseEquip || pendingIncense >= 0;

        public void Prepare(TemplePrologueWorld owner, Camera eye)
        {
            if (world || !owner || !eye) throw new ArgumentException("Prologue world and camera are required");
            var temple = Resources.Load<GameObject>("TemplePrologue/temple-visual");
            var pine = Resources.Load<GameObject>("TemplePrologue/pine-visual");
            var incensePrefab = Resources.Load<GameObject>("PlayerItems/incense");
            if (!temple || !pine || !incensePrefab || !Resources.Load<GameObject>("CorridorEntrance/temple-gate-visual"))
                throw new InvalidOperationException("The temple, pine, incense and gate prefabs are required");
            if (!ExternalAudio.Shared("enemy-baby-cry") || !ExternalAudio.Shared("story-bell") || !ExternalAudio.Shared("ambience-corridor") || !ExternalAudio.Shared("door-open"))
                throw new InvalidOperationException("The prologue's recorded ambience, bell and cry are required");
            var incenseAction = HandAnimationLibrary.Load().GetClip("UseIncense");
            if (incenseAction == null || incenseAction.releaseTime <= 0 || incenseAction.releaseTime >= 4)
                throw new InvalidOperationException("The authored incense hand action is required");
            world = owner; camera = eye;
            CaptureEnvironment();
            schoolAmbience = owner.GetComponentInParent<RoomAmbience>();
            if (schoolAmbience) { oldAmbienceEnabled = schoolAmbience.enabled; schoolAmbience.enabled = false; }

            var floorSource = Resources.Load<Material>("CorridorEntrance/ritual-floor/CE_ritual-floor_0");
            if (!floorSource) throw new InvalidOperationException("The existing stone surface is required");
            var earth = ForestEarth();
            var path = Surface(floorSource, "Old stone path", new Color(.57f, .53f, .46f), new Vector2(1, 8));
            var wood = Material("Prayer table", new Color(.16f, .095f, .055f));
            var bronze = Material("Incense vessel", new Color(.20f, .17f, .12f));
            Box("Forest ground", new Vector3(0, -.16f, -2), new Vector3(48, .3f, 64), earth, true);
            Box("Stone approach", new Vector3(0, .005f, -5), new Vector3(3.4f, .025f, 31), path, false);
            foreach (float side in new[] { -1f, 1f })
                Box("Forest boundary", new Vector3(side * 22, 3, -2), new Vector3(.3f, 6, 64), earth, true, false);
            Box("Forest end", new Vector3(0, 3, -27), new Vector3(44, 6, .3f), earth, true, false);
            Box("Temple boundary", new Vector3(0, 3, 29), new Vector3(44, 6, .3f), earth, true, false);
            var moon = new GameObject("Temple outdoor fill"); moon.transform.SetParent(transform, false);
            moon.transform.localRotation = Quaternion.Euler(48, -35, 0);
            var moonlight = moon.AddComponent<Light>(); moonlight.type = LightType.Directional;
            moonlight.color = new Color(.66f, .72f, .70f); moonlight.intensity = .75f; moonlight.shadows = LightShadows.None;
            var shrineFill = new GameObject("Temple facade fill"); shrineFill.transform.SetParent(transform, false);
            shrineFill.transform.localPosition = new Vector3(0, 4.2f, 10.7f);
            var facadeLight = shrineFill.AddComponent<Light>(); facadeLight.type = LightType.Point;
            facadeLight.color = new Color(1, .72f, .48f); facadeLight.range = 11; facadeLight.intensity = 2.6f; facadeLight.shadows = LightShadows.None;
            var shrine = CorridorEntranceModels.Group(transform, "Generated Korean temple", Vector3.zero);
            Instantiate(temple, shrine, false);
            shrine.GetChild(0).localRotation = Quaternion.identity;
            // The preserved FBX imports with the roof toward +X in the front
            // view. Correct the art axes, then ground by actual renderer bounds.
            CorridorEntranceModels.GroundAtHeight(shrine, new Vector3(0, 0, 18), 6f,
                Quaternion.Euler(0, 0, 90) * Quaternion.Euler(0, 90, 0));
            // The preserved model is an exterior; keep the accessible prayer space in front.
            Box("Temple front physical wall", new Vector3(0, 2, 13.8f), new Vector3(9, 4, .3f), wood, true, false);
            Box("Prayer table top", new Vector3(0, .77f, 7.35f), new Vector3(1.8f, .15f, .75f), wood, true);
            foreach (float side in new[] { -1f, 1f })
                Box("Prayer table leg", new Vector3(side * .72f, .36f, 7.35f), new Vector3(.12f, .72f, .55f), wood, false);
            var vessel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            vessel.name = "Incense vessel"; vessel.transform.SetParent(transform, false);
            vessel.transform.localPosition = new Vector3(0, .91f, 7.2f); vessel.transform.localScale = new Vector3(.42f, .11f, .42f);
            vessel.GetComponent<Renderer>().sharedMaterial = bronze; Destroy(vessel.GetComponent<Collider>());
            for (int i = 0; i < 16; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                int row = i / 2;
                var tree = Instantiate(pine, transform); tree.name = "Pine " + i;
                tree.transform.localPosition = new Vector3(side * (5.5f + (row % 3) * 2.3f), -.15f, -23 + row * 6.5f);
                tree.transform.localRotation = Quaternion.Euler(0, (i * 83) % 360, 0);
                tree.transform.localScale = Vector3.one * (.78f + (i % 4) * .11f);
                var trunk = tree.AddComponent<CapsuleCollider>(); trunk.center = new Vector3(0, 2, 0);
                trunk.height = 4; trunk.radius = .35f;
            }
            gate = CorridorEntranceModels.Attach("temple-gate", transform, "Unfamiliar gate", new Vector3(0, 0, -8));
            CorridorEntranceModels.GroundAtHeight(gate, new Vector3(0, 0, -8), 5.5f, Quaternion.identity);
            gate.gameObject.SetActive(false);
            foreach (float side in new[] { -1f, 1f })
            {
                foreach (float z in new[] { 8.5f, -1f })
                {
                    Box("Lantern post", new Vector3(side * 2.5f, .52f, z), new Vector3(.12f, 1.04f, .12f), wood, false);
                    var lantern = CorridorEntranceModels.Attach("buddhist-lantern", transform, "Temple lantern", Vector3.zero);
                    CorridorEntranceModels.GroundAtHeight(lantern, new Vector3(side * 2.5f, 1.02f, z), .65f, Quaternion.identity);
                    lanterns.Add(lantern);
                }
                Lamp(new Vector3(side * 2.5f, 1.5f, 8.5f), 4.8f, 2.2f);
                Lamp(new Vector3(side * 2.5f, 1.2f, -1f), 4.2f, 1.4f);
            }
            foreach (float side in new[] { -1f, 1f })
                for (int i = 0; i < 3; i++)
                {
                    var rock = CorridorEntranceModels.Attach("cave-rock", transform, "Forest stone", Vector3.zero);
                    CorridorEntranceModels.GroundAtHeight(rock, new Vector3(side * (4.5f + i), 0, -16 + i * 11),
                        .8f + .25f * i, Quaternion.Euler(0, i * 71, 0));
                }
            air = Source("Forest air", "ambience-corridor", new Vector3(5, 2, -3), true, .15f, 35);
            bell = Source("Distant temple bell", "story-bell", new Vector3(-3, 4, 15), false, .10f, 32);
            cry = Source("Distant child crying", "enemy-baby-cry", new Vector3(0, 1.5f, -8), true, .30f, 22);
            gateSound = Source("Gate opening", "door-open", new Vector3(0, 1.5f, -8), false, .65f, 16);
            gateSound.ignoreListenerPause = true; // The interaction immediately enters the masked transition.
            cry.Stop(); air.Play(); bell.Play(); nextBell = 18f;
        }
        void CaptureEnvironment()
        {
            oldFog = RenderSettings.fog; oldFogMode = RenderSettings.fogMode; oldFogColor = RenderSettings.fogColor;
            oldFogDensity = RenderSettings.fogDensity; oldAmbientMode = RenderSettings.ambientMode;
            oldAmbient = RenderSettings.ambientLight; oldAmbientIntensity = RenderSettings.ambientIntensity;
            oldClearFlags = camera.clearFlags; oldBackground = camera.backgroundColor;
            captured = true;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.055f, .067f, .065f); RenderSettings.fogDensity = .035f;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.12f, .14f, .13f);
            RenderSettings.ambientIntensity = .7f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.045f, .060f, .058f, 1);
        }
        Material Material(string name, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit shader is required");
            var result = new Material(shader) { name = name, color = color };
            materials.Add(result); return result;
        }
        Material Surface(Material source, string name, Color color, Vector2 tiling)
        {
            var result = new Material(source) { name = name, color = color };
            result.SetTextureScale("_BaseMap", tiling);
            if (result.HasProperty("_MetallicGlossMap")) result.SetTexture("_MetallicGlossMap", null);
            result.DisableKeyword("_METALLICSPECGLOSSMAP");
            result.SetFloat("_Metallic", 0); result.SetFloat("_Smoothness", .08f);
            materials.Add(result); return result;
        }
        Material ForestEarth()
        {
            // Small repeatable CPU texture for matte soil; no reflective indoor floor map.
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Temple forest soil", wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float a = Mathf.PerlinNoise(x * .125f, y * .125f);
                    float b = Mathf.PerlinNoise(x * .39f + 13, y * .39f + 7);
                    float shade = .72f + .34f * a + .14f * b;
                    pixels[y * size + x] = new Color(.18f * shade, .15f * shade, .105f * shade, 1);
                }
            texture.SetPixels(pixels); texture.Apply(true, false); textures.Add(texture);
            var soil = Material("Matte forest soil", Color.white); soil.SetTexture("_BaseMap", texture);
            soil.SetTextureScale("_BaseMap", new Vector2(14, 18)); soil.SetFloat("_Metallic", 0); soil.SetFloat("_Smoothness", .02f);
            return soil;
        }
        GameObject Box(string name, Vector3 pos, Vector3 scale, Material material, bool collision, bool visible = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.layer = 8;
            go.transform.SetParent(transform, false); go.transform.localPosition = pos; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material; go.GetComponent<Renderer>().enabled = visible;
            if (!collision) Destroy(go.GetComponent<Collider>());
            return go;
        }
        void Lamp(Vector3 at, float range, float intensity)
        {
            var go = new GameObject("Temple lantern light"); go.transform.SetParent(transform, false); go.transform.localPosition = at;
            var light = go.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(1, .58f, .27f);
            light.range = range; light.intensity = intensity; light.shadows = LightShadows.None; lamps.Add(light);
        }
        AudioSource Source(string name, string cue, Vector3 at, bool loop, float volume, float distance)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false); go.transform.localPosition = at;
            var source = go.AddComponent<AudioSource>(); source.playOnAwake = false; source.clip = ExternalAudio.Shared(cue);
            source.loop = loop; source.volume = volume; source.spatialBlend = 1; source.minDistance = 2; source.maxDistance = distance;
            source.rolloffMode = AudioRolloffMode.Linear; source.dopplerLevel = 0; return source;
        }
        public void PlaceIncense()
        {
            if (incense) Destroy(incense.gameObject);
            if (smoke) Destroy(smoke.gameObject);
            var item = Instantiate(Resources.Load<GameObject>("PlayerItems/incense"), transform);
            item.name = "Placed incense"; item.transform.localPosition = new Vector3(0, 1.02f, 7.2f); incense = item.transform;
            var plume = new GameObject("Placed incense smoke"); plume.transform.SetParent(transform, false);
            smoke = plume.AddComponent<IncenseSmokeTrail>(); smoke.Prepare(camera);
        }
        public void BeginIncense(PlayerMotor player)
        {
            if (!player || !player.Hands || !player.Items) throw new InvalidOperationException("Player incense action is unavailable");
            player.Items.Select(PlayerItem.Incense);
            incenseActor = player;
            waitingForIncenseEquip = true;
            pendingIncense = -1;
        }
        public void RestoreIncense(bool placed, PlayerMotor player)
        {
            if (placed) PlaceIncense(); else BeginIncense(player);
        }
        public void BeginSecondDay()
        {
            secondDay = true;
            pendingIncense = -1; waitingForIncenseEquip = false; incenseActor = null;
            if (incense) Destroy(incense.gameObject);
            if (smoke) Destroy(smoke.gameObject);
            incense = null; smoke = null;
            if (bell) bell.Stop();
            for (int i = 0; i < lamps.Count; i++) if (i % 2 == 1)
            {
                lamps[i].enabled = false;
                var block = new MaterialPropertyBlock(); block.SetColor("_EmissionColor", Color.black);
                foreach (var renderer in lanterns[i].GetComponentsInChildren<Renderer>(true)) renderer.SetPropertyBlock(block);
            }
            RenderSettings.fogDensity = .045f;
            RenderSettings.ambientLight = new Color(.075f, .087f, .085f);
            if (cry) cry.Play();
        }
        public void RevealGate() { if (gate) gate.gameObject.SetActive(true); }
        public void PlayGateSound() { if (gateSound) gateSound.Play(); }
        void Update()
        {
            if (!world || !world.Ready || !GameSession.Current || !GameSession.Current.InputAllowed || world.TransitionRequested) return;
            if (!secondDay && bell)
            {
                nextBell -= Time.deltaTime;
                if (nextBell <= 0) { bell.Play(); nextBell = 18f; }
            }
            if (secondDay && cry && camera)
                cry.volume = .30f * Mathf.InverseLerp(2, 8, Vector3.Distance(camera.transform.position, world.GatePoint + Vector3.up * 1.5f));
            if (waitingForIncenseEquip && incenseActor && !incenseActor.HandActions.Busy && incenseActor.Hands.ReadyForAction(PlayerItem.Incense))
            {
                incenseActor.Hands.PlayAction(PlayerItem.Incense);
                pendingIncense = HandAnimationLibrary.Load().GetClip("UseIncense").releaseTime;
                waitingForIncenseEquip = false;
            }
            if (pendingIncense >= 0)
            {
                pendingIncense -= Time.deltaTime;
                if (pendingIncense <= 0) { pendingIncense = -1; PlaceIncense(); }
            }
            if (smoke && incense)
            {
                Vector3 direction = world.GatePoint - world.IncensePoint;
                smoke.Advance(incense.position + Vector3.up * .22f, direction, Time.deltaTime, 1, secondDay ? 1 : 0);
            }
        }
        public void ReleaseEnvironment()
        {
            if (!captured) return;
            RenderSettings.fog = oldFog; RenderSettings.fogMode = oldFogMode; RenderSettings.fogColor = oldFogColor;
            RenderSettings.fogDensity = oldFogDensity; RenderSettings.ambientMode = oldAmbientMode;
            RenderSettings.ambientLight = oldAmbient; RenderSettings.ambientIntensity = oldAmbientIntensity;
            if (camera) { camera.clearFlags = oldClearFlags; camera.backgroundColor = oldBackground; }
            if (schoolAmbience) schoolAmbience.enabled = oldAmbienceEnabled;
            captured = false;
        }
        void OnDestroy()
        {
            ReleaseEnvironment();
            foreach (var item in materials) if (item) Destroy(item);
            foreach (var item in textures) if (item) Destroy(item);
        }
    }
}
