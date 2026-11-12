using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Modular level geometry and existing PBR surfaces; no gameplay state lives here.
    [DisallowMultipleComponent]
    public sealed class AlleyApproachWorld : MonoBehaviour
    {
        public static readonly Vector3 Start = new Vector3(500, .04f, 386);
        public static readonly Vector3 Barrier = new Vector3(500, 0, 405);
        public static readonly Vector3 Exit = new Vector3(500, .04f, 414);
        readonly GraphicsSurfaceLibrary.Pool surfaces = new GraphicsSurfaceLibrary.Pool();
        readonly List<Mesh> wallMeshes = new List<Mesh>();
        Material storeSignMaterial;
        GameObject walkInStorefront;
        bool authoredGateReady, rightWeatheredReady;

        public static AlleyApproachWorld Create(Transform parent)
        {
            if (!parent) throw new ArgumentNullException(nameof(parent));
            var root = new GameObject("Korean alley approach");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(500, 0, 400);
            var world = root.AddComponent<AlleyApproachWorld>();
            world.Build();
            var temple = parent.GetComponentInParent<TemplePrologueWorld>();
            if (temple && GameSession.Current && GameSession.Current.player && GameSession.Current.player.eyes)
                root.AddComponent<AlleyDuskEnvironment>().Prepare(GameSession.Current.player.eyes, temple);
            return world;
        }

        void Build()
        {
            walkInStorefront = Resources.Load<GameObject>("AlleyStorefrontInterior/StorefrontDoorCandidate");
            authoredGateReady = gameObject.AddComponent<AlleyAuthoredGate>().TryBuild(RoadBend);
            var road = surfaces.Get("concrete-rough", new Color(.105f, .10f, .095f));
            // Old dry pavement should not reflect the sunset like a wet floor.
            road.SetFloat("_Smoothness", .65f);
            var wall = surfaces.Get("plaster-damp", new Color(.82f, .78f, .71f));
            var concreteWall = surfaces.Get("alley-concrete-wall", new Color(.84f, .83f, .80f));
            var formedWall = surfaces.Get("alley-form-wall", new Color(.83f, .82f, .78f));
            var brick = surfaces.Get("alley-red-brick", new Color(.90f, .84f, .78f));
            var agedBrick = surfaces.Get("alley-aged-brick", new Color(.88f, .87f, .84f));
            var dark = surfaces.Get("wood-aged", new Color(.28f, .22f, .18f));
            var roof = surfaces.Get("concrete-rough", new Color(.21f, .20f, .21f));
            var wallCoping = surfaces.Get("alley-concrete-wall", new Color(.66f, .64f, .60f));
            var iron = surfaces.Get("painted-metal", new Color(.22f, .23f, .22f));
            var red = surfaces.Get("cloth-charred", new Color(.54f, .24f, .19f));
            var cream = surfaces.Get("cloth-charred", new Color(.72f, .64f, .51f));
            // Carry the pavement under the rear wall so the sunset sky cannot
            // leak through the former 20 cm gap at the foot of the gate.
            Box("Alley pavement", new Vector3(0, -.12f, -1.55f), new Vector3(10.5f, .24f, 32.1f), road, true);
            gameObject.AddComponent<AlleyRoadDressing>().Prepare();
            var pine = Resources.Load<GameObject>("TemplePrologue/pine-trunk-review");
            if (pine)
                foreach (float side in new[] { -1f, 1f })
                    for (int i = 0; i < 2; i++)
                    {
                        var tree = Instantiate(pine, transform, false);
                        tree.name = "Mountain forest visible beyond alley";
                        // The first right-hand silhouette must sit beyond
                        // the last village roof at the mountain seam.
                        float treeX = side > 0 && i == 0 ? 8.4f : 5.8f + i * 2;
                        float treeZ = side > 0 && i == 0 ? 23f : 20 + i * 5;
                        tree.transform.localPosition = new Vector3(side * treeX, -.1f, treeZ);
                        tree.transform.localRotation = Quaternion.Euler(0, side * 45 + i * 110, 0);
                        foreach (var renderer in tree.GetComponentsInChildren<Renderer>(true))
                            renderer.shadowCastingMode = ShadowCastingMode.Off;
                    }
            for (int i = 0; i < 5; i++)
            {
                float z = -12 + i * 6;
                foreach (float side in new[] { -1f, 1f })
                {
                    float height = i == 4 ? 2.1f : (side < 0 && i < 2 ? 2.8f : 2.35f);
                    // A different property uses formed concrete while the
                    // adjacent wall remains the older weathered plaster.
                    var wallFinish = side < 0 && i == 3 ? formedWall :
                        (side > 0 && i == 3 ? agedBrick :
                            (side > 0 && i == 0 ? brick : (side > 0 && i == 1 ? agedBrick : (i >= 2 ? concreteWall : wall))));
                    if (walkInStorefront && side < 0 && (i == 0 || i == 1))
                        ShopDoorWallSegments(z - 3, z + 3, height, wall);
                    else if (i < 2)
                        Box("Weathered residential wall", new Vector3(side * 3.2f, height * .5f, z), new Vector3(.35f, height, 6), wallFinish, true);
                    else
                        CurvedWall(side, z - 3, z + 3, height, wallFinish, wallCoping);
                    if (i < 2)
                        Box("Wall tile coping", new Vector3(side * 3.2f, height + .04f, z), new Vector3(.45f, .12f, 6), wallCoping, false);
                    float upperHeight = 1.3f + (i % 3) * .3f;
                    Box("House facade above wall", new Vector3(side * (4.75f + .15f * (i % 2)), height + upperHeight * .5f, z + .3f),
                        new Vector3(2.6f, upperHeight, 4.8f), i % 2 == 0 ? wall : dark, false);
                    TiltedBox("Sloping dark tile roof", new Vector3(side * 4.7f, height + upperHeight + .17f, z + .3f),
                        new Vector3(3.6f, .18f, 5.4f), Quaternion.Euler(0, 0, side * 12), roof);
                    for (int tile = 0; tile < 7; tile++)
                        TiltedBox("Roof tile ridge", new Vector3(side * 4.7f, height + upperHeight + .27f, z - 2.2f + tile * .82f),
                            new Vector3(3.65f, .08f, .08f), Quaternion.Euler(0, 0, side * 12), roof);
                }
            }
            // A shallow shop front is part of the streetscape, not a generated asset.
            Box("Shop frontage", new Vector3(-3.02f, 1.38f, -8), new Vector3(.18f, 2.75f, 5.1f), dark, false);
            Box("Shop upper sign", new Vector3(-2.88f, 2.5f, -8), new Vector3(.12f, .55f, 5.1f), wall, false);
            var sign = new GameObject("우암상회 hand-painted sign");
            sign.layer = 8; sign.transform.SetParent(transform, false);
            sign.transform.localPosition = new Vector3(-2.76f, 2.48f, -8);
            sign.transform.localRotation = Quaternion.Euler(0, 90, 0);
            var lettering = sign.AddComponent<TextMesh>();
            lettering.font = Resources.Load<Font>("Fonts/PauseMyeongjo");
            if (!lettering.font) throw new InvalidOperationException("Korean sign font is missing");
            lettering.GetComponent<MeshRenderer>().sharedMaterial = lettering.font.material;
            lettering.text = "우 암 상 회"; lettering.anchor = TextAnchor.MiddleCenter;
            lettering.alignment = TextAlignment.Center; lettering.fontSize = 90;
            lettering.characterSize = .008f; lettering.color = new Color(.18f, .10f, .075f);
            // A perpendicular sign faces people entering from the near end;
            // it complements the long sign parallel to the shop frontage.
            Box("Projecting shop sign frame", new Vector3(-2.63f, 2.75f, -11.2f), new Vector3(.94f, .94f, .10f), dark, false);
            var signTemplate = Resources.Load<Material>("GraphicsPbr/paper-aged/material-emissive");
            var signTexture = Resources.Load<Texture2D>("TempleApproach/alley-store-sign");
            if (!signTemplate) throw new InvalidOperationException("Missing shop sign template: GraphicsPbr/paper-aged/material-emissive");
            if (!signTexture) throw new InvalidOperationException("Missing shop sign texture: TempleApproach/alley-store-sign");
            storeSignMaterial = new Material(signTemplate) { name = "Alley shop baked sign" };
            storeSignMaterial.SetColor("_BaseColor", Color.white);
            storeSignMaterial.SetTexture("_BaseMap", signTexture);
            storeSignMaterial.SetTexture("_EmissionMap", signTexture);
            storeSignMaterial.SetColor("_EmissionColor", new Color(.48f, .40f, .30f));
            storeSignMaterial.EnableKeyword("_EMISSION");
            for (int face = 0; face < 2; face++)
            {
                var panel = GameObject.CreatePrimitive(PrimitiveType.Quad);
                panel.name = "Baked Korean shop sign face"; panel.layer = 8;
                panel.transform.SetParent(transform, false);
                // Each face must sit outside its own side of the frame.
                // The old positive-Z face was buried behind the opaque box.
                panel.transform.localPosition = new Vector3(-2.63f, 2.75f, -11.2f + (face == 0 ? -.065f : .065f));
                panel.transform.localRotation = Quaternion.Euler(0, face * 180, 0);
                panel.transform.localScale = Vector3.one * .84f;
                var signRenderer = panel.GetComponent<Renderer>(); signRenderer.sharedMaterial = storeSignMaterial;
                signRenderer.shadowCastingMode = ShadowCastingMode.Off;
                Destroy(panel.GetComponent<Collider>());
            }
            for (int i = 0; i < 10; i++)
            {
                TiltedBox("Striped shop awning", new Vector3(-2.63f, 2.19f, -10.25f + i * .5f),
                    new Vector3(1.10f, .09f, .50f), Quaternion.Euler(0, 0, -12), i % 2 == 0 ? red : cream);
                Box("Weathered awning hanging edge", new Vector3(-2.09f, 2.02f, -10.25f + i * .5f),
                    new Vector3(.04f, .20f, .50f), i % 2 == 0 ? red : cream, false);
            }
            for (int post = 0; post < 4; post++)
                Box("Shop timber post", new Vector3(-2.64f, 1.08f, -10.40f + post * 1.48f), new Vector3(.16f, 2.16f, .10f), dark, false);
            for (int i = 0; i < 3; i++)
            {
                Box("Shop window frame", new Vector3(-2.88f, 1.25f, -9.65f + i * 1.5f), new Vector3(.08f, 1.65f, 1.2f), iron, false);
                Box("Shop warm window", new Vector3(-2.82f, 1.25f, -9.65f + i * 1.5f), new Vector3(.04f, 1.36f, .95f),
                    surfaces.Get("painted-metal", new Color(.56f, .37f, .20f), new Color(.75f, .31f, .08f)), false);
                for (int shelf = 0; shelf < 3; shelf++)
                {
                    float shelfY = .78f + shelf * .38f;
                    Box("Window shelf", new Vector3(-2.77f, shelfY, -9.65f + i * 1.5f), new Vector3(.10f, .045f, .92f), dark, false);
                    for (int item = 0; item < 4; item++)
                        Cylinder("Store jar", new Vector3(-2.73f, shelfY + .095f, -10.0f + i * 1.5f + item * .22f),
                            new Vector3(.095f, .075f, .095f), item % 2 == 0 ? cream : red, false);
                }
                Box("Window cross rail", new Vector3(-2.68f, 1.24f, -9.65f + i * 1.5f), new Vector3(.09f, .065f, 1.08f), dark, false);
                Box("Window center mullion", new Vector3(-2.67f, 1.24f, -9.65f + i * 1.5f), new Vector3(.09f, 1.68f, .065f), dark, false);
            }
            TiltedBox("Damaged window loose timber", new Vector3(-2.59f, .65f, -9.7f),
                new Vector3(.08f, 1.08f, .08f), Quaternion.Euler(17, 0, 0), dark);
            Box("Store display low projecting ledge", new Vector3(-2.51f, .47f, -8.1f), new Vector3(.50f, .13f, 4.4f), dark, false);
            Box("Shop entrance frame", new Vector3(-2.81f, 1.04f, -5.66f), new Vector3(.12f, 2.08f, 1.06f), iron, false);
            Box("Shop entrance wood door", new Vector3(-2.72f, .99f, -5.66f), new Vector3(.08f, 1.94f, .88f), dark, false);
            Box("Shop entrance upper window", new Vector3(-2.67f, 1.45f, -5.66f), new Vector3(.035f, .74f, .65f),
                surfaces.Get("painted-metal", new Color(.56f, .37f, .20f), new Color(.75f, .31f, .08f)), false);
            Box("Shop entrance handle", new Vector3(-2.62f, .98f, -5.95f), new Vector3(.05f, .23f, .04f), iron, false);
            // Reuse a real project prop as the shop's exterior display.
            var freezer = Resources.Load<GameObject>("AlleyFreezer/Freezer");
            var cabinet = freezer ? freezer : Resources.Load<GameObject>("SchoolRenewal/metal-cabinet/metal-cabinet-visual");
            if (cabinet)
            {
                var holder = CorridorEntranceModels.Group(transform, freezer ? "Shop old ice cream freezer" : "Shop old metal cabinet", Vector3.zero);
                Instantiate(cabinet, holder, false);
                CorridorEntranceModels.GroundAtHeight(holder, new Vector3(-2.38f, 0, -11.8f), freezer ? .85f : .72f, Quaternion.Euler(0, 90, 0));
                foreach (var node in holder.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
            }
            var shopGlow = new GameObject("Warm shop entrance light"); shopGlow.transform.SetParent(transform, false);
            shopGlow.transform.localPosition = new Vector3(-2.3f, 2.12f, -8);
            var shopLight = shopGlow.AddComponent<Light>(); shopLight.type = LightType.Point;
            shopLight.color = new Color(1, .55f, .24f); shopLight.range = 6; shopLight.intensity = 4f;
            shopLight.shadows = LightShadows.None;
            for (int bulb = 0; bulb < 2; bulb++)
                Box("Warm shop bulb", new Vector3(-2.48f, 2.12f, -9.5f + bulb * 2.8f), new Vector3(.10f, .15f, .10f),
                    surfaces.Get("painted-metal", new Color(.9f, .7f, .35f), new Color(1.3f, .65f, .18f)), false);
            // Keep the straight gate fully within the straight wall. The
            // old position straddled the bend and stood out in the lane.
            foreach (float z in new[] { -11.7f, -4.4f, 4f, 11f })
            {
                // A gate is rigid, so align it to the average wall tangent
                // across its width rather than leaving it straight as the
                // wall curves away from each edge.
                float slope = (RoadBend(z + .875f) - RoadBend(z - .875f)) / 1.75f;
                Quaternion rotation = Quaternion.Euler(0, Mathf.Atan(slope) * Mathf.Rad2Deg, 0);
                Vector3 pivot = new Vector3(3.01f + RoadBend(z), 0, z);
                void GatePiece(string label, Vector3 offset, Vector3 size, Material material)
                {
                    var piece = Box(label, pivot + rotation * offset, size, material, false);
                    piece.transform.localRotation = rotation;
                    if (authoredGateReady && Mathf.Approximately(z, 4f)) piece.GetComponent<Renderer>().enabled = false;
                }
                // Older neighboring gates were painted independently rather
                // than sharing an identical black factory finish.
                var gateFinish = Mathf.Approximately(z, 4f) ?
                    surfaces.Get("painted-metal", new Color(.42f, .56f, .44f)) :
                    (Mathf.Approximately(z, 11f) ? surfaces.Get("painted-metal", new Color(.44f, .48f, .53f)) : iron);
                GatePiece("House gate", new Vector3(0, 1.06f, 0), new Vector3(.08f, 2.12f, 1.75f), gateFinish);
                for (int bar = 0; bar < 7; bar++)
                    GatePiece("Gate vertical rail", new Vector3(-.06f, 1.08f, -.7f + bar * .23f), new Vector3(.05f, 1.9f, .035f), dark);
                foreach (float y in new[] { .22f, 1.05f, 1.98f })
                    GatePiece("Gate horizontal rail", new Vector3(-.08f, y, 0), new Vector3(.05f, .055f, 1.73f), dark);
                foreach (float offset in new[] { -.9f, .9f })
                    GatePiece("Gate edge frame", new Vector3(-.05f, 1.06f, offset), new Vector3(.08f, 2.18f, .08f), dark);
                // A pair of solid leaves with their seam, pull and hinge plates
                // reads as a usable old steel door at player height.
                var hardware = surfaces.Get("metal-rust", new Color(.45f, .39f, .32f));
                GatePiece("Gate meeting seam", new Vector3(-.105f, 1.06f, 0), new Vector3(.025f, 1.94f, .025f), dark);
                GatePiece("Gate latch plate", new Vector3(-.12f, 1.02f, -.09f), new Vector3(.025f, .17f, .10f), hardware);
                GatePiece("Gate door pull", new Vector3(-.15f, 1.02f, -.09f), new Vector3(.04f, .105f, .025f), iron);
                foreach (float hingeZ in new[] { -.78f, .78f })
                    foreach (float hingeY in new[] { .34f, 1.74f })
                        GatePiece("Gate hinge plate", new Vector3(-.11f, hingeY, hingeZ),
                            new Vector3(.025f, .13f, .065f), hardware);
            }
            for (int i = 0; i < 4; i++)
            {
                // Keep the first pole clear of the authored tiled wall.
                float z = i == 0 ? -8.2f : -10 + i * 8;
                Cylinder("Utility pole", new Vector3(2.76f, 2.7f, z), new Vector3(.18f, 2.7f, .18f), iron, true);

                var light = new GameObject("Warm alley lamp"); light.transform.SetParent(transform, false);
                light.transform.localPosition = new Vector3(2.47f, 3.3f, z);
                var lamp = light.AddComponent<Light>(); lamp.type = LightType.Point; lamp.color = new Color(1, .66f, .35f);
                lamp.range = 6; lamp.intensity = .8f; lamp.shadows = LightShadows.None;
                Box("Lamp arm", new Vector3(2.57f, 3.34f, z), new Vector3(.45f, .07f, .07f), iron, false);
            }
            var servicePoles = new Vector3[2];
            float[] servicePoleZ = { -1f, 8.5f };
            for (int i = 0; i < servicePoles.Length; i++)
            {
                float z = servicePoleZ[i], x = -3.65f + RoadBend(z);
                // Secondary poles sit behind the wall, with no new collision.
                Cylinder("Secondary village utility pole", new Vector3(x, 2.65f, z),
                    new Vector3(.14f, 2.65f, .14f), wallCoping, false);
                transform.GetChild(transform.childCount - 1).GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                servicePoles[i] = new Vector3(x, 5.27f, z);
            }
            gameObject.AddComponent<AlleyUtilityDressing>().Prepare(servicePoles);
            float noticeZ = 1.8f;
            float noticeSlope = (RoadBend(noticeZ + .02f) - RoadBend(noticeZ - .02f)) / .04f;
            Vector3 noticeInward = new Vector3(1, 0, -noticeSlope).normalized;
            Vector3 noticePoint = new Vector3(-3.2f + RoadBend(noticeZ), 1.48f, noticeZ) + noticeInward * .198f;
            var wallDetails = gameObject.AddComponent<AlleyWallDressing>();
            // The generated right wall projects farther into the lane than
            // the old skin; this is the measured pipe-to-mesh contact at z=1.4.
            float rightPipeFace = (rightWeatheredReady ? 2.837f : 3.025f) + RoadBend(1.4f);
            wallDetails.Prepare(
                new Vector3(-3.025f + RoadBend(.7f), 0, .7f),
                new Vector3(rightPipeFace, 0, 1.4f), noticePoint, noticeInward);
            foreach (var entry in new[] { (z: -5.7f, number: "23-4"), (z: 7.2f, number: "18") })
            {
                float slope = (RoadBend(entry.z + .02f) - RoadBend(entry.z - .02f)) / .04f;
                var inward = new Vector3(-1, 0, slope).normalized;
                var point = new Vector3(3.2f + RoadBend(entry.z), 1.67f, entry.z) + inward * .195f;
                wallDetails.AddHouseNumber(point, inward, entry.number);
            }
            // An old delivery flyer belongs to a separate property and sits
            // against the inward-facing brick wall, away from its number plate.
            const float flyerZ = 8.45f;
            float flyerSlope = (RoadBend(flyerZ + .02f) - RoadBend(flyerZ - .02f)) / .04f;
            var flyerInward = new Vector3(-1, 0, flyerSlope).normalized;
            var flyerPoint = new Vector3(3.2f + RoadBend(flyerZ), 1.48f, flyerZ) + flyerInward * .205f;
            wallDetails.AddCoalNotice(flyerPoint, flyerInward);
            // Small translucent runoff patches follow individual straight
            // segments of the curved wall rather than spanning its bends.
            foreach (float z in new[] { .5f, 6.5f, 12.5f })
            {
                float a = RoadBend(z - .5f), b = RoadBend(z + .5f);
                var inward = new Vector3(1, 0, -(b - a)).normalized;
                var point = new Vector3(-3.2f + (a + b) * .5f, z > 10 ? 1.50f : 1.76f, z) + inward * .205f;
                wallDetails.AddRunoff(point, inward, new Vector2(.82f, 1.34f), z == 6.5f);
            }
            // Weathering on the opposite boundary makes the two sides read
            // as independently maintained residential properties.
            foreach (float z in new[] { -1f, 7.4f, 14f })
            {
                float a = RoadBend(z - .5f), b = RoadBend(z + .5f);
                var inward = new Vector3(-1, 0, b - a).normalized;
                var point = new Vector3(3.2f + (a + b) * .5f, 1.55f, z) + inward * .205f;
                wallDetails.AddRunoff(point, inward, new Vector2(.72f, 1.18f), z != 7.4f);
            }
            Box("Alley rear boundary", new Vector3(0, 1.4f, -17.2f), new Vector3(8, 2.8f, .3f), wall, true);
            var rearBoundary = transform.GetChild(transform.childCount - 1);
            var rearSections = GetComponent<AlleyWeatheredWallSections>();
            if (!rearSections) rearSections = gameObject.AddComponent<AlleyWeatheredWallSections>();
            var rearGate = GetComponent<AlleyAuthoredGate>();
            bool hasRearGate = rearGate && rearGate.TryBuildRear(new Vector3(0, 0, -17.2f), 8f, 2.8f);
            if (hasRearGate)
            {
                var corner = Box("Rear wall corner pier", new Vector3(-3.85f, 1.1f, -17.2f),
                    new Vector3(.36f, 2.2f, .38f), concreteWall, false);
                var cornerRenderer = corner.GetComponent<Renderer>();
                cornerRenderer.shadowCastingMode = ShadowCastingMode.Off;
                cornerRenderer.receiveShadows = false;
            }
            if (hasRearGate || rearSections.TryBuildRear(new Vector3(0, 0, -17.2f), 8f, 2.8f))
                rearBoundary.GetComponent<Renderer>().enabled = false;
            InstallKoreanWallCandidate();
            InstallResidentialHouses();
            InstallStorefront();
            if (walkInStorefront) BuildShopInterior(wall, dark);
        }

        void InstallKoreanWallCandidate()
        {
            var prefab = Resources.Load<GameObject>("AlleyKoreanWall/KoreanWall");
            if (!prefab) return;
            var holder = CorridorEntranceModels.Group(transform, "Authored Korean wall candidate", Vector3.zero);
            Instantiate(prefab, holder, false);
            CorridorEntranceModels.GroundAtHeight(holder, new Vector3(3.2f, 0, -12f), 2.35f, Quaternion.Euler(0, -90, 0));
            var bounds = MonsterPresentationScale.Bounds(holder);
            if (bounds.size.z < .05f) { Destroy(holder.gameObject); return; }
            var scale = holder.localScale;
            scale.z *= 6f / bounds.size.z;
            holder.localScale = scale;
            bounds = MonsterPresentationScale.Bounds(holder);
            Vector3 target = transform.TransformPoint(new Vector3(3.2f, 0, -12f));
            holder.position += new Vector3(target.x - bounds.center.x, target.y - bounds.min.y, target.z - bounds.center.z);
            foreach (var node in holder.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
            // Hide only the old visual surfaces; their collision remains active.
            foreach (Transform child in transform)
            {
                bool wallPart = child.name == "Weathered residential wall" || child.name == "Wall tile coping";
                bool gatePart = child.name == "House gate" || child.name == "Gate vertical rail" ||
                    child.name == "Gate horizontal rail" || child.name == "Gate edge frame" ||
                    child.name == "Gate meeting seam" || child.name == "Gate latch plate" ||
                    child.name == "Gate door pull" || child.name == "Gate hinge plate";
                if (child.localPosition.x > 2.8f && child.localPosition.z > -15f && child.localPosition.z < -9f && (wallPart || gatePart))
                {
                    var renderer = child.GetComponent<Renderer>();
                    if (renderer) renderer.enabled = false;
                }
            }
        }
        void InstallResidentialHouses()
        {
            var prefab = Resources.Load<GameObject>("AlleyBrickHouse/BrickHousePbr") ??
                Resources.Load<GameObject>("AlleyBrickHouse/BrickHouse");
            if (!prefab) return;
            // The walls were just created and rotated in this same frame.
            // Refresh physics bounds before using them for house placement.
            Physics.SyncTransforms();
            // The existing colliding garden walls remain the navigation boundary.
            foreach (Transform child in transform)
            {
                bool oldHouse = child.name == "House facade above wall" || child.name == "Sloping dark tile roof" || child.name == "Roof tile ridge";
                if (oldHouse && (child.localPosition.z > -3f || child.localPosition.x > 3f))
                {
                    var renderer = child.GetComponent<Renderer>();
                    if (renderer) renderer.enabled = false;
                }
            }
            var alternate = Resources.Load<GameObject>("AlleyPlasterHouse/PlasterHousePbr") ??
                Resources.Load<GameObject>("AlleyPlasterHouse/PlasterHouse");
            var nearWindowRepair = Resources.Load<GameObject>("AlleyCompactBrickHouse/geometry-ground-window");
            var originalCompactMaterial = Resources.Load<Material>("AlleyCompactBrickHouse/CompactBrickHousePbr");
            // Avoid loading a second 125k-face copy when the repaired near
            // model and its original material are already available.
            var compact = nearWindowRepair && originalCompactMaterial ? null :
                Resources.Load<GameObject>("AlleyCompactBrickHouse/CompactBrickHousePbr");
            var repairedCompact = Resources.Load<GameObject>("AlleyCompactBrickHouse/geometry-mid-window");
            var farCompact = repairedCompact ? repairedCompact : Resources.Load<GameObject>("AlleyCompactBrickHouse/geometry-mid");
            var farCompactMaterial = repairedCompact ?
                (Resources.Load<Material>("AlleyCompactBrickHouse/CompactBrickHouseWarm") ?? originalCompactMaterial) :
                originalCompactMaterial;
            var slate = Resources.Load<GameObject>("AlleySlateHouse/SlateHousePbr");
            var farSlate = Resources.Load<GameObject>("AlleySlateHouse/geometry-far");
            var farSlateMaterial = Resources.Load<Material>("AlleySlateHouse/SlateHousePbr");
            var twoStory = Resources.Load<GameObject>("AlleyTwoStoryHouse/TwoStoryHouseClean") ??
                Resources.Load<GameObject>("AlleyTwoStoryHouse/TwoStoryHouse");
            // Distant houses reuse the authored PBR materials on their
            // 30k-triangle versions, leaving room for a varied skyline.
            var farHouse = Resources.Load<GameObject>("AlleyTwoStoryHouse/geometry-clean-far") ??
                Resources.Load<GameObject>("AlleyTwoStoryHouse/geometry-far");
            var farMaterial = Resources.Load<Material>("AlleyTwoStoryHouse/TwoStoryHouse");
            var waterTankHouse = Resources.Load<GameObject>("AlleyWaterTankHouse/geometry-high");
            var waterTankMaterial = Resources.Load<Material>("AlleyWaterTankHouse/WaterTankHouse");
            var tiledHouse = Resources.Load<GameObject>("AlleyTiledHouse/geometry-high");
            var tiledMaterial = Resources.Load<Material>("AlleyTiledHouse/TiledHouse");
            var farBrick = Resources.Load<GameObject>("AlleyBrickHouse/geometry-far");
            var farBrickMaterial = Resources.Load<Material>("AlleyBrickHouse/BrickHousePbr") ??
                Resources.Load<Material>("AlleyBrickHouse/BrickHouse");
            var farPlaster = Resources.Load<GameObject>("AlleyPlasterHouse/geometry-far");
            var farPlasterMaterial = Resources.Load<Material>("AlleyPlasterHouse/PlasterHousePbr") ??
                Resources.Load<Material>("AlleyPlasterHouse/PlasterHouse");
            void PlaceBackground(Transform holder, GameObject full, GameObject reduced, Material material)
            {
                bool useReduced = reduced && material;
                Instantiate(useReduced ? reduced : full, holder, false);
                if (useReduced)
                    foreach (var renderer in holder.GetComponentsInChildren<Renderer>(true))
                        renderer.sharedMaterial = material;
            }
            // Replace the flat foreground block above the right wall with the
            // authored house, keeping its actual mesh outside the lane.
            var foreground = CorridorEntranceModels.Group(transform, "Foreground old Korean house", Vector3.zero);
            Instantiate(slate ? slate : prefab, foreground, false);
            CorridorEntranceModels.GroundAtHeight(foreground, new Vector3(7.0f, 0, -8f), 4.7f, Quaternion.Euler(0, -90, 0));
            var foregroundBounds = MonsterPresentationScale.Bounds(foreground);
            float foregroundClearance = transform.position.x + 4.85f;
            if (foregroundBounds.min.x < foregroundClearance)
                foreground.position += Vector3.right * (foregroundClearance - foregroundBounds.min.x);
            foreach (var node in foreground.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
            var occupiedHouses = new List<Bounds> { MonsterPresentationScale.Bounds(foreground) };
            var aerialHouses = new List<Transform> { foreground };
            void SeparateOutward(Transform holder, float side)
            {
                var bounds = MonsterPresentationScale.Bounds(holder);
                for (int pass = 0; pass < occupiedHouses.Count; pass++)
                {
                    bool moved = false;
                    foreach (var occupied in occupiedHouses)
                    {
                        if (!bounds.Intersects(occupied)) continue;
                        float shift = side > 0 ? occupied.max.x - bounds.min.x + .20f :
                            occupied.min.x - bounds.max.x - .20f;
                        holder.position += Vector3.right * shift;
                        bounds = MonsterPresentationScale.Bounds(holder);
                        moved = true;
                    }
                    if (!moved) break;
                }
                foreach (var occupied in occupiedHouses)
                    if (bounds.Intersects(occupied)) throw new InvalidOperationException("House overlap could not be resolved");
                occupiedHouses.Add(bounds);
            }
            // Separate roofs along the lane before resolving their lateral
            // bounds. A few decimetres of longitudinal overlap previously
            // pushed whole houses many metres away from the street.
            float[] stationOffsets = { 0f, .5f, 2.7f, -.45f, -.2f, 1.6f };
            int houseIndex = 0;
            foreach (float side in new[] { -1f, 1f })
            {
                foreach (float z in new[] { 0f, 8f, 15f })
                {
                    var holder = CorridorEntranceModels.Group(transform, "Old Korean brick house", Vector3.zero);
                    int variant = houseIndex++;
                    bool compactBackground = variant == 5 && farCompact && farCompactMaterial;
                    var selected = compact && variant == 0 ? compact :
                        (twoStory && variant == 3 ? twoStory : (alternate && variant % 2 == 0 ? alternate : prefab));
                    if (variant == 0 && nearWindowRepair && originalCompactMaterial)
                    {
                        var repaired = CorridorEntranceModels.Group(holder, "Compact brick house repaired ground window", Vector3.zero);
                        PlaceBackground(repaired, selected, nearWindowRepair, originalCompactMaterial);
                    }
                    else if (compactBackground) PlaceBackground(holder, selected, farCompact, farCompactMaterial);
                    else Instantiate(selected, holder, false);
                    float height = compactBackground ? 5.6f : (((compact || (nearWindowRepair && originalCompactMaterial)) && variant == 0) ||
                        (twoStory && variant == 3) ? 6f : 4.15f + (variant % 3) * .22f);
                    float setback = 6.65f + (variant % 2) * .25f;
                    float stationZ = z + stationOffsets[variant];
                    // Not every house faces directly across the lane. Show
                    // two side elevations to break the repeated roofline.
                    float yaw = variant == 1 ? 35f : variant == 4 ? 145f :
                        (side < 0 ? 90 : -90) + (variant % 3 - 1) * 4;
                    CorridorEntranceModels.GroundAtHeight(holder,
                        new Vector3(side * setback + RoadBend(stationZ), 0, stationZ), height,
                        Quaternion.Euler(0, yaw, 0));
                    // Seat each generated footprint behind the actual walls
                    // spanning it. A single worst-case lane width pushes houses
                    // unnecessarily far from the outside of the curved street.
                    var bounds = MonsterPresentationScale.Bounds(holder);
                    float clearance = side > 0 ? float.NegativeInfinity : float.PositiveInfinity;
                    foreach (Transform wallNode in transform)
                    {
                        if (wallNode.localPosition.x * side <= 0 ||
                            (wallNode.name != "Curved residential wall" && wallNode.name != "Weathered residential wall")) continue;
                        var wallCollider = wallNode.GetComponent<BoxCollider>();
                        if (!wallCollider || !wallCollider.enabled) continue;
                        var wallBounds = wallCollider.bounds;
                        if (wallBounds.max.z < bounds.min.z || wallBounds.min.z > bounds.max.z) continue;
                        clearance = side > 0 ? Mathf.Max(clearance, wallBounds.max.x + .30f) :
                            Mathf.Min(clearance, wallBounds.min.x - .30f);
                    }
                    if (!float.IsInfinity(clearance))
                    {
                        float nearEdge = side > 0 ? bounds.min.x : bounds.max.x;
                        holder.position += Vector3.right * (clearance - nearEdge);
                    }
                    // Leave a small yard between the last left-hand
                    // house and the existing forest tree roots at the seam.
                    if (variant == 2)
                    {
                        var edge = MonsterPresentationScale.Bounds(holder).max.x;
                        float forestClearance = transform.position.x - 6.6f;
                        if (edge > forestClearance) holder.position += Vector3.left * (edge - forestClearance);
                    }
                    // Keep the entire house away from previously placed
                    // buildings without moving it into the walkable lane.
                    SeparateOutward(holder, side);
                    if (variant == 0 || variant == 3) aerialHouses.Add(holder);
                    foreach (var node in holder.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
                }
                // A second row breaks the empty skyline beyond the nearer roofs.
                var rear = CorridorEntranceModels.Group(transform, "Distant neighborhood roof", Vector3.zero);
                bool plasterRear = side < 0 && alternate;
                PlaceBackground(rear, plasterRear ? alternate : prefab,
                    plasterRear ? farPlaster : farBrick, plasterRear ? farPlasterMaterial : farBrickMaterial);
                CorridorEntranceModels.GroundAtHeight(rear,
                    new Vector3(side * 12.8f, 0, side < 0 ? 5f : 10f), plasterRear ? 5.0f : 5.1f,
                    Quaternion.Euler(0, side < 0 ? 83 : -96, 0));
                SeparateOutward(rear, side);
                foreach (var node in rear.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
                foreach (var renderer in rear.GetComponentsInChildren<Renderer>(true))
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            // A low roof beyond the rear wall gives the closed starting
            // boundary a lived-in streetscape when the player looks back.
            if (farHouse && farMaterial)
            {
                var backstreet = CorridorEntranceModels.Group(transform, "Backstreet roof beyond rear wall", Vector3.zero);
                PlaceBackground(backstreet, twoStory ? twoStory : prefab, farHouse, farMaterial);
                CorridorEntranceModels.GroundAtHeight(backstreet, new Vector3(.4f, 0, -22.8f), 5.8f,
                    Quaternion.Euler(0, 26f, 0));
                var backstreetBounds = MonsterPresentationScale.Bounds(backstreet);
                bool spaceAvailable = true;
                foreach (var occupied in occupiedHouses)
                    if (backstreetBounds.Intersects(occupied)) { spaceAvailable = false; break; }
                if (!spaceAvailable)
                {
                    // A visual backdrop must not prevent the approach loading
                    // when another installed house has a different footprint.
                    backstreet.gameObject.SetActive(false);
                    Destroy(backstreet.gameObject);
                }
                else
                {
                    occupiedHouses.Add(backstreetBounds);
                    aerialHouses.Add(backstreet);
                    foreach (var node in backstreet.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
                    foreach (var renderer in backstreet.GetComponentsInChildren<Renderer>(true))
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            // A second, lower roof continues the neighborhood beyond the
            // rear gate. It is presentation-only and never blocks the lane.
            if (farSlate && farSlateMaterial)
            {
                var backCorner = CorridorEntranceModels.Group(transform, "Backstreet slate roof", Vector3.zero);
                Instantiate(farSlate, backCorner, false);
                CorridorEntranceModels.GroundAtHeight(backCorner, new Vector3(10f, 0, -25f), 4.8f,
                    Quaternion.Euler(0, -27f, 0));
                var cornerBounds = MonsterPresentationScale.Bounds(backCorner);
                bool spaceAvailable = cornerBounds.max.z < transform.position.z - 17.35f;
                foreach (var occupied in occupiedHouses)
                    if (cornerBounds.Intersects(occupied)) { spaceAvailable = false; break; }
                if (!spaceAvailable)
                {
                    backCorner.gameObject.SetActive(false);
                    Destroy(backCorner.gameObject);
                }
                else
                {
                    occupiedHouses.Add(cornerBounds);
                    foreach (var node in backCorner.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
                    foreach (var renderer in backCorner.GetComponentsInChildren<Renderer>(true))
                    {
                        renderer.sharedMaterial = farSlateMaterial;
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                    }
                }
            }
            // The old forest horizon sits below this part of the village.
            // A visual-only residential yard keeps the distant houses grounded
            // without extending the walkable road or adding invisible barriers.
            Box("Residential ground behind shop", new Vector3(-26f, -.14f, -2f),
                new Vector3(48f, .22f, 50f), surfaces.Get("concrete-rough", new Color(.25f, .24f, .22f)), false);
            // Continue the same visual ground behind the start of the alley.
            // It overlaps the existing slab and the distant hillside edge, so
            // the dusk sky cannot show through their former open wedge. Keep
            // it slightly below the first slab to avoid coplanar flicker.
            Box("Residential ground behind village", new Vector3(-26.25f, -.16f, -48.5f),
                new Vector3(48.5f, .22f, 43.2f), surfaces.Get("concrete-rough", new Color(.25f, .24f, .22f)), false);
            // Continue the non-walkable yard behind the right end of the rear
            // gate; overlap the road below its surface without z-fighting.
            Box("Residential ground behind rear-right", new Vector3(12f, -.16f, -31f),
                new Vector3(18f, .22f, 28f), surfaces.Get("concrete-rough", new Color(.25f, .24f, .22f)), false);
            // A second authored boundary gives the rear houses a courtyard
            // edge and screens the broad empty ground beside the store.
            var backWall = Resources.Load<GameObject>("AlleyKoreanWall/KoreanWall");
            if (backWall)
            {
                var yardWall = CorridorEntranceModels.Group(transform, "Shop rear courtyard wall", Vector3.zero);
                Instantiate(backWall, yardWall, false);
                CorridorEntranceModels.GroundAtHeight(yardWall, new Vector3(-8.3f, 0, -15f), 2.15f,
                    Quaternion.Euler(0, 90, 0));
                foreach (var node in yardWall.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
                foreach (var renderer in yardWall.GetComponentsInChildren<Renderer>(true))
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                GetComponent<AlleyAuthoredWallSections>().TryBuildCourtyardReturn(yardWall, -17.2f, -3.7f);
            }
            gameObject.AddComponent<AlleyCourtyardPottery>().Prepare();
            // Roofs behind the shop fill the empty side skyline without adding
            // collision or changing the shop entrance and event corridor.
            for (int i = 0; i < 3; i++)
            {
                var rearShop = CorridorEntranceModels.Group(transform, "Residential roofs behind shop", Vector3.zero);
                bool useTwoStory = i == 0 && twoStory;
                bool useSlate = i == 1 && slate && farSlate && farSlateMaterial;
                bool usePlaster = !useTwoStory && !useSlate && alternate;
                bool useWaterTankHouse = i == 0 && waterTankHouse && waterTankMaterial;
                bool useTiledHouse = i == 2 && tiledHouse && tiledMaterial;
                PlaceBackground(rearShop, useSlate ? slate : (useTwoStory ? twoStory : (usePlaster ? alternate : prefab)),
                    useWaterTankHouse ? waterTankHouse : (useTiledHouse ? tiledHouse : (useSlate ? farSlate : (useTwoStory ? farHouse : (usePlaster ? farPlaster : null)))),
                    useWaterTankHouse ? waterTankMaterial : (useTiledHouse ? tiledMaterial : (useSlate ? farSlateMaterial : (useTwoStory ? farMaterial : (usePlaster ? farPlasterMaterial : null)))));
                CorridorEntranceModels.GroundAtHeight(rearShop,
                    new Vector3(-12.2f, 0, i == 2 ? -20f : -12f + i * 8f), i == 0 && (useTwoStory || useWaterTankHouse) ? 5.8f : 4.7f,
                    Quaternion.Euler(0, useWaterTankHouse ? 158f : i == 2 ? 104f : 83f + i * 9f, 0));
                SeparateOutward(rearShop, -1f);
                if (i == 0) aerialHouses.Add(rearShop);
                foreach (var node in rearShop.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
                foreach (var renderer in rearShop.GetComponentsInChildren<Renderer>(true))
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            // A lighter version of the authored house makes a second roofline
            // possible without duplicating the full-detail mesh or its texture.
            if (farHouse && farMaterial)
                for (int i = 0; i < 2; i++)
                {
                    var distant = CorridorEntranceModels.Group(transform, "Far residential roofline", Vector3.zero);
                    bool useSlate = i == 0 && farSlate && farSlateMaterial;
                    bool usePlaster = !useSlate && i == 0 && farPlaster && farPlasterMaterial;
                    Instantiate(useSlate ? farSlate : (usePlaster ? farPlaster : farHouse), distant, false);
                    CorridorEntranceModels.GroundAtHeight(distant,
                        new Vector3(-20.2f, 0, -15f + i * 12f), 5.2f + i * .5f,
                        Quaternion.Euler(0, 68f + i * 19f, 0));
                    SeparateOutward(distant, -1f);
                    foreach (var node in distant.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
                    foreach (var renderer in distant.GetComponentsInChildren<Renderer>(true))
                    {
                        renderer.sharedMaterial = useSlate ? farSlateMaterial : (usePlaster ? farPlasterMaterial : farMaterial);
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                    }
                }
            gameObject.AddComponent<AlleyRoofDetails>().Prepare(aerialHouses);
        }
        // Two continuous bends leave the shop, beast marker and mountain seam
        // at their original coordinates. Segment overlap keeps the colliders closed.
        static float RoadBend(float z)
        {
            if (z <= -3 || z >= 15) return 0;
            if (z <= 5) return 1.25f * Mathf.Sin((z + 3) * Mathf.PI / 8);
            return -1.25f * Mathf.Sin((z - 5) * Mathf.PI / 10);
        }
        void CurvedWall(float side, float start, float end, float height, Material wall, Material roof)
        {
            int originalStart = transform.childCount;
            const int count = 6;
            var surface = new GraphicsSurfaceLibrary.Geometry(Vector3.zero, GraphicsSurfaceLibrary.TileSpan(wall));
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.Lerp(start, end, i / (float)count);
                float b = Mathf.Lerp(start, end, (i + 1) / (float)count);
                float xa = side * 3.2f + RoadBend(a), xb = side * 3.2f + RoadBend(b);
                var center = new Vector3((xa + xb) * .5f, height * .5f, (a + b) * .5f);
                float angle = Mathf.Atan2(xb - xa, b - a) * Mathf.Rad2Deg;
                float length = Vector2.Distance(new Vector2(xa, a), new Vector2(xb, b)) + .06f;
                Box("Curved residential wall", center, new Vector3(.35f, height, length), wall, true);
                var segment = transform.GetChild(transform.childCount - 1);
                segment.localRotation = Quaternion.Euler(0, angle, 0);
                // Keep the original overlapping colliders, but draw a single
                // continuous wall skin so their cube seams do not show.
                segment.GetComponent<Renderer>().enabled = false;
                Vector3 Edge(float z, float x, float face)
                {
                    float slope = (RoadBend(z + .02f) - RoadBend(z - .02f)) / .04f;
                    return new Vector3(x, 0, z) + new Vector3(1, 0, -slope).normalized * (.175f * face);
                }
                // Split only the visual wall where the authored gate joins.
                // Collider segments remain untouched and continuous.
                var footing = surfaces.Get("alley-concrete-wall", new Color(.66f, .64f, .60f));
                void DrawWallPart(float from, float to)
                {
                    if (to - from < .001f) return;
                    float fromX = side * 3.2f + RoadBend(from), toX = side * 3.2f + RoadBend(to);
                    var nearA = Edge(from, fromX, -1); var nearB = Edge(to, toX, -1);
                    var farA = Edge(from, fromX, 1); var farB = Edge(to, toX, 1);
                    Vector3 up = Vector3.up * height;
                    surface.Quad(nearA, nearB, nearB + up, nearA + up);
                    surface.Quad(farB, farA, farA + up, farB + up);
                    var partCenter = new Vector3((fromX + toX) * .5f, 0, (from + to) * .5f);
                    float partAngle = Mathf.Atan2(toX - fromX, to - from) * Mathf.Rad2Deg;
                    bool meetsGate = authoredGateReady && side > 0 &&
                        (Mathf.Approximately(to, AlleyAuthoredGate.Start) || Mathf.Approximately(from, AlleyAuthoredGate.End));
                    float partLength = Vector2.Distance(new Vector2(fromX, from), new Vector2(toX, to)) + (meetsGate ? .005f : .06f);
                    TiltedBox("Curved wall coping", partCenter + Vector3.up * (height + .04f),
                        new Vector3(.45f, .12f, partLength), Quaternion.Euler(0, partAngle, 0), roof);
                    TiltedBox("Weathered wall footing", partCenter + Vector3.up * .09f,
                        new Vector3(.39f, .18f, partLength), Quaternion.Euler(0, partAngle, 0), footing);
                }
                bool cutsGate = authoredGateReady && side > 0 && a < AlleyAuthoredGate.End && b > AlleyAuthoredGate.Start;
                if (cutsGate)
                {
                    DrawWallPart(a, Mathf.Min(b, AlleyAuthoredGate.Start));
                    DrawWallPart(Mathf.Max(a, AlleyAuthoredGate.End), b);
                }
                else DrawWallPart(a, b);
                if (i % 3 == 0 && !(authoredGateReady && side > 0 && a > AlleyAuthoredGate.Start && a < AlleyAuthoredGate.End))
                {
                    Box("Concrete wall pier", new Vector3(xa, height * .5f, a),
                        new Vector3(.41f, height + .04f, .22f), footing, false);
                    Box("Concrete wall pier cap", new Vector3(xa, height + .06f, a),
                        new Vector3(.48f, .08f, .30f), footing, false);
                }
            }
            var wallMesh = surface.Mesh("Continuous curved wall skin");
            wallMeshes.Add(wallMesh);
            var wallSkin = new GameObject("Continuous curved wall skin");
            wallSkin.layer = 8; wallSkin.transform.SetParent(transform, false);
            wallSkin.AddComponent<MeshFilter>().sharedMesh = wallMesh;
            wallSkin.AddComponent<MeshRenderer>().sharedMaterial = wall;
            bool replaceVisual = false;
            if (side < 0 && Mathf.Approximately(start, -3f))
                replaceVisual = gameObject.AddComponent<AlleyAuthoredWallSections>().TryBuild(start, end, height, RoadBend);
            else if (side < 0 && (Mathf.Approximately(start, 3f) || Mathf.Approximately(start, 9f)))
            {
                var sections = GetComponent<AlleyWeatheredWallSections>();
                if (!sections) sections = gameObject.AddComponent<AlleyWeatheredWallSections>();
                replaceVisual = sections.TryBuild(start, end, height, RoadBend);
            }
            else if (side > 0 && Mathf.Approximately(start, -3f))
            {
                var sections = GetComponent<AlleyWeatheredWallSections>();
                if (!sections) sections = gameObject.AddComponent<AlleyWeatheredWallSections>();
                float visibleEnd = authoredGateReady ? Mathf.Min(end, AlleyAuthoredGate.Start) : end;
                replaceVisual = sections.TryBuild(start, visibleEnd, height, RoadBend, 1);
                rightWeatheredReady = replaceVisual;
            }
            if (replaceVisual)
            {
                // Keep all original curved colliders, replacing only their
                // matching render surfaces and caps with the authored section.
                for (int index = originalStart; index < transform.childCount; index++)
                {
                    var child = transform.GetChild(index);
                    if (child.name != "Continuous curved wall skin" && child.name != "Curved wall coping" &&
                        child.name != "Weathered wall footing" && child.name != "Concrete wall pier" &&
                        child.name != "Concrete wall pier cap") continue;
                    var renderer = child.GetComponent<Renderer>();
                    if (renderer) renderer.enabled = false;
                }
            }
        }
        // Preserve the measured opening across the original wall segments.
        void ShopDoorWallSegments(float start, float end, float height, Material wall)
        {
            const float left = -9.09946f, right = -8.11197f, top = 1.859f;
            void Segment(float a, float b, float bottom, float upper)
            {
                if (b - a < .005f || upper <= bottom) return;
                Box("Weathered residential wall", new Vector3(-3.2f, (bottom + upper) * .5f, (a + b) * .5f),
                    new Vector3(.35f, upper - bottom, b - a), wall, true);
            }
            Segment(start, Mathf.Min(end, left), 0, height);
            Segment(Mathf.Max(start, right), end, 0, height);
            Segment(Mathf.Max(start, left), Mathf.Min(end, right), top, height);
        }

        void BuildShopInterior(Material wall, Material wood)
        {
            // A small replaceable interior joins the existing road at y=0.
            // Coordinates are relative to the village origin (500,0,400).
            Box("Shop interior floor", new Vector3(-4.7f, -.12f, -8.6f), new Vector3(3, .24f, 5.5f),
                surfaces.Get("concrete-rough", new Color(.34f, .30f, .26f)), true);
            Box("Shop interior back wall", new Vector3(-6.12f, 1.2f, -8.6f), new Vector3(.12f, 2.4f, 5.5f), wall, true);
            foreach (float z in new[] { -11.3f, -5.9f })
            {
                // Keep the original collision boundary, but do not let its
                // full rectangular placeholder protrude through the scanned facade.
                var boundary = Box("Shop interior side wall", new Vector3(-4.95f, 1.2f, z),
                    new Vector3(2.3f, 2.4f, .12f), wall, true);
                boundary.GetComponent<Renderer>().enabled = false;
                // A one-sided plane is visible from the room but cannot
                // cover the exterior when the scan tapers at its side edges.
                var inside = GameObject.CreatePrimitive(PrimitiveType.Quad);
                inside.name = "Shop interior inset side wall"; inside.layer = 8;
                inside.transform.SetParent(transform, false);
                inside.transform.localPosition = new Vector3(-4.95f, 1.2f, z);
                inside.transform.localRotation = Quaternion.Euler(0, z < -8 ? 180 : 0, 0);
                inside.transform.localScale = new Vector3(2.3f, 2.4f, 1);
                inside.GetComponent<Renderer>().sharedMaterial = wall;
                Destroy(inside.GetComponent<Collider>());
            }
            Box("Shop interior counter", new Vector3(-5.6f, .48f, -7.45f), new Vector3(.38f, .96f, 1.7f), wood, true);
            foreach (float z in new[] { -9.09946f, -8.11197f })
                Box("Shop entrance timber jamb", new Vector3(-3.89f, .93f, z), new Vector3(.12f, 1.86f, .09f), wood, false);
            Box("Shop entrance timber lintel", new Vector3(-3.89f, 1.87f, -8.6057f), new Vector3(.12f, .12f, 1.08f), wood, false);
            for (int shelf = 0; shelf < 3; shelf++)
                Box("Shop interior shelf", new Vector3(-6.0f, .65f + shelf * .43f, -7.5f), new Vector3(.32f, .06f, 1.6f), wood, false);
            for (int crate = 0; crate < 2; crate++)
                Box("Shop plain drink crate", new Vector3(-5.6f, .17f, -10f + crate * .55f), new Vector3(.45f, .34f, .45f), wood, false);
            var lamp = new GameObject("Shop interior warm bulb"); lamp.transform.SetParent(transform, false);
            lamp.transform.localPosition = new Vector3(-4.9f, 2.1f, -8.0f);
            var light = lamp.AddComponent<Light>(); light.type = LightType.Point; light.color = new Color(1f, .57f, .27f);
            light.range = 4f; light.intensity = 1.2f; light.shadows = LightShadows.None;
        }
        // Endpoints are fitted to the visible front mesh of the authored
        // walk-in shop, rather than its much broader renderer bounds.
        void AttachProjectingSign()
        {
            if (!walkInStorefront) return;
            var finish = surfaces.Get("metal-rust", new Color(.28f, .26f, .23f));
            foreach (float y in new[] { 2.46f, 3.04f })
            {
                var start = new Vector3(-3.025f, y, -11.2f);
                var end = new Vector3(y < 2.75f ? -4.10f : -3.86f, y, -10.9f);
                Vector3 delta = end - start;
                var bracket = Box("Projecting sign mounting bracket", (start + end) * .5f,
                    new Vector3(.07f, .07f, delta.magnitude + .10f), finish, false);
                bracket.transform.localRotation = Quaternion.LookRotation(delta, Vector3.up);
                bracket.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        void InstallStorefront()
        {
            var prefab = walkInStorefront ? walkInStorefront : Resources.Load<GameObject>("AlleyStorefront/Storefront");
            if (!prefab) return; // Preserve the original facade on old builds.
            var holder = CorridorEntranceModels.Group(transform, "Authored Korean neighborhood shop", Vector3.zero);
            Instantiate(prefab, holder, false);
            // The FBX retains Blender's X-axis up conversion under the prefab.
            // Its shop front points along Unity +Z before this yaw; rotate it
            // toward the street (+X), not toward the wall behind it.
            CorridorEntranceModels.GroundAtHeight(holder, new Vector3(-4.2f, 0, -8), 4.0f, Quaternion.Euler(0, 90, 0));
            foreach (var node in holder.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
            var bounds = MonsterPresentationScale.Bounds(holder);
            holder.position += Vector3.right * ((transform.position.x - 2.99f) - bounds.max.x);
            AttachProjectingSign();
            // Only presentation geometry is replaced. Original wall colliders,
            // item targets, the projecting sign and freezer remain untouched.
            foreach (Transform child in transform)
            {
                string n = child.name;
                bool previousShop = n.StartsWith("Shop frontage") || n.StartsWith("Shop upper sign") ||
                    n.StartsWith("Shop timber post") || n.StartsWith("Striped shop awning") ||
                    n.StartsWith("Weathered awning") || n.StartsWith("Shop window") || n.StartsWith("Shop warm window") || n.StartsWith("Window ") ||
                    n.StartsWith("Store jar") || n.StartsWith("Store display") || n.StartsWith("Damaged window") ||
                    n.StartsWith("Shop entrance frame") || n.StartsWith("Shop entrance wood") ||
                    n.StartsWith("Shop entrance upper") || n.StartsWith("Shop entrance handle") ||
                    n.StartsWith("우암상회 hand-painted");
                bool hiddenOldWall = child.localPosition.x < -3 && child.localPosition.z < -3 &&
                    child.localPosition.z > -15 && (n == "Weathered residential wall" || n == "Wall tile coping" ||
                    n == "House facade above wall" || n == "Sloping dark tile roof" || n == "Roof tile ridge");
                if (previousShop || hiddenOldWall)
                {
                    var renderer = child.GetComponent<Renderer>();
                    if (renderer) renderer.enabled = false;
                }
            }
        }
        void TiltedBox(string label, Vector3 point, Vector3 size, Quaternion rotation, Material material)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = label; piece.layer = 8; piece.transform.SetParent(transform, false);
            piece.transform.localPosition = point; piece.transform.localRotation = rotation; piece.transform.localScale = size;
            piece.GetComponent<Renderer>().sharedMaterial = material;
            if (label == "Curved wall coping")
                piece.GetComponent<MeshFilter>().sharedMesh = surfaces.MetreBoxMesh(size, 0, GraphicsSurfaceLibrary.TileSpan(material));
            Destroy(piece.GetComponent<Collider>());
        }
        GameObject Box(string label, Vector3 point, Vector3 size, Material material, bool collision)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = label; piece.layer = 8; piece.transform.SetParent(transform, false);
            piece.transform.localPosition = point; piece.transform.localScale = size;
            piece.GetComponent<Renderer>().sharedMaterial = material;
            // The rear yard spans tens of metres: default cube UVs stretch a
            // single texture across it and erase the surface detail.
            if (label == "Alley pavement" || label == "Residential ground behind shop" ||
                label == "Residential ground behind village" || label == "Residential ground behind rear-right")
                piece.GetComponent<MeshFilter>().sharedMesh = surfaces.MetreBoxMesh(size, 0, surfaces.TileSpan("concrete-rough"));
            else if (label == "Weathered residential wall" || label == "Curved residential wall" ||
                label == "Concrete wall pier" || label == "Rear wall corner pier" || label == "Wall tile coping")
                piece.GetComponent<MeshFilter>().sharedMesh = surfaces.MetreBoxMesh(size, 0, GraphicsSurfaceLibrary.TileSpan(material));
            if (!collision) Destroy(piece.GetComponent<Collider>());
            return piece;
        }
        void Cylinder(string label, Vector3 point, Vector3 scale, Material material, bool collision)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            piece.name = label; piece.layer = 8; piece.transform.SetParent(transform, false);
            piece.transform.localPosition = point; piece.transform.localScale = scale;
            piece.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) Destroy(piece.GetComponent<Collider>());
        }
        void OnDestroy()
        {
            surfaces.Dispose();
            if (storeSignMaterial) Destroy(storeSignMaterial);
            foreach (var wallMesh in wallMeshes) if (wallMesh) Destroy(wallMesh);
        }
    }
}
