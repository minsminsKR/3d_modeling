using System;
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
        Material storeSignMaterial;
        GameObject walkInStorefront;

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
            var road = surfaces.Get("concrete-rough", new Color(.105f, .10f, .095f));
            var wall = surfaces.Get("plaster-damp", new Color(.61f, .55f, .48f));
            var dark = surfaces.Get("wood-aged", new Color(.28f, .22f, .18f));
            var roof = surfaces.Get("concrete-rough", new Color(.21f, .20f, .21f));
            var iron = surfaces.Get("painted-metal", new Color(.22f, .23f, .22f));
            var red = surfaces.Get("cloth-charred", new Color(.54f, .24f, .19f));
            var cream = surfaces.Get("cloth-charred", new Color(.72f, .64f, .51f));
            Box("Alley pavement", new Vector3(0, -.12f, -1.25f), new Vector3(10.5f, .24f, 31.5f), road, true);
            gameObject.AddComponent<AlleyRoadDressing>().Prepare();
            var pine = Resources.Load<GameObject>("TemplePrologue/pine-trunk-review");
            if (pine)
                foreach (float side in new[] { -1f, 1f })
                    for (int i = 0; i < 2; i++)
                    {
                        var tree = Instantiate(pine, transform, false);
                        tree.name = "Mountain forest visible beyond alley";
                        tree.transform.localPosition = new Vector3(side * (5.8f + i * 2), -.1f, 20 + i * 5);
                        tree.transform.localRotation = Quaternion.Euler(0, side * 45 + i * 110, 0);
                        foreach (var renderer in tree.GetComponentsInChildren<Renderer>(true))
                            renderer.shadowCastingMode = ShadowCastingMode.Off;
                    }
            for (int i = 0; i < 5; i++)
            {
                float z = -12 + i * 6;
                foreach (float side in new[] { -1f, 1f })
                {
                    float height = i == 4 ? 2.1f : 2.8f;
                    if (walkInStorefront && side < 0 && (i == 0 || i == 1))
                        ShopDoorWallSegments(z - 3, z + 3, height, wall);
                    else if (i < 2)
                        Box("Weathered residential wall", new Vector3(side * 3.2f, height * .5f, z), new Vector3(.35f, height, 6), wall, true);
                    else
                        CurvedWall(side, z - 3, z + 3, height, wall, roof);
                    if (i < 2)
                        Box("Wall tile coping", new Vector3(side * 3.2f, height + .05f, z), new Vector3(.58f, .16f, 6), roof, false);
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
                panel.transform.localPosition = new Vector3(-2.63f, 2.75f, -11.265f);
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
            for (int i = 0; i < 3; i++)
            {
                float z = -3 + i * 7;
                Box("House gate", new Vector3(3.01f, 1.06f, z), new Vector3(.08f, 2.12f, 1.75f), iron, false);
                for (int bar = 0; bar < 7; bar++)
                    Box("Gate vertical rail", new Vector3(2.95f, 1.08f, z - .7f + bar * .23f), new Vector3(.05f, 1.9f, .035f), dark, false);
            }
            for (int i = 0; i < 4; i++)
            {
                float z = -10 + i * 8;
                Cylinder("Utility pole", new Vector3(2.76f, 2.7f, z), new Vector3(.18f, 2.7f, .18f), iron, true);
                var polePrefab = Resources.Load<GameObject>("AlleyUtilityPole/UtilityPole");
                if (polePrefab && i >= 2)
                {
                    var previousPole = transform.GetChild(transform.childCount - 1).GetComponent<Renderer>();
                    if (previousPole) previousPole.enabled = false;
                    var holder = CorridorEntranceModels.Group(transform, "Weathered Korean utility pole", Vector3.zero);
                    Instantiate(polePrefab, holder, false);
                    CorridorEntranceModels.GroundAtHeight(holder, new Vector3(2.76f, 0, z), 5.4f, Quaternion.identity);
                    foreach (var node in holder.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
                }
                var light = new GameObject("Warm alley lamp"); light.transform.SetParent(transform, false);
                light.transform.localPosition = new Vector3(2.47f, 3.3f, z);
                var lamp = light.AddComponent<Light>(); lamp.type = LightType.Point; lamp.color = new Color(1, .66f, .35f);
                lamp.range = 6; lamp.intensity = .8f; lamp.shadows = LightShadows.None;
                Box("Lamp arm", new Vector3(2.57f, 3.34f, z), new Vector3(.45f, .07f, .07f), iron, false);
                if (i < 3) Box("Overhead wire", new Vector3(2.76f, 5.2f, z + 4), new Vector3(.025f, .025f, 8), iron, false);
            }
            for (int wire = 0; wire < 5; wire++)
                Wire(new Vector3(-3.15f, 4.2f + wire * .14f, -10 + wire * 4.7f),
                    new Vector3(2.76f, 4.8f + wire * .08f, -7 + wire * 4.7f), iron);
            Box("Alley rear boundary", new Vector3(0, 1.4f, -17.2f), new Vector3(8, 2.8f, .3f), wall, true);
            InstallResidentialHouses();
            InstallStorefront();
            if (walkInStorefront) BuildShopInterior(wall, dark);
        }

        void InstallResidentialHouses()
        {
            var prefab = Resources.Load<GameObject>("AlleyBrickHouse/BrickHouse");
            if (!prefab) return;
            // The existing colliding garden walls remain the navigation boundary.
            foreach (Transform child in transform)
            {
                bool oldHouse = child.name == "House facade above wall" || child.name == "Sloping dark tile roof" || child.name == "Roof tile ridge";
                if (oldHouse && child.localPosition.z > -3f)
                {
                    var renderer = child.GetComponent<Renderer>();
                    if (renderer) renderer.enabled = false;
                }
            }
            var alternate = Resources.Load<GameObject>("AlleyPlasterHouse/PlasterHouse");
            int houseIndex = 0;
            foreach (float side in new[] { -1f, 1f })
            {
                foreach (float z in new[] { 0f, 8f, 15f })
                {
                    var holder = CorridorEntranceModels.Group(transform, "Old Korean brick house", Vector3.zero);
                    var selected = alternate && houseIndex++ % 2 == 0 ? alternate : prefab;
                    Instantiate(selected, holder, false);
                    CorridorEntranceModels.GroundAtHeight(holder,
                        new Vector3(side * 6.8f + RoadBend(z), 0, z), 4.3f,
                        Quaternion.Euler(0, side < 0 ? 90 : -90, 0));
                    foreach (var node in holder.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
                }
            }
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
            const int count = 6;
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.Lerp(start, end, i / (float)count);
                float b = Mathf.Lerp(start, end, (i + 1) / (float)count);
                float xa = side * 3.2f + RoadBend(a), xb = side * 3.2f + RoadBend(b);
                var center = new Vector3((xa + xb) * .5f, height * .5f, (a + b) * .5f);
                float angle = Mathf.Atan2(xb - xa, b - a) * Mathf.Rad2Deg;
                float length = Vector2.Distance(new Vector2(xa, a), new Vector2(xb, b)) + .06f;
                Box("Curved residential wall", center, new Vector3(.35f, height, length), wall, true);
                transform.GetChild(transform.childCount - 1).localRotation = Quaternion.Euler(0, angle, 0);
                TiltedBox("Curved wall coping", new Vector3(center.x, height + .05f, center.z),
                    new Vector3(.58f, .16f, length), Quaternion.Euler(0, angle, 0), roof);
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
                Box("Shop interior side wall", new Vector3(-4.95f, 1.2f, z), new Vector3(2.3f, 2.4f, .12f), wall, true);
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
            // Only presentation geometry is replaced. Original wall colliders,
            // item targets, the projecting sign and freezer remain untouched.
            foreach (Transform child in transform)
            {
                string n = child.name;
                bool previousShop = n.StartsWith("Shop frontage") || n.StartsWith("Shop upper sign") ||
                    n.StartsWith("Shop timber post") || n.StartsWith("Striped shop awning") ||
                    n.StartsWith("Weathered awning") || n.StartsWith("Shop window") || n.StartsWith("Window ") ||
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
        void Wire(Vector3 a, Vector3 b, Material material)
        {
            var segment = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            segment.name = "Overhead crossing cable"; segment.layer = 8;
            segment.transform.SetParent(transform, false);
            segment.transform.localPosition = (a + b) * .5f;
            segment.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            segment.transform.localScale = new Vector3(.018f, Vector3.Distance(a, b) * .5f, .018f);
            segment.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(segment.GetComponent<Collider>());
        }
        void TiltedBox(string label, Vector3 point, Vector3 size, Quaternion rotation, Material material)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = label; piece.layer = 8; piece.transform.SetParent(transform, false);
            piece.transform.localPosition = point; piece.transform.localRotation = rotation; piece.transform.localScale = size;
            piece.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(piece.GetComponent<Collider>());
        }
        void Box(string label, Vector3 point, Vector3 size, Material material, bool collision)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = label; piece.layer = 8; piece.transform.SetParent(transform, false);
            piece.transform.localPosition = point; piece.transform.localScale = size;
            piece.GetComponent<Renderer>().sharedMaterial = material;
            if (label == "Alley pavement")
                piece.GetComponent<MeshFilter>().sharedMesh = surfaces.MetreBoxMesh(size, 0, surfaces.TileSpan("concrete-rough"));
            if (!collision) Destroy(piece.GetComponent<Collider>());
        }
        void Cylinder(string label, Vector3 point, Vector3 scale, Material material, bool collision)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            piece.name = label; piece.layer = 8; piece.transform.SetParent(transform, false);
            piece.transform.localPosition = point; piece.transform.localScale = scale;
            piece.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) Destroy(piece.GetComponent<Collider>());
        }
        void OnDestroy() { surfaces.Dispose(); if (storeSignMaterial) Destroy(storeSignMaterial); }
    }
}
