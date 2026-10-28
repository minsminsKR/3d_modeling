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
        // Measured on the imported flashlight: the broad head ends below -.07
        // local Y; the cylindrical grip is about 24mm in radius.
        const float HandleRadius = .024f, HandleCenterY = .035f;
        HandAnimationLibrary library;
        HandAnimationRig leftRig, rightRig;
        HandCylinderContact torchContact;
        readonly HandActionTimeline cosmeticAction = new HandActionTimeline();
        readonly HandEquipTransition equip = new HandEquipTransition();
        readonly HandMotionState leftMotion = new HandMotionState(), rightMotion = new HandMotionState();
        HandMotionSample leftSway, rightSway;
        Vector3 previousPlayerPosition;
        float previousYaw, previousPitch, replenish = 1, paperRetire = -1;
        bool motionReady, lastReleased;
        PlayerFeedback footsteps;
        float stepImpulse;
        HeldPaperEffects paperEffects;
        PlayerMotor player;
        Camera viewCamera;
        Transform studio, leftGrip, rightGrip, left, right, held, torch;
        RenderTexture target;
        Image image;
        Shader surfaceShader;
        readonly List<Material> materials = new List<Material>();
        readonly List<Material> heldMaterials = new List<Material>();
        readonly List<Transform> flameCards = new List<Transform>();
        readonly List<MaterialPropertyBlock> flameProperties = new List<MaterialPropertyBlock>();
        Material flameMaterial;
        Bounds heldBounds;
        PlayerItem displayed = (PlayerItem)(-1), actionItem;
        bool displayedBurning, displayedAvailable;
        float age;
#if UNITY_EDITOR
        float previewFingerDegrees = -1;
        float previewActionTime = -1;
#endif
        public RenderTexture Output => target;
        public PlayerItem PresentedItem => displayed;
        public bool EquipmentSettled => studio && !equip.Busy && replenish >= .999f;

        public void PlayAction(PlayerItem item)
        {
            library = library ?? HandAnimationLibrary.Load();
            var clip = library.GetClip("Use" + item);
            cosmeticAction.Reset();
            cosmeticAction.TryBegin(PlayerHandActions.Kind(item), new HandActionTiming(clip.duration,
                HandActionTiming.For(PlayerHandActions.Kind(item)).PrepareEnds, clip.releaseTime));
            actionItem = item;
        }
        void Awake() { player = GetComponent<PlayerMotor>(); }

        void Prepare()
        {
            library = HandAnimationLibrary.Load();
            if (player && player.Feedback)
            { footsteps = player.Feedback; footsteps.Footstep += OnFootstep; }
            studio = new GameObject("First person hand studio").transform;
            studio.SetParent(transform, false);
            studio.position = new Vector3(1000, 1000, 1000);
            viewCamera = studio.gameObject.AddComponent<Camera>();
            viewCamera.clearFlags = CameraClearFlags.SolidColor;
            viewCamera.backgroundColor = Color.clear;
            viewCamera.cullingMask = 1 << ViewLayer;
            viewCamera.fieldOfView = 58;
            viewCamera.aspect = 16f / 9;
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
            torchContact = new HandCylinderContact(torch);
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
            if (isLeft) leftRig = new HandAnimationRig(root, library); else rightRig = new HandAnimationRig(root, library);
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            { skin.quality = SkinQuality.Bone4; skin.updateWhenOffscreen = true; skin.forceMatrixRecalculationPerRender = true; }
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
            paperEffects?.Dispose(); paperEffects = null;
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
                    case PlayerItem.Trap: held = TrapVisuals.Attach(rightGrip).transform; break;
                    case PlayerItem.Incense: held = PlayerItemVisuals.Attach("incense", rightGrip); break;
                    case PlayerItem.Talisman: held = PlayerItemVisuals.Attach("talisman", rightGrip); break;
                }
            }
            if (held)
            {
                heldBounds = LocalBounds(held);
                ConfigureMaterials(held, burning, true);
                if (item == PlayerItem.Talisman)
                {
                    foreach (var renderer in held.GetComponentsInChildren<Renderer>(true))
                        foreach (var material in renderer.sharedMaterials)
                        { SetBurnHeight(material, renderer, held); material.SetFloat("_PaperFlex", 1); }
                    paperEffects = new HeldPaperEffects(held, heldBounds, ViewLayer);
                }
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
            bool graphics = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
            viewCamera.enabled = visible && graphics;
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
            if (graphics) EnsureTarget(Mathf.Max(1, Mathf.RoundToInt(Screen.width * scale)), Mathf.Max(1, Mathf.RoundToInt(Screen.height * scale)));
            bool burning = player.Rituals && player.Rituals.Burning;
            var item = burning ? PlayerItem.Talisman : player.Items.Selected;
            bool available = burning || item == PlayerItem.Flashlight || player.Items.Count(item) > 0 ||
                (item == PlayerItem.Incense && player.Rituals.Smoke);
            age += Time.deltaTime; cosmeticAction.Advance(Time.deltaTime);
            AdvanceMotion(Time.deltaTime, player.Running);
            DrawPose(item, burning, available, burning ? player.Rituals.BurnedFraction : 0,
                Time.deltaTime, player.ActualSpeed > .1f, player.Running, session.Shell.ReducedMotion);
        }
        public bool ReadyForAction(PlayerItem item) => studio && (item == PlayerItem.Flashlight ||
            !equip.Busy && displayed == item && replenish >= .999f);

        bool HasItem(PlayerItem item, bool burning, bool fallback)
        {
            if (!player || !player.Items) return fallback;
            return item == PlayerItem.Flashlight || burning && item == PlayerItem.Talisman ||
                player.Items.Count(item) > 0 || item == PlayerItem.Incense && player.Rituals.Smoke;
        }
        void DrawPose(PlayerItem requested, bool burning, bool available, float burnFraction, float delta, bool walking, bool running, bool reducedMotion)
        {
            var actionOwner = player ? player.HandActions : null;
            bool owned = actionOwner && actionOwner.Playing;
            bool playing = owned || cosmeticAction.Busy;
            var used = owned ? actionOwner.PendingItem : actionItem;
            float time = owned ? actionOwner.AnimationTime : cosmeticAction.Elapsed;
            bool released = owned && actionOwner.Released && (used == PlayerItem.Snapper || used == PlayerItem.Trap);
#if UNITY_EDITOR
            if (previewActionTime >= 0)
            {
                playing = true; used = requested; time = previewActionTime;
                released = (used == PlayerItem.Snapper || used == PlayerItem.Trap) && time > library.GetClip("Use" + used).releaseTime;
            }
#endif
            if (lastReleased && !released) replenish = 0;
            lastReleased = released;
            replenish = Mathf.Min(1, replenish + Mathf.Max(0, delta) / .30f);
            var desired = playing && used != PlayerItem.Flashlight ? used : requested;
            if ((int)displayed < 0) equip.Reset((int)desired);
            if (!playing && !equip.Busy && equip.VisibleItem != (int)desired)
                equip.TryBegin(equip.VisibleItem, (int)desired);
#if UNITY_EDITOR
            if (!Application.isPlaying) equip.Reset((int)desired);
#endif
            equip.Advance(delta);
            var item = (PlayerItem)equip.VisibleItem;
            bool paperBurning = burning && item == PlayerItem.Talisman;
            bool present = HasItem(item, burning, available);
            // The paper lifetime still ends at the inventory clock. Keep the
            // empty fingers visible long enough to lower them after the last
            // sheet burns, instead of removing a raised hand in one frame.
            if (displayedBurning && !paperBurning && item == PlayerItem.Talisman && !present)
                paperRetire = 0;
            if (present || item != PlayerItem.Talisman) paperRetire = -1;
            if (paperRetire >= 0) paperRetire = Mathf.Min(.30f, paperRetire + Mathf.Max(0, delta));
            bool retiringPaper = paperRetire >= 0 && paperRetire < .30f;
            // Removing/adding a supply happens under a lowered hand or at its
            // actual release, never while changing a fully visible grip.
            if (displayed != item || displayedBurning != paperBurning || displayedAvailable != present)
                ShowItem(item, paperBurning, present);
            var leftClip = playing && used == PlayerItem.Flashlight ? library.GetClip("UseFlashlight") : null;
            var rightClip = playing && used == item && used != PlayerItem.Flashlight ? library.GetClip("Use" + item) : null;
            float rightTime = time;
            if (equip.Busy)
            {
                bool leaving = equip.Progress < .5f;
                rightClip = library.GetClip((leaving ? "Unequip" : "Equip") + item);
                rightTime = (leaving ? equip.Progress * 2 : (equip.Progress - .5f) * 2) * rightClip.duration;
            }
            else if (retiringPaper && rightClip == null)
            {
                rightClip = library.GetClip("UnequipTalisman");
                rightTime = paperRetire / .30f * rightClip.duration;
            }
            float sideScale = Mathf.Max(1, viewCamera.aspect / (16f / 9));
            leftGrip.localPosition = new Vector3(-.260f * sideScale, -.230f, .50f);
            leftGrip.localRotation = Quaternion.Euler(15, 0, 0);
            bool support = item == PlayerItem.Trap;
            float height = support ? -.305f : item == PlayerItem.Talisman ? -.10f : -.245f;
            rightGrip.localPosition = new Vector3((support ? .300f : .315f) * sideScale, height, support ? .62f : .50f);
            rightGrip.localRotation = Quaternion.identity;
            left.localPosition = new Vector3(0, -.062f, -.036f);
            left.localRotation = Quaternion.Euler(0, 90, -6);
            right.localPosition = new Vector3(0, -.07f, -.036f);
            right.localRotation = support ? Quaternion.Euler(-65, 132, -8) : Quaternion.Euler(4, -24, 8);
            right.gameObject.SetActive(item != PlayerItem.Flashlight && (present || rightClip != null || retiringPaper));
            ApplySocket(leftGrip, leftClip, time, reducedMotion ? default : leftSway);
            ApplySocket(rightGrip, rightClip, rightTime, reducedMotion ? default : rightSway);
            if (!reducedMotion)
            { leftGrip.localPosition += Vector3.down * stepImpulse; rightGrip.localPosition += Vector3.down * stepImpulse * .75f; }
            rightGrip.localPosition += Vector3.down * (.34f * (1 - equip.Visibility) + .30f * (1 - Ease(0, 1, replenish)));
            if (retiringPaper) rightGrip.localPosition += Vector3.down * (.34f * Ease(0, .30f, paperRetire));
            if (released)
                rightGrip.localPosition += Vector3.down * (.28f * Ease(library.GetClip("Use" + used).releaseTime,
                    library.GetClip("Use" + used).duration, time));
            leftRig.Apply(library.GetPose("Flashlight"), leftClip, time);
            rightRig.Apply(library.GetPose(present ? item.ToString() : "Relaxed"), rightClip, rightTime);
#if UNITY_EDITOR
            if (previewFingerDegrees >= 0) { leftRig.PreviewCurl(previewFingerDegrees); rightRig.PreviewCurl(previewFingerDegrees); }
#endif
            SetPropTransform(item);
            torch.localRotation = Quaternion.Euler(-90, 0, 0);
            torch.rotation = leftRig.PalmRotationDelta * torch.rotation;
            if (held) held.rotation = rightRig.PalmRotationDelta * held.rotation;
            var torchPose = library.GetPose("Flashlight");
            var handleOffset = torchPose.handleOffset == Vector3.zero ? new Vector3(0, .020f, HandleRadius + .001f) : torchPose.handleOffset;
            torch.position = leftRig.Palm + leftRig.PalmRotationDelta * left.TransformVector(handleOffset) - torch.up * HandleCenterY;
            torchContact.Correct(leftRig, skipThumb: leftClip != null, maxDegrees: 6);
            AlignProps(item);
            if (owned && used == PlayerItem.Trap && item == PlayerItem.Trap) MoveTrapToFloor(actionOwner, time);
            if (held) held.gameObject.SetActive(!released);
            if (item == PlayerItem.Snapper && held && !released) PredictSnapper(sideScale, reducedMotion ? default : rightSway);
            foreach (var material in materials) if (material) material.SetFloat("_Monochrome", burning ? 1 : 0);
            foreach (var material in heldMaterials) if (material)
            { material.SetFloat("_Burn", burnFraction); material.SetFloat("_PaperTime", age); }
            DrawFlames(burnFraction);
            paperEffects?.Advance(age, delta, burnFraction, viewCamera, paperBurning);
            UpdatePhysicalTorch();
        }
        static void ApplySocket(Transform socket, HandAnimationLibrary.Clip clip, float time, HandMotionSample motion)
        {
            socket.localPosition += new Vector3(motion.OffsetH, motion.OffsetV, motion.OffsetZ);
            socket.localRotation *= Quaternion.Euler(motion.LookPitch, motion.LookYaw, motion.Roll);
            if (clip != null) { socket.localPosition += clip.Position(time); socket.localRotation *= clip.Rotation(time); }
        }
        void SetPropTransform(PlayerItem item)
        {
            if (!held) return;
            held.localScale = item == PlayerItem.Snapper ? Vector3.one * .85f : Vector3.one;
            switch (item)
            {
                case PlayerItem.Talisman: held.localPosition = new Vector3(-.035f, .17f, -.005f); held.localRotation = Quaternion.Euler(0, 165, -8); break;
                case PlayerItem.Incense: held.localPosition = new Vector3(-.025f, .04f, .025f); held.localRotation = Quaternion.Euler(-5, 0, -8); break;
                case PlayerItem.Trap: held.localPosition = new Vector3(-.02f, .03f, .045f); held.localRotation = Quaternion.Euler(0, 0, -8); break;
                default: held.localPosition = Vector3.zero; held.localRotation = Quaternion.Euler(12, 0, -10); break;
            }
        }
        void AlignProps(PlayerItem item)
        {
            if (!held) return;
            switch (item)
            {
                case PlayerItem.Talisman:
                    // Hang from an upper corner. The upper edge stays pinned
                    // while the free lower sheet flexes in the paper shader.
                    held.position = rightRig.Pinch - held.up * (heldBounds.max.y - .008f) + held.right * heldBounds.size.x * .28f; break;
                case PlayerItem.Incense:
                    held.position = rightRig.Pinch + held.up * (heldBounds.size.y * .5f - .035f); break;
                case PlayerItem.Snapper: held.position = rightRig.Pinch; break;
                case PlayerItem.Trap: held.position = rightRig.Palm - held.up * heldBounds.min.y; break;
            }
        }
        void MoveTrapToFloor(PlayerHandActions owner, float time)
        {
            var clip = library.GetClip("UseTrap");
            float blend = time <= clip.releaseTime ? Ease(.14f, clip.releaseTime, time) : 1 - Ease(clip.releaseTime, clip.duration, time);
            Vector3 floor = owner.Placement.position + Vector3.up * .006f;
            if (player.eyes.WorldToViewportPoint(floor).z <= .08f) return;
            Vector3 targetPoint = ViewPoint(floor);
            Quaternion targetRotation = viewCamera.transform.rotation * Quaternion.Inverse(player.eyes.transform.rotation) * owner.Placement.rotation;
            Vector3 source = rightRig.Palm - (rightGrip.rotation * Quaternion.Euler(0, 0, -8) * Vector3.up) * heldBounds.min.y;
            var offset = (targetPoint - source) * blend;
            rightGrip.position += offset;
            if (held)
            {
                held.position = source + offset;
                held.rotation = Quaternion.Slerp(held.rotation, targetRotation, blend);
                float lens = Mathf.Tan(viewCamera.fieldOfView * Mathf.Deg2Rad * .5f) / Mathf.Tan(player.eyes.fieldOfView * Mathf.Deg2Rad * .5f);
                held.localScale = Vector3.one * Mathf.Lerp(1, lens, blend);
            }
        }
        Vector3 predictedSnapperPoint, predictedSnapperDirection;
        bool predictedSnapper;
        void PredictSnapper(float sideScale, HandMotionSample motion)
        {
            var clip = library.GetClip("UseSnapper");
            var socketPosition = rightGrip.localPosition; var socketRotation = rightGrip.localRotation;
            var propPosition = held.position; var propRotation = held.rotation;
            rightRig.SavePose();
            rightGrip.localPosition = new Vector3(.315f * sideScale, -.245f, .50f);
            rightGrip.localRotation = Quaternion.identity;
            ApplySocket(rightGrip, clip, clip.releaseTime, motion);
            rightRig.Apply(library.GetPose("Snapper"), clip, clip.releaseTime);
            held.position = rightRig.Pinch;
            predictedSnapperPoint = WorldPoint(held.position);
            predictedSnapperDirection = WorldDirection(rightGrip.forward);
            predictedSnapper = true;
            rightRig.RestorePose();
            rightGrip.SetLocalPositionAndRotation(socketPosition, socketRotation);
            held.SetPositionAndRotation(propPosition, propRotation);
        }
        Vector3 WorldPoint(Vector3 point)
        {
            var viewport = viewCamera.WorldToViewportPoint(point);
            return player && player.eyes ? player.eyes.ViewportToWorldPoint(viewport) : viewCamera.transform.InverseTransformPoint(point);
        }
        Vector3 ViewPoint(Vector3 point) => viewCamera.ViewportToWorldPoint(player.eyes.WorldToViewportPoint(point));
        Vector3 WorldDirection(Vector3 direction)
        {
            if (!player || !player.eyes) return direction.normalized;
            var local = viewCamera.transform.InverseTransformDirection(direction);
            float lens = Mathf.Tan(player.eyes.fieldOfView * Mathf.Deg2Rad * .5f) / Mathf.Tan(viewCamera.fieldOfView * Mathf.Deg2Rad * .5f);
            local.x *= lens * player.eyes.aspect / viewCamera.aspect; local.y *= lens;
            return player.eyes.transform.TransformDirection(local).normalized;
        }
        public bool TryGetSnapperLaunch(out Vector3 point, out Vector3 forward)
        {
            point = predictedSnapperPoint; forward = predictedSnapperDirection;
            if (!studio || !predictedSnapper || displayed != PlayerItem.Snapper || !held) return false;
            if (player && player.HandActions && player.HandActions.CommitPending && player.HandActions.PendingItem == PlayerItem.Snapper)
            { point = WorldPoint(held.position); forward = WorldDirection(rightGrip.forward); }
            return true;
        }
        public bool TryGetHeldRootWorld(PlayerItem item, out Vector3 point)
        {
            point = default;
            if (!studio || displayed != item || !held || !held.gameObject.activeInHierarchy) return false;
            point = WorldPoint(held.position); return true;
        }
        public string ContactDiagnostics => torchContact == null ? "Unprepared" : JsonUtility.ToJson(new ContactReport {
            digits = new List<HandCylinderContact.Diagnostic>(torchContact.LastDiagnostics).ToArray() });
        [Serializable] sealed class ContactReport { public HandCylinderContact.Diagnostic[] digits; }
        void UpdatePhysicalTorch()
        {
            if (!player || !player.eyes || !player.flashlight || player.Hidden) return;
            // The visual hand is a separate close-up camera and can sway away
            // from the reticle. Place the physical spot at the gameplay eye and
            // aim along its centre ray: it stays centred at every distance and
            // cannot be projected through a nearby wall by the hand camera.
            var eye = player.eyes;
            var centre = eye.ViewportPointToRay(new Vector3(.5f, .5f, 0));
            player.flashlight.transform.SetPositionAndRotation(eye.transform.position,
                Quaternion.LookRotation(centre.direction, eye.transform.up));
        }
        public void RefreshAfterCommit()
        {
            if (!studio || !player || !GameSession.Current) return;
            bool burning = player.Rituals.Burning;
            var item = burning ? PlayerItem.Talisman : player.Items.Selected;
            DrawPose(item, burning, HasItem(item, burning, true), burning ? player.Rituals.BurnedFraction : 0,
                0, player.ActualSpeed > .1f, player.Running, GameSession.Current.Shell.ReducedMotion);
        }
        void AdvanceMotion(float delta, bool running)
        {
            if (!player || !player.eyes || delta <= 0) return;
            var position = player.transform.position;
            float yaw = player.eyes.transform.eulerAngles.y, pitch = player.eyes.transform.eulerAngles.x;
            var displacement = position - previousPlayerPosition;
            if (!motionReady || displacement.sqrMagnitude > 4)
            { leftMotion.Reset(); rightMotion.Reset(); displacement = Vector3.zero; previousYaw = yaw; previousPitch = pitch; motionReady = true; }
            float speed = Mathf.Max(.1f, running ? player.runSpeed : player.walkSpeed);
            var local = player.transform.InverseTransformDirection(displacement / delta) / speed;
            float turn = Mathf.DeltaAngle(previousYaw, yaw) / delta, tilt = Mathf.DeltaAngle(previousPitch, pitch) / delta;
            leftSway = leftMotion.Advance(delta, local.z, local.x, turn, tilt, running, 1.1f);
            float mass = player.Items.Selected == PlayerItem.Trap ? 2.4f : player.Items.Selected == PlayerItem.Incense ? .7f : .85f;
            rightSway = rightMotion.Advance(delta, local.z, local.x, turn, tilt, running, mass);
            previousPlayerPosition = position; previousYaw = yaw; previousPitch = pitch;
            stepImpulse *= Mathf.Exp(-24 * delta);
        }
        void OnFootstep(Vector3 point, bool wet, float strength)
        { stepImpulse = Mathf.Clamp(strength * .0025f, .0006f, .0045f); }
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
        // CPU-only geometry review: no RenderTexture, draw call or play session.
        public Camera PrepareCpuPreview(PlayerItem item, float burnFraction = -1, float actionProgress = -1, float aspect = 16f / 9)
        {
            if (!studio) Prepare();
            lastReleased = false; replenish = 1; paperRetire = -1; cosmeticAction.Reset(); equip.Reset((int)item);
            viewCamera.enabled = false; viewCamera.aspect = aspect;
            age = 1.3f; previewFingerDegrees = -1;
            actionItem = item; previewActionTime = actionProgress < 0 ? -1 : library.GetClip("Use" + item).duration * Mathf.Clamp01(actionProgress);
            DrawPose(item, burnFraction >= 0, true, Mathf.Max(0, burnFraction), 0, false, false, true);
            return viewCamera;
        }
        // Opt-in still rendering; no player launch or game/session clock is needed.
        public Camera PrepareEditorPreview(PlayerItem item, float burnFraction, float actionProgress = -1, float fingerDegrees = -1)
        {
            if (!studio) Prepare();
            lastReleased = false; replenish = 1; cosmeticAction.Reset(); equip.Reset((int)item);
            EnsureTarget(1280, 720);
            age = 1.3f; previewFingerDegrees = fingerDegrees;
            actionItem = item; previewActionTime = actionProgress < 0 ? -1 : library.GetClip("Use" + item).duration * Mathf.Clamp01(actionProgress);
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
            if (footsteps) footsteps.Footstep -= OnFootstep;
            paperEffects?.Dispose();
            image?.RemoveFromHierarchy();
            if (target) { target.Release(); Discard(target); }
            foreach (var material in materials) Discard(material);
            Discard(flameMaterial);
            if (studio) Discard(studio.gameObject);
        }
    }
}











