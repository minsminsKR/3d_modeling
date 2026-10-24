using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    // A camera-local colour layer keeps only the burning paper and flame coloured.
    [DefaultExecutionOrder(650)]
    public sealed class FirstPersonHands : MonoBehaviour
    {
        const int ViewLayer = 30;
        const float ActionDuration = .55f;
        // Measured on the imported flashlight: the broad head ends below -.07
        // local Y; the cylindrical grip is about 24mm in radius.
        const float HandleRadius = .024f, HandleCenterY = .035f;
        static readonly string[] WrappedDigits = { "index", "middle", "ring", "little" };
        sealed class Finger
        {
            public Transform bone;
            public Quaternion rest;
            public string digit;
            public string boneName;
            public int joint;
            public bool left;
            public Transform tip;
            public float curl, spread, twist;
            public HandSkinDeformation skinDeformation;
        }
        PlayerMotor player;
        Camera viewCamera;
        Transform studio, leftGrip, rightGrip, left, right, held, torch;
        Transform leftPalm, rightPalm;
        RenderTexture target;
        Image image;
        Shader surfaceShader;
        readonly List<Material> materials = new List<Material>();
        readonly List<Material> heldMaterials = new List<Material>();
        readonly List<Finger> fingers = new List<Finger>();
        readonly List<Transform> flameCards = new List<Transform>();
        readonly List<MaterialPropertyBlock> flameProperties = new List<MaterialPropertyBlock>();
        Material flameMaterial;
        Bounds heldBounds;
        PlayerItem displayed = (PlayerItem)(-1), actionItem;
        bool displayedBurning, displayedAvailable, posed;
        float action, age;
#if UNITY_EDITOR
        float previewFingerDegrees = -1;
#endif
        public RenderTexture Output => target;

        public void PlayAction(PlayerItem item) { action = ActionDuration; actionItem = item; }
        void Awake() { player = GetComponent<PlayerMotor>(); }

        void Prepare()
        {
            studio = new GameObject("First person hand studio").transform;
            studio.SetParent(transform, false);
            studio.position = new Vector3(1000, 1000, 1000);
            viewCamera = studio.gameObject.AddComponent<Camera>();
            viewCamera.clearFlags = CameraClearFlags.SolidColor;
            viewCamera.backgroundColor = Color.clear;
            viewCamera.cullingMask = 1 << ViewLayer;
            viewCamera.fieldOfView = 58;
            viewCamera.nearClipPlane = .01f;
            viewCamera.farClipPlane = 3;
            viewCamera.depth = -20;
            viewCamera.allowHDR = false;
            viewCamera.allowMSAA = true;
            var data = viewCamera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.volumeLayerMask = 0;
            data.renderShadows = false;
            surfaceShader = Resources.Load<Shader>("PlayerItemSurface");
            if (!surfaceShader) throw new InvalidOperationException("Missing player item surface shader");
            leftGrip = Socket("Left flashlight grip");
            rightGrip = Socket("Right item grip");
            left = PlayerItemVisuals.Attach("hand", leftGrip);
            right = PlayerItemVisuals.Attach("hand", rightGrip);
            left.localScale = new Vector3(-1, 1, 1);
            AddHand(left, true);
            AddHand(right, false);
            torch = PlayerItemVisuals.Attach("flashlight", leftGrip);
            torch.localPosition = new Vector3(0, .04f, .03f);
            torch.localRotation = Quaternion.Euler(-90, 0, 0);
            ConfigureMaterials(torch, false, false);
            Layer(studio);
        }
        Transform Socket(string name)
        {
            var socket = new GameObject(name).transform;
            socket.SetParent(studio, false);
            return socket;
        }
        void AddHand(Transform root, bool isLeft)
        {
            var skinDeformation = new HandSkinDeformation(root);
            foreach (var bone in root.GetComponentsInChildren<Transform>(true))
            {
                int separator = bone.name.LastIndexOf('_');
                if (separator < 0 || !int.TryParse(bone.name.Substring(separator + 1), out int joint)) continue;
                string digit = bone.name.Substring(0, separator);
                if (digit != "thumb" && digit != "index" && digit != "middle" && digit != "ring" && digit != "little") continue;
                fingers.Add(new Finger { bone = bone, rest = bone.localRotation, digit = digit, joint = joint, left = isLeft,
                    tip = joint == 3 ? bone.Find(digit + "_tip") : null, skinDeformation = skinDeformation, boneName = bone.name });
            }
            if (isLeft) leftPalm = FindMarker(root,"palm_contact"); else rightPalm = FindMarker(root,"palm_contact");
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { skin.updateWhenOffscreen = true; skin.forceMatrixRecalculationPerRender = true; }
            ConfigureMaterials(root, false, false);
        }
        static void Layer(Transform node)
        {
            foreach (var child in node.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = ViewLayer;
        }
        void ConfigureMaterials(Transform root, bool burning, bool disposable)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var sources = renderer.sharedMaterials;
                var owned = new Material[sources.Length];
                for (int i = 0; i < sources.Length; i++)
                {
                    var source = sources[i];
                    var material = new Material(surfaceShader) { name = "Viewmodel " + (source ? source.name : "surface") };
                    if (source)
                    {
                        string albedo = source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
                        if (source.HasProperty(albedo))
                        {
                            material.SetTexture("_BaseMap", source.GetTexture(albedo));
                            material.SetTextureScale("_BaseMap", source.GetTextureScale(albedo));
                            material.SetTextureOffset("_BaseMap", source.GetTextureOffset(albedo));
                        }
                        material.SetColor("_BaseColor", source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") :
                            source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white);
                        string metal = source.HasProperty("_MetalRough") ? "_MetalRough" : "_MetallicGlossMap";
                        Texture map = source.HasProperty(metal) ? source.GetTexture(metal) : null;
                        if (source.HasProperty("_HasMetalRough") && source.GetFloat("_HasMetalRough") < .5f) map = null;
                        material.SetTexture("_MetalRough", map);
                        material.SetFloat("_HasMetalRough", map ? 1 : 0);
                        material.SetFloat("_PbrLayout", metal == "_MetalRough" ? 0 : 1);
                        material.SetFloat("_Metallic", source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0);
                        material.SetFloat("_Smoothness", source.HasProperty("_Smoothness") ? source.GetFloat("_Smoothness") : .45f);
                        Texture normal = source.HasProperty("_BumpMap") ? source.GetTexture("_BumpMap") : null;
                        material.SetTexture("_BumpMap", normal);
                        material.SetFloat("_HasNormal", normal ? 1 : 0);
                        material.SetFloat("_BumpScale", source.HasProperty("_BumpScale") ? source.GetFloat("_BumpScale") : 1);
                        material.SetFloat("_Skin", source.HasProperty("_Skin") ? source.GetFloat("_Skin") : 0);
                    }
                    material.SetFloat("_Viewmodel", 1);
                    material.SetFloat("_Burning", burning ? 1 : 0);
                    materials.Add(material);
                    if (disposable) heldMaterials.Add(material);
                    if (burning) SetBurnHeight(material, renderer, root);
                    owned[i] = material;
                }
                renderer.sharedMaterials = owned;
                // Explicit textures avoid the initial isolated-camera SRP cache
                // reusing the skin texture for another close-up renderer.
                for (int i = 0; i < owned.Length; i++)
                {
                    var block = new MaterialPropertyBlock();
                    foreach (string property in new[] { "_BaseMap", "_MetalRough", "_BumpMap" })
                    {
                        var texture = owned[i].GetTexture(property);
                        if (texture) block.SetTexture(property, texture);
                    }
                    renderer.SetPropertyBlock(block, i);
                }
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }
        static Bounds LocalBounds(Transform root)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var local = renderer.localBounds;
                var matrix = root.worldToLocalMatrix * renderer.localToWorldMatrix;
                for (int corner = 0; corner < 8; corner++)
                {
                    var p = local.center + Vector3.Scale(local.extents, new Vector3((corner & 1) == 0 ? -1 : 1,
                        (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    p = matrix.MultiplyPoint3x4(p);
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
                }
            }
            return bounds;
        }
        void SetBurnHeight(Material material, Renderer renderer, Transform root)
        {
            // FBX meshes retain exporter axes. Measure height in the upright prefab,
            // then map each renderer's vertices into that space before clipping.
            var matrix = root.worldToLocalMatrix * renderer.localToWorldMatrix;
            float span = Mathf.Max(.0001f, heldBounds.size.y);
            material.SetVector("_BurnHeight", new Vector4(matrix.m10 / span, matrix.m11 / span,
                matrix.m12 / span, (matrix.m13 - heldBounds.min.y) / span));
        }
        void ShowItem(PlayerItem item, bool burning, bool available)
        {
            if (held) Discard(held.gameObject);
            held = null;
            foreach (var material in heldMaterials) { materials.Remove(material); Discard(material); }
            heldMaterials.Clear();
            foreach (var card in flameCards) if (card) Discard(card.gameObject);
            flameCards.Clear(); flameProperties.Clear();
            if (available)
            {
                switch (item)
                {
                    case PlayerItem.Snapper: held = SnapperVisuals.AttachPellet(rightGrip); held.localScale *= .85f; break;
                    case PlayerItem.Trap: held = TrapVisuals.Attach(rightGrip, .23f).transform; break;
                    case PlayerItem.Incense: held = PlayerItemVisuals.Attach("incense", rightGrip); break;
                    case PlayerItem.Talisman: held = PlayerItemVisuals.Attach("talisman", rightGrip); break;
                }
            }
            if (held)
            {
                heldBounds = LocalBounds(held);
                ConfigureMaterials(held, burning, true);
                Layer(held);
                if (burning) PrepareFlames();
            }
            displayed = item; displayedBurning = burning; displayedAvailable = available;
        }
        void PrepareFlames()
        {
            if (!flameMaterial)
            {
                var shader = Resources.Load<Shader>("GraphicsUpgrade/Shaders/CandleFlame");
                var texture = Resources.Load<Texture2D>("GraphicsUpgrade/Textures/candle-flame-v3");
                if (!shader || !texture) throw new InvalidOperationException("Missing photographic talisman flame resources");
                flameMaterial = new Material(shader) { name = "Burning talisman photographic flame" };
                flameMaterial.SetTexture("_FlameTex", texture);
                flameMaterial.SetColor("_FlameColor", new Color(1, .8f, .52f));
                flameMaterial.SetFloat("_Emission", 2);
                flameMaterial.SetFloat("_Opacity", .82f);
            }
            for (int i = 0; i < 5; i++)
            {
                var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
                card.name = "Paper edge flame " + i;
                card.transform.SetParent(studio, false);
                card.layer = ViewLayer;
                Discard(card.GetComponent<Collider>());
                var renderer = card.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = flameMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                flameCards.Add(card.transform);
                flameProperties.Add(new MaterialPropertyBlock());
            }
        }
        void EnsureTarget(int width, int height)
        {
            if (target && target.width == width && target.height == height) return;
            if (target) { target.Release(); Discard(target); }
            target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Hands and held items", antiAliasing = 2 };
            target.Create(); viewCamera.targetTexture = target; viewCamera.aspect = (float)width / height;
            if (image != null) image.image = target;
        }
        void LateUpdate()
        {
            var session = GameSession.Current;
            bool visible = player && player.eyes && !player.Hidden && session && session.InputAllowed &&
                (!session.ChapterMode || !session.Chapter.FirstAppearances || !session.Chapter.FirstAppearances.CameraOwned);
            if (!studio && visible) Prepare();
            if (!studio) return;
            viewCamera.enabled = visible;
            var view = session ? session.GetComponent<GameShellView>() : null;
            if (view && view.Root != null)
            {
                if (image == null)
                {
                    image = new Image { name = "first-person-hands", pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.StretchToFill };
                    image.style.position = Position.Absolute;
                    image.style.left = image.style.right = image.style.top = image.style.bottom = 0;
                    image.image = target;
                }
                if (image.parent != view.Root) view.Root.Insert(0, image);
                image.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (!visible) return;
            float scale = Mathf.Min(1, 2560f / Mathf.Max(1, Screen.width), 1440f / Mathf.Max(1, Screen.height));
            EnsureTarget(Mathf.Max(1, Mathf.RoundToInt(Screen.width * scale)), Mathf.Max(1, Mathf.RoundToInt(Screen.height * scale)));
            bool burning = player.Rituals && player.Rituals.Burning;
            var item = burning ? PlayerItem.Talisman : player.Items.Selected;
            bool available = burning || item == PlayerItem.Flashlight || player.Items.Count(item) > 0 ||
                (item == PlayerItem.Incense && player.Rituals.Smoke);
            age += Time.deltaTime; action = Mathf.Max(0, action - Time.deltaTime);
            DrawPose(item, burning, available, burning ? player.Rituals.BurnedFraction : 0,
                Time.deltaTime, player.ActualSpeed > .1f, player.Running, session.Shell.ReducedMotion);
        }
        void DrawPose(PlayerItem item, bool burning, bool available, float burnFraction, float delta, bool walking, bool running, bool reducedMotion)
        {
            if (displayed != item || displayedBurning != burning || displayedAvailable != available) ShowItem(item, burning, available);
            float sway = reducedMotion ? 0 : Mathf.Sin(age * (running ? 10 : 6)) * (walking ? .006f : .001f);
            float sideScale = Mathf.Max(1, viewCamera.aspect / (16f / 9));
            leftGrip.localPosition = new Vector3(-.260f * sideScale, -.230f + sway, .50f);
            leftGrip.localRotation = Quaternion.Euler(-45, 0, -18);
            bool palmUp = item == PlayerItem.Trap;
            bool looseGrip = item == PlayerItem.Snapper;
            rightGrip.localPosition = new Vector3((palmUp || looseGrip ? .235f : .315f) * sideScale, (palmUp || looseGrip ? -.195f : -.245f) - sway, .50f);
            rightGrip.localRotation = Quaternion.identity;
            left.localPosition = new Vector3(0, -.062f, -.036f);
            left.localRotation = Quaternion.Euler(0, 90, -6);
            right.localPosition = new Vector3(0, -.07f, -.036f);
            right.localRotation = palmUp ? Quaternion.Euler(-65, 132, -8) : looseGrip ? Quaternion.Euler(8, 158, -12) : Quaternion.Euler(4, -24, 8);
            right.gameObject.SetActive(item != PlayerItem.Flashlight && (available || action > 0));
            float phase = 1 - action / ActionDuration;
            float flick = action > 0 ? Mathf.Sin(phase * Mathf.PI) : 0;
            if (actionItem == PlayerItem.Snapper && action > 0)
            {
                rightGrip.localPosition += new Vector3(flick * .10f * sideScale, flick * .015f, flick * .10f);
                rightGrip.localRotation = Quaternion.Euler(-flick * 8, 0, flick * 8);
            }
            else if (actionItem == PlayerItem.Trap) rightGrip.localPosition += new Vector3(0, -flick * .09f, flick * .05f);
            else if (actionItem == PlayerItem.Incense || actionItem == PlayerItem.Talisman)
                // Lift the pinched item along the outer edge; bringing tall
                // paper inward crosses the protected forward sightline.
                rightGrip.localPosition += new Vector3(flick * .02f, flick * .025f, 0);
            if (held)
            {
                held.gameObject.SetActive(!(actionItem == PlayerItem.Snapper && action > 0 && phase > .38f));
                switch (item)
                {
                    case PlayerItem.Talisman:
                        held.localPosition = new Vector3(-.035f, .17f, -.005f);
                        held.localRotation = Quaternion.Euler(0, 165, -8);
                        break;
                    case PlayerItem.Incense:
                        held.localPosition = new Vector3(-.025f, .04f, .025f);
                        held.localRotation = Quaternion.Euler(-5, 0, -8);
                        break;
                    case PlayerItem.Trap:
                        held.localPosition = new Vector3(-.02f, .03f, .045f);
                        held.localRotation = Quaternion.Euler(0, 0, -8);
                        break;
                    default:
                        held.localPosition = new Vector3(-.035f, .04f, .05f);
                        held.localRotation = Quaternion.Euler(12, 0, -10);
                        break;
                }
            }
            float poseBlend = posed ? 1 - Mathf.Exp(-18 * delta) : 1;
            foreach (var finger in fingers)
            {
                float angle = FingerCurl(finger, item, available);
                if (!finger.left && actionItem == PlayerItem.Snapper && action > 0)
                    angle = Mathf.Lerp(angle, 8, Ease(.22f, .42f, phase) * (1 - Ease(.72f, 1, phase)));
#if UNITY_EDITOR
                if (previewFingerDegrees >= 0) angle = previewFingerDegrees;
#endif
                float spread = FingerSpread(finger, palmUp);
                float twist = finger.digit == "thumb" && finger.joint == 1 ? finger.left ? 40 : palmUp ? -10 : -18 : 0;
#if UNITY_EDITOR
                if (previewFingerDegrees >= 0) spread = twist = 0;
#endif
                finger.curl = Mathf.Lerp(finger.curl, angle, poseBlend);
                finger.spread = Mathf.Lerp(finger.spread, spread, poseBlend);
                finger.twist = Mathf.Lerp(finger.twist, twist, poseBlend);
                ApplyFingerPose(finger);
            }
            bool pinch = available && (item == PlayerItem.Talisman || item == PlayerItem.Incense);
#if UNITY_EDITOR
            if (previewFingerDegrees >= 0) pinch = false;
#endif
            bool gripTorch = true;
#if UNITY_EDITOR
            if (previewFingerDegrees >= 0) gripTorch = false;
#endif
            // Set the actual handle first, then solve skin contact around it.
            torch.position = Palm(true) + left.forward * (HandleRadius + .001f) - torch.up * HandleCenterY;
            if (gripTorch) GripFlashlight();
            if (pinch) PinchFingers(right);
            if (looseGrip && available && action <= 0)
                SolveThumb(false, Tip(false, "index") - right.right * .015f + right.up * .018f);
            AlignProps(item);
            foreach (var finger in fingers) finger.skinDeformation.Apply(finger.boneName, finger.curl);
            posed = true;
            foreach (var material in materials) if (material) material.SetFloat("_Monochrome", burning ? 1 : 0);
            foreach (var material in heldMaterials) if (material) material.SetFloat("_Burn", burnFraction);
            DrawFlames(burnFraction);
        }
        Finger FindFinger(bool isLeft, string digit, int joint)
        {
            foreach (var finger in fingers) if (finger.left == isLeft && finger.digit == digit && finger.joint == joint) return finger;
            return null;
        }
        Vector3 Tip(bool isLeft, string digit)
        {
            var finger = FindFinger(isLeft, digit, 3);
            if (finger == null) return isLeft ? leftGrip.position : rightGrip.position;
            if (finger.tip) return finger.tip.position;
            // Exported phalanges point along local Y; the final length is close
            // to the previous joint span and scales with the imported armature.
            return finger.bone.TransformPoint(Vector3.up * (Mathf.Abs(finger.bone.localPosition.y) * .8f));
        }
        static Transform FindMarker(Transform root, string name)
        {
            foreach (var node in root.GetComponentsInChildren<Transform>()) if (node.name == name) return node;
            return null;
        }
        Vector3 Palm(bool isLeft)
        {
            var hand = isLeft ? left : right;
            var marker = isLeft ? leftPalm : rightPalm;
            return marker ? marker.position : hand.TransformPoint(new Vector3(0, .085f, .014f));
        }
        static void ApplyFingerPose(Finger finger)
        {
            // Separate anatomical axes: flexion at each phalanx, metacarpal
            // spread, and the thumb's fixed opposition. CCD never adds roll.
            finger.bone.localRotation = finger.rest * Quaternion.AngleAxis(finger.spread, Vector3.forward) *
                Quaternion.AngleAxis(finger.twist, Vector3.up) * Quaternion.AngleAxis(finger.curl, Vector3.right);
        }
        void SolveFinger(bool isLeft, string digit, Vector3 target)
        {
            for (int iteration = 0; iteration < 14; iteration++)
            {
                for (int joint = 2; joint >= 1; joint--)
                {
                    var finger = FindFinger(isLeft, digit, joint);
                    if (finger == null) continue;
                    var bone = finger.bone;
                    var current = Tip(isLeft, digit) - bone.position;
                    var desired = target - bone.position;
                    // Solve in parent space, including the opposite hand's
                    // reflected scale. World quaternions cannot encode it.
                    current = bone.parent.InverseTransformVector(current);
                    desired = bone.parent.InverseTransformVector(desired);
                    if (current.sqrMagnitude < 1e-16f || desired.sqrMagnitude < 1e-16f) continue;
                    if (digit == "thumb" && joint == 1)
                    {
                        var spreadAxis = finger.rest * Vector3.forward;
                        float spreadChange = Vector3.SignedAngle(Vector3.ProjectOnPlane(current, spreadAxis),
                            Vector3.ProjectOnPlane(desired, spreadAxis), spreadAxis);
                        finger.spread = Mathf.Clamp(finger.spread + spreadChange * .55f, -65, 20);
                        ApplyFingerPose(finger);
                        current = bone.parent.InverseTransformVector(Tip(isLeft, digit) - bone.position);
                    }
                    var axis = finger.rest * Quaternion.AngleAxis(finger.spread, Vector3.forward) *
                        Quaternion.AngleAxis(finger.twist, Vector3.up) * Vector3.right;
                    float change = Vector3.SignedAngle(Vector3.ProjectOnPlane(current, axis), Vector3.ProjectOnPlane(desired, axis), axis);
                    float limit = digit == "thumb" ? joint == 1 ? 48 : 70 : joint == 1 ? 68 : 85;
                    float minimum = digit == "thumb" && joint == 1 ? -35 : 0;
                    finger.curl = Mathf.Clamp(finger.curl + change * .65f, minimum, limit);
                    ApplyFingerPose(finger);
                    if (joint == 2)
                    {
                        // A relaxed distal joint follows the middle phalanx;
                        // tip-only CCD otherwise folds it into a sharp hook.
                        var distal = FindFinger(isLeft, digit, 3);
                        if (distal != null)
                        {
                            distal.curl = Mathf.Min(digit == "thumb" ? 50 : 55, finger.curl * (digit == "thumb" ? .75f : .65f));
                            ApplyFingerPose(distal);
                        }
                    }
                }
                if ((Tip(isLeft, digit) - target).sqrMagnitude < .000001f) break;
            }
        }
        void SolveThumb(bool isLeft, Vector3 target)
        {
            var metacarpal = FindFinger(isLeft, "thumb", 1);
            if (metacarpal == null) return;
            // Opposition belongs to the thumb's saddle joint. Ordinary finger
            // flexion alone cannot bring this new anatomical thumb around a
            // cylinder. Keep both CMC axes bounded; phalanges still only flex.
            for (int pass = 0; pass < 5; pass++)
            {
                SolveFinger(isLeft, "thumb", target);
                if ((Tip(isLeft,"thumb")-target).sqrMagnitude < .00000225f) break;
                float step = 20 * Mathf.Pow(.6f,pass);
                for (int axis = 0; axis < 2; axis++)
                {
                    float origin = axis == 0 ? metacarpal.spread : metacarpal.twist;
                    float best = origin, cost = (Tip(isLeft,"thumb")-target).sqrMagnitude;
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        float candidate = Mathf.Clamp(origin + sign * step, axis == 0 ? -45 : -20, axis == 0 ? 35 : 80);
                        if (axis == 0) metacarpal.spread = candidate; else metacarpal.twist = candidate;
                        ApplyFingerPose(metacarpal);
                        float error = (Tip(isLeft,"thumb")-target).sqrMagnitude;
                        if (error < cost) { cost = error; best = candidate; }
                    }
                    if (axis == 0) metacarpal.spread = best; else metacarpal.twist = best;
                    ApplyFingerPose(metacarpal);
                }
            }
            SolveFinger(isLeft,"thumb",target);
        }
        void GripFlashlight()
        {
            var axis = -torch.up;
            var center = torch.TransformPoint(new Vector3(0, HandleCenterY, 0));
            var around = left.forward;
            SolveThumb(true, center - around * (HandleRadius + .002f) + left.up * .004f + axis * .026f);
            for (int i = 0; i < WrappedDigits.Length; i++)
            {
                // Follow the knuckles' real order along the handle. A fixed
                // reversed list crossed the index/little targets on the mirror.
                var knuckle = FindFinger(true, WrappedDigits[i], 1);
                float along = Mathf.Clamp(Vector3.Dot(knuckle.bone.position - center, axis), -.045f, .045f);
                SolveFinger(true, WrappedDigits[i], center + around * (HandleRadius + .0085f) - left.up * .005f + axis * along);
            }
        }
        void PinchFingers(Transform hand)
        {
            var target = hand.TransformPoint(new Vector3(-.031f, .16f, .065f));
            SolveThumb(false, target - hand.right * .003f);
            SolveFinger(false, "index", target + hand.right * .003f);
        }
        void AlignProps(PlayerItem item)
        {
            // Keep the handle in the palm and every small prop between actual
            // fingertips, including during the shared wrist/action motion.
            if (!held) return;
            var pinch = (Tip(false, "thumb") + Tip(false, "index")) * .5f;
            switch (item)
            {
                case PlayerItem.Talisman:
                    held.position = pinch + held.up * (heldBounds.size.y * .5f - .008f) + held.right * (heldBounds.size.x * .28f);
                    break;
                case PlayerItem.Incense:
                    held.position = pinch + held.up * (heldBounds.size.y * .5f - .035f);
                    break;
                case PlayerItem.Snapper:
                    held.position = Palm(false) + right.forward * .006f;
                    break;
                case PlayerItem.Trap:
                    // The base rests on the open palm, with teeth pointing up.
                    held.position = Palm(false) - held.up * heldBounds.min.y;
                    break;
            }
        }
        static float FingerCurl(Finger finger, PlayerItem item, bool available)
        {
            int joint = Mathf.Clamp(finger.joint - 1, 0, 2);
            if (!finger.left && !available) return joint == 1 ? 18 : 12;
            if (!finger.left && item == PlayerItem.Snapper)
            {
                if (finger.digit == "thumb") return joint == 0 ? 18 : joint == 1 ? 30 : 20;
                float snapperExtra = finger.digit == "little" ? 6 : finger.digit == "ring" ? 3 : 0;
                return (joint == 0 ? 48 : joint == 1 ? 65 : 38) + snapperExtra;
            }
            bool support = !finger.left && item == PlayerItem.Trap;
            if (support)
            {
                if (finger.digit == "thumb") return joint == 0 ? 14 : joint == 1 ? 28 : 20;
                float cup = finger.digit == "little" ? 6 : finger.digit == "ring" ? 4 : finger.digit == "middle" ? 2 : 0;
                return (joint == 0 ? 10 : joint == 1 ? 52 : 34) + cup;
            }
            if (finger.digit == "thumb") return joint == 0 ? 22 : joint == 1 ? 28 : 16;
            bool pinch = !finger.left && (item == PlayerItem.Talisman || item == PlayerItem.Incense);
            if (pinch && finger.digit == "index") return joint == 0 ? 18 : joint == 1 ? 32 : 18;
            float extra = finger.digit == "little" ? 8 : finger.digit == "ring" ? 4 : 0;
            return (joint == 0 ? 26 : joint == 1 ? 52 : 32) + extra;
        }
        static float FingerSpread(Finger finger, bool palmUp)
        {
            if (finger.joint != 1) return 0;
            if (finger.digit == "thumb") return -30;
            if (!finger.left && palmUp)
                return finger.digit == "index" ? -4 : finger.digit == "ring" ? 4 : finger.digit == "little" ? 9 : 0;
            return finger.digit == "index" ? -2 : finger.digit == "ring" ? 2 : finger.digit == "little" ? 4 : 0;
        }
        static float Ease(float from, float to, float value) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(from, to, value));
        void DrawFlames(float burnFraction)
        {
            if (!held || flameCards.Count == 0) return;
            float line = Mathf.Lerp(heldBounds.max.y, heldBounds.min.y, burnFraction);
            float fade = Ease(0, .015f, burnFraction) * (1 - Ease(.97f, 1, burnFraction));
            for (int i = 0; i < flameCards.Count; i++)
            {
                float wave = Mathf.Sin(age * (15 + i * 1.1f) + i * 2.4f);
                float height = .026f + .009f * (wave * .5f + .5f);
                var p = new Vector3(Mathf.Lerp(heldBounds.min.x, heldBounds.max.x, (i + .5f) / flameCards.Count), line, heldBounds.center.z);
                var card = flameCards[i];
                card.position = held.TransformPoint(p) + viewCamera.transform.up * (height * .43f) - viewCamera.transform.forward * .002f;
                card.rotation = viewCamera.transform.rotation * Quaternion.Euler(0, 0, wave * 10);
                card.localScale = new Vector3(.013f + wave * .002f, height, 1);
                var properties = flameProperties[i];
                properties.SetFloat("_Opacity", fade * (.64f + wave * .1f));
                card.GetComponent<Renderer>().SetPropertyBlock(properties);
            }
        }
        public bool TryGetIncenseTip(out Vector3 cameraLocal)
        {
            cameraLocal = default;
            if (!held || displayed != PlayerItem.Incense || !viewCamera) return false;
            var tip = held.TransformPoint(new Vector3(heldBounds.center.x, heldBounds.max.y, heldBounds.center.z));
            var viewport = viewCamera.WorldToViewportPoint(tip);
            cameraLocal = player && player.eyes ? player.eyes.transform.InverseTransformPoint(player.eyes.ViewportToWorldPoint(viewport)) :
                viewCamera.transform.InverseTransformPoint(tip);
            return true;
        }
#if UNITY_EDITOR
        public string EditorGripReport()
        {
            var center=torch.TransformPoint(new Vector3(0,HandleCenterY,0));
            var axis=torch.up;
            var text=new System.Text.StringBuilder();
            foreach(string digit in new[]{"thumb","index","middle","ring","little"})
            {
                var offset=Tip(true,digit)-center;
                var radial=Vector3.ProjectOnPlane(offset,axis);
                text.AppendLine(digit+" radial="+radial.ToString("F5")+" radius="+radial.magnitude.ToString("F5")+
                    " thumbDot="+Vector3.Dot(radial.normalized,Vector3.ProjectOnPlane(Tip(true,"thumb")-center,axis).normalized).ToString("F4"));
            }
            return text.ToString();
        }
        // Opt-in still rendering; no player launch or game/session clock is needed.
        public Camera PrepareEditorPreview(PlayerItem item, float burnFraction, float actionProgress = -1, float fingerDegrees = -1)
        {
            if (!studio) Prepare();
            EnsureTarget(1280, 720);
            age = 1.3f; previewFingerDegrees = fingerDegrees;
            actionItem = item; action = actionProgress < 0 ? 0 : ActionDuration * (1 - Mathf.Clamp01(actionProgress));
            posed = false;
            DrawPose(item, burnFraction >= 0, true, Mathf.Max(0, burnFraction), 0, false, false, true);
            if (fingerDegrees >= 0)
            {
                if (held) held.gameObject.SetActive(false);
                rightGrip.localPosition = new Vector3(.2f, -.15f, .43f);
            }
            return viewCamera;
        }
#endif
        void OnDisable()
        {
            if (viewCamera) viewCamera.enabled = false;
            if (image != null) image.style.display = DisplayStyle.None;
        }
        static void Discard(UnityEngine.Object value)
        {
            if (!value) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
        void OnDestroy()
        {
            image?.RemoveFromHierarchy();
            if (target) { target.Release(); Discard(target); }
            foreach (var material in materials) Discard(material);
            Discard(flameMaterial);
            if (studio) Discard(studio.gameObject);
        }
    }
}











